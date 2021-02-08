/*
    CSharpEditor - A C# source code editor with syntax highlighting, intelligent
    code completion and real-time compilation error checking.
    Copyright (C) 2021  Giorgio Bianchini
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, version 3.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace CSharpEditor
{
    internal class MultiItemContainer : UserControl
    {
        public MultiItemContainer()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<Button>("ExpanderButton").Click += (s, e) =>
            {
                if (IsExpanded)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").IsVisible = false;
                    this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M3.5,0.7 L3.5,11.3 L8.5,6 Z");
                    this.FindControl<Path>("ExpanderPath").Fill = Brushes.White;
                    IsExpanded = false;
                }
                else
                {
                    if (!ChildrenInitialized)
                    {
                        InitializeChildren();
                        ChildrenInitialized = true;
                    }

                    this.FindControl<StackPanel>("ChildrenContainer").IsVisible = true;
                    this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M2.5,9.5 L9.5,2.5 L9.5,9.5 Z");
                    this.FindControl<Path>("ExpanderPath").Fill = Brushes.Black;
                    IsExpanded = true;
                }
            };
        }

        bool IsExpanded = false;
        bool ChildrenInitialized = false;

        int MinIndex;
        int MaxIndex;
        List<object> AllItems = null;
        List<KeyValuePair<string, (object, bool, bool)>> AllProperties = null;
        List<KeyValuePair<string, (bool, bool)>> AllRemoteProperties = null;
        (string itemId, VariableTypes itemType, object itemValue)[] AllRemoteItems = null;
        string OwnerObjectId = null;
        PropertyOrFieldGetter PropertyOrFieldGetter = null;
        ItemsGetter ItemsGetter = null;

        ToggleButton NonPublicVisibilityToggle;

        public MultiItemContainer(int minIndex, int maxIndex, List<object> allItems, ToggleButton nonPublicVisibilityToggle)
        {
            this.InitializeComponent();
            this.FindControl<StackPanel>("VariableContainer").Children.Add(new TextBlock() { Text = minIndex.ToString() + "…" + maxIndex });
            this.MinIndex = minIndex;
            this.MaxIndex = maxIndex;
            this.AllItems = allItems;
            this.NonPublicVisibilityToggle = nonPublicVisibilityToggle;
        }

        public MultiItemContainer(int minIndex, int maxIndex, List<KeyValuePair<string, (object, bool, bool)>> allItems, ToggleButton nonPublicVisibilityToggle)
        {
            this.InitializeComponent();
            this.FindControl<StackPanel>("VariableContainer").Children.Add(new TextBlock() { Text = minIndex.ToString() + "…" + maxIndex });
            this.MinIndex = minIndex;
            this.MaxIndex = maxIndex;
            this.AllProperties = allItems;
            this.NonPublicVisibilityToggle = nonPublicVisibilityToggle;
        }

        public MultiItemContainer(string ownerObjectId, int minIndex, int maxIndex, List<KeyValuePair<string, (bool, bool)>> allItems, ToggleButton nonPublicVisibilityToggle, PropertyOrFieldGetter propertyOrFieldGetter, ItemsGetter itemsGetter)
        {
            this.InitializeComponent();
            this.FindControl<StackPanel>("VariableContainer").Children.Add(new TextBlock() { Text = minIndex.ToString() + "…" + maxIndex });
            this.MinIndex = minIndex;
            this.MaxIndex = maxIndex;
            this.AllRemoteProperties = allItems;
            this.PropertyOrFieldGetter = propertyOrFieldGetter;
            this.ItemsGetter = itemsGetter;
            this.OwnerObjectId = ownerObjectId;
            this.NonPublicVisibilityToggle = nonPublicVisibilityToggle;
        }

        public MultiItemContainer(string ownerObjectId, int minIndex, int maxIndex, (string itemId, VariableTypes itemType, object itemValue)[] allItems, ToggleButton nonPublicVisibilityToggle, PropertyOrFieldGetter propertyOrFieldGetter, ItemsGetter itemsGetter)
        {
            this.InitializeComponent();
            this.FindControl<StackPanel>("VariableContainer").Children.Add(new TextBlock() { Text = minIndex.ToString() + "…" + maxIndex });
            this.MinIndex = minIndex;
            this.MaxIndex = maxIndex;
            this.AllRemoteItems = allItems;
            this.PropertyOrFieldGetter = propertyOrFieldGetter;
            this.ItemsGetter = itemsGetter;
            this.OwnerObjectId = ownerObjectId;
            this.NonPublicVisibilityToggle = nonPublicVisibilityToggle;
        }

        private void InitializeChildren()
        {
            if (AllItems != null)
            {
                if (MaxIndex - MinIndex + 1 <= 10)
                {
                    for (int i = MinIndex; i <= MaxIndex; i++)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(null, AllItems[i], new TaggedText[] { new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, i.ToString()), new TaggedText(TextTags.Operator, "]") }, NonPublicVisibilityToggle) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
                else
                {
                    int depth = (int)Math.Floor(Math.Log10(MaxIndex - MinIndex));

                    int step = (int)Math.Pow(10, depth);

                    for (int i = MinIndex; i <= MaxIndex; i += step)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(i, Math.Min(i + step - 1, MaxIndex), AllItems, NonPublicVisibilityToggle) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
            }
            else if (AllProperties != null)
            {
                if (MaxIndex - MinIndex + 1 <= 10)
                {
                    for (int i = MinIndex; i <= MaxIndex; i++)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(null, AllProperties[i].Value.Item1, new TaggedText[] { new TaggedText(TextTags.Property, AllProperties[i].Key) }, NonPublicVisibilityToggle, isProperty: AllProperties[i].Value.Item2, isField: !AllProperties[i].Value.Item2, isPrivate: AllProperties[i].Value.Item3) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
                else
                {
                    int depth = (int)Math.Floor(Math.Log10(MaxIndex - MinIndex));

                    int step = (int)Math.Pow(10, depth);

                    for (int i = MinIndex; i <= MaxIndex; i += step)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(i, Math.Min(i + step - 1, MaxIndex), AllProperties, NonPublicVisibilityToggle) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
            }
            else if (AllRemoteProperties != null)
            {
                if (MaxIndex - MinIndex + 1 <= 10)
                {
                    for (int i = MinIndex; i <= MaxIndex; i++)
                    {
                        (string propertyId, VariableTypes propertyType, object propertyValue) property = this.PropertyOrFieldGetter(OwnerObjectId, AllRemoteProperties[i].Key, AllRemoteProperties[i].Value.Item1);

                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(property.propertyId, null, property.propertyType, property.propertyValue, new TaggedText[] { new TaggedText(TextTags.Property, AllRemoteProperties[i].Key) }, NonPublicVisibilityToggle, this.PropertyOrFieldGetter, this.ItemsGetter, isProperty: AllRemoteProperties[i].Value.Item1, isField: !AllRemoteProperties[i].Value.Item1, isPrivate: AllRemoteProperties[i].Value.Item2) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
                else
                {
                    int depth = (int)Math.Floor(Math.Log10(MaxIndex - MinIndex));

                    int step = (int)Math.Pow(10, depth);

                    for (int i = MinIndex; i <= MaxIndex; i += step)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(this.OwnerObjectId, i, Math.Min(i + step - 1, MaxIndex), AllRemoteProperties, NonPublicVisibilityToggle, PropertyOrFieldGetter, ItemsGetter) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
            }
            else if (AllRemoteItems != null)
            {
                if (MaxIndex - MinIndex + 1 <= 10)
                {
                    for (int i = MinIndex; i <= MaxIndex; i++)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(AllRemoteItems[i].itemId, null, AllRemoteItems[i].itemType, AllRemoteItems[i].itemValue, new TaggedText[] { new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, i.ToString()), new TaggedText(TextTags.Operator, "]") }, NonPublicVisibilityToggle, this.PropertyOrFieldGetter, this.ItemsGetter) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
                else
                {
                    int depth = (int)Math.Floor(Math.Log10(MaxIndex - MinIndex));

                    int step = (int)Math.Pow(10, depth);

                    for (int i = MinIndex; i <= MaxIndex; i += step)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(OwnerObjectId, i, Math.Min(i + step - 1, MaxIndex), AllRemoteItems, NonPublicVisibilityToggle, PropertyOrFieldGetter, ItemsGetter) { Margin = new Thickness(16, 0, 0, 0) });
                    }
                }
            }
        }
    }
}
