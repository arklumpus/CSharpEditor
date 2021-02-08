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
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;

namespace CSharpEditor
{
    internal delegate (string propertyId, VariableTypes propertyType, object propertyValue) PropertyOrFieldGetter(string variableId, string propertyName, bool isProperty);

    internal delegate (string itemId, VariableTypes itemType, object itemValue)[] ItemsGetter(string variableId);

    internal enum VariableTypes
    {
        String,
        Char,
        Number,
        Bool,
        Null,
        Enum,
        Class,
        Delegate,
        Interface,
        Other,
        IEnumerable
    }

    internal class VariableExpander : UserControl
    {
        public VariableExpander()
        {
            this.InitializeComponent();
        }

        internal ToggleButton NonPublicVisibilityToggle;

        public VariableExpander(string variableName, object variableValue, TaggedText[] variableDisplayName, ToggleButton nonPublicVisibilityToggle, double fontSize = 14, bool isProperty = false, bool isField = false, bool isPrivate = false)
        {
            this.InitializeComponent();

            this.NonPublicVisibilityToggle = nonPublicVisibilityToggle;

            VectSharp.Font labelFont = new VectSharp.Font(Editor.OpenSansRegular, fontSize);
            VectSharp.Font codeFont = new VectSharp.Font(Editor.RobotoMonoRegular, fontSize);

            Canvas iconCanvas = new Canvas() { Width = 18, Height = 16 };

            Control nameControl;
            Control valueControl;

            if (variableValue is string str)
            {
                iconCanvas.Children.Add(new TypeIcons.StringIcon());

                string stringLiteral = str.Substring(0, Math.Min(str.Length, 30));

                if (str.Length > 30)
                {
                    stringLiteral += "…";
                }

                stringLiteral = stringLiteral.ToLiteral();

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.StringLiteral, stringLiteral) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, str);

                this.FindControl<Button>("InspectButton").IsVisible = true;

                this.FindControl<Button>("InspectButton").Click += async (s, e) =>
                {
                    Window window = new Window() { Width = 450, Height = 200 };
                    window.Title = "Inspect string";
                    window.Content = new TextBox() { Text = str, IsReadOnly = true, AcceptsReturn = true, FontSize = fontSize, FontFamily = FontFamily.Parse("resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Roboto Mono") };

                    await window.ShowDialog(this.FindAncestorOfType<Window>());
                };
            }
            else if (variableValue is char chr)
            {
                string stringLiteral = chr.ToLiteral();

                iconCanvas.Children.Add(new TypeIcons.CharIcon());

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.StringLiteral, stringLiteral) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, chr);
            }
            else if (variableValue is long || variableValue is int || variableValue is double || variableValue is decimal || variableValue is ulong || variableValue is uint || variableValue is short || variableValue is ushort || variableValue is byte || variableValue is sbyte || variableValue is float)
            {
                iconCanvas.Children.Add(new TypeIcons.NumberIcon());

                string stringValue = Convert.ToString(variableValue, System.Globalization.CultureInfo.InvariantCulture);

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.NumericLiteral, stringValue) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, stringValue);
            }
            else if (variableValue is bool bol)
            {
                iconCanvas.Children.Add(new TypeIcons.BoolIcon());

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.Keyword, bol.ToString().ToLower()) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, bol.ToString().ToLower());
            }
            else if (variableValue is null)
            {
                iconCanvas.Children.Add(new TypeIcons.NullIcon());

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.Keyword, "null") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, "null");
            }
            else if (variableValue is IEnumerable enumerable)
            {
                string enumerableType = "";

                int count = -1;

                foreach (Type interf in ((Type)variableValue.GetType()).GetInterfaces())
                {
                    if (interf.IsGenericType && interf.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        foreach (Type tp in interf.GetGenericArguments())
                        {
                            enumerableType += tp.Name + ", ";
                        }
                    }
                }

                if (enumerableType.Length > 2)
                {
                    enumerableType = enumerableType[0..^2];
                }

                if (variableValue is ICollection collection)
                {
                    count = collection.Count;
                }

                iconCanvas.Children.Add(new TypeIcons.ArrayIcon());

                if (count >= 0)
                {
                    if (!string.IsNullOrEmpty(variableName))
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, count.ToString()), new TaggedText(TextTags.Operator, "]") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                    else
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Interface, "ICollection"), new TaggedText(TextTags.Operator, "<"), new TaggedText(TextTags.Class, enumerableType), new TaggedText(TextTags.Operator, ">"), new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, count.ToString()), new TaggedText(TextTags.Operator, "]") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(variableName))
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "[]") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                    else
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Interface, "IEnumerable"), new TaggedText(TextTags.Operator, "<"), new TaggedText(TextTags.Class, enumerableType), new TaggedText(TextTags.Operator, ">") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                }

                ToolTip.SetTip(this, "IEnumerable<" + enumerableType + ">");

                this.FindControl<Button>("ExpanderButton").IsVisible = true;

                bool expanderOpen = false;
                bool childrenInitialized = false;

                this.FindControl<Button>("ExpanderButton").Click += (s, e) =>
                {
                    if (expanderOpen)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").IsVisible = false;
                        this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M3.5,0.7 L3.5,11.3 L8.5,6 Z");
                        this.FindControl<Path>("ExpanderPath").Fill = Brushes.White;
                        expanderOpen = false;
                    }
                    else
                    {
                        if (!childrenInitialized)
                        {
                            InitializeChildren(enumerable);
                            childrenInitialized = true;
                        }

                        this.FindControl<StackPanel>("ChildrenContainer").IsVisible = true;
                        this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M2.5,9.5 L9.5,2.5 L9.5,9.5 Z");
                        this.FindControl<Path>("ExpanderPath").Fill = Brushes.Black;
                        expanderOpen = true;
                    }
                };
            }
            else if (variableValue is Enum)
            {
                iconCanvas.Children.Add(new IntellisenseIcon.EnumIcon() { RenderTransform = new TranslateTransform(2, 2) });

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.Enum, variableValue.GetType().Name), new TaggedText(TextTags.Punctuation, "."), new TaggedText(TextTags.EnumMember, variableValue.ToString()) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, variableValue.GetType().Name + "." + variableValue.ToString());
            }
            else
            {
                if (variableValue.GetType().IsClass)
                {
                    if (variableValue is Delegate)
                    {
                        iconCanvas.Children.Add(new IntellisenseIcon.DelegateIcon() { Margin = new Thickness(2, 2, 0, 0) });
                    }
                    else
                    {
                        iconCanvas.Children.Add(new IntellisenseIcon.ClassIcon());
                    }
                }
                else if (variableValue.GetType().IsInterface)
                {
                    iconCanvas.Children.Add(new IntellisenseIcon.InterfaceIcon());
                }
                else
                {
                    iconCanvas.Children.Add(new IntellisenseIcon.StructIcon() { Margin = new Thickness(1, 2, 0, 0) });
                }

                List<System.Reflection.MemberInfo> propertyList = new List<System.Reflection.MemberInfo>();

                foreach (System.Reflection.MemberInfo prop in ((Type)variableValue.GetType()).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (prop.MemberType == System.Reflection.MemberTypes.Property || prop.MemberType == System.Reflection.MemberTypes.Field)
                    {
                        propertyList.Add(prop);
                    }
                }

                propertyList = new List<MemberInfo>(from el in propertyList where (el.MemberType == MemberTypes.Field && !((FieldInfo)el).IsStatic) || (el.MemberType == MemberTypes.Property && !((((PropertyInfo)el).GetMethod != null && ((PropertyInfo)el).GetMethod.IsStatic) || (((PropertyInfo)el).SetMethod != null && ((PropertyInfo)el).SetMethod.IsStatic))) orderby el.Name ascending select el);

                valueControl = new Canvas();

                if (propertyList.Count > 0)
                {
                    this.FindControl<Button>("ExpanderButton").IsVisible = true;

                    bool expanderOpen = false;
                    bool childrenInitialized = false;

                    this.FindControl<Button>("ExpanderButton").Click += (s, e) =>
                    {
                        if (expanderOpen)
                        {
                            this.FindControl<StackPanel>("ChildrenContainer").IsVisible = false;
                            this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M3.5,0.7 L3.5,11.3 L8.5,6 Z");
                            this.FindControl<Path>("ExpanderPath").Fill = Brushes.White;
                            expanderOpen = false;
                        }
                        else
                        {
                            if (!childrenInitialized)
                            {
                                Dictionary<string, (object, bool, bool)> properties = new Dictionary<string, (object, bool, bool)>();
                                foreach (MemberInfo property in propertyList)
                                {
                                    if (property.MemberType == MemberTypes.Field)
                                    {
                                        try
                                        {
                                            properties.Add(property.Name, (((FieldInfo)property).GetValue(variableValue), false, !((FieldInfo)property).IsPublic));
                                        }
                                        catch { }
                                    }
                                    else if (property.MemberType == MemberTypes.Property)
                                    {
                                        try
                                        {
                                            properties.Add(property.Name, (((PropertyInfo)property).GetValue(variableValue), true, !(((PropertyInfo)property).GetMethod?.IsPublic == true || ((PropertyInfo)property).SetMethod?.IsPublic == true)));
                                        }
                                        catch { }
                                    }
                                }

                                InitializeChildren(properties);
                                childrenInitialized = true;
                            }

                            this.FindControl<StackPanel>("ChildrenContainer").IsVisible = true;
                            this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M2.5,9.5 L9.5,2.5 L9.5,9.5 Z");
                            this.FindControl<Path>("ExpanderPath").Fill = Brushes.Black;
                            expanderOpen = true;
                        }
                    };
                }
            }

            if (isProperty)
            {
                iconCanvas.Children.Clear();
                iconCanvas.Children.Add(new IntellisenseIcon.PropertyIcon());
            }

            if (isField)
            {
                iconCanvas.Children.Clear();
                iconCanvas.Children.Add(new IntellisenseIcon.FieldIcon());
            }

            if (isPrivate)
            {
                iconCanvas.Children.Add(new TypeIcons.PrivateIcon() { UseLayoutRounding = true });
                this.Bind<bool>(Grid.IsVisibleProperty, nonPublicVisibilityToggle.GetBindingObservable(ToggleButton.IsCheckedProperty).Select(x => x.Value.Value));
            }

            nameControl = FormattedText.FormatDescription(variableDisplayName, null, labelFont, codeFont).Render(double.PositiveInfinity, false, iconCanvas);

            valueControl.Margin = new Thickness(4, 0, 0, 0);



            this.FindControl<StackPanel>("VariableContainer").Children.Add(nameControl);
            this.FindControl<StackPanel>("VariableContainer").Children.Add(valueControl);
        }

        public VariableExpander(string variableId, string variableName, VariableTypes variableType, object vvariableValue, TaggedText[] variableDisplayName, ToggleButton nonPublicVisibilityToggle, PropertyOrFieldGetter propertyOrFieldGetter, ItemsGetter itemsGetter, double fontSize = 14, bool isProperty = false, bool isField = false, bool isPrivate = false)
        {
            this.InitializeComponent();

            this.NonPublicVisibilityToggle = nonPublicVisibilityToggle;

            VectSharp.Font labelFont = new VectSharp.Font(Editor.OpenSansRegular, fontSize);
            VectSharp.Font codeFont = new VectSharp.Font(Editor.RobotoMonoRegular, fontSize);

            Canvas iconCanvas = new Canvas() { Width = 18, Height = 16 };

            Control nameControl;
            Control valueControl;

            if (variableType == VariableTypes.String)
            {
                string str = (string)vvariableValue;

                iconCanvas.Children.Add(new TypeIcons.StringIcon());

                string stringLiteral = str.Substring(0, Math.Min(str.Length, 30));

                if (str.Length > 30)
                {
                    stringLiteral += "…";
                }

                stringLiteral = stringLiteral.ToLiteral();

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.StringLiteral, stringLiteral) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, str);

                this.FindControl<Button>("InspectButton").IsVisible = true;

                this.FindControl<Button>("InspectButton").Click += async (s, e) =>
                {
                    Window window = new Window() { Width = 450, Height = 200 };
                    window.Title = "Inspect string";
                    window.Content = new TextBox() { Text = str, IsReadOnly = true, AcceptsReturn = true, FontSize = fontSize, FontFamily = FontFamily.Parse("resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Roboto Mono") };

                    await window.ShowDialog(this.FindAncestorOfType<Window>());
                };
            }
            else if (variableType == VariableTypes.Char)
            {
                char chr = ((string)vvariableValue)[0];

                string stringLiteral = chr.ToLiteral();

                iconCanvas.Children.Add(new TypeIcons.CharIcon());

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.StringLiteral, stringLiteral) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, chr);
            }
            else if (variableType == VariableTypes.Number)
            {
                iconCanvas.Children.Add(new TypeIcons.NumberIcon());

                string stringValue = (string)vvariableValue;

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.NumericLiteral, stringValue) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, stringValue);
            }
            else if (variableType == VariableTypes.Bool)
            {
                string bol = (string)vvariableValue;

                iconCanvas.Children.Add(new TypeIcons.BoolIcon());

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.Keyword, bol.ToString().ToLower()) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, bol.ToString().ToLower());
            }
            else if (variableType == VariableTypes.Null)
            {
                iconCanvas.Children.Add(new TypeIcons.NullIcon());

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.Keyword, "null") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, "null");
            }
            else if (variableType == VariableTypes.IEnumerable)
            {
                (string enumerableType, int count) = ((string enumerableType, int count))vvariableValue;

                iconCanvas.Children.Add(new TypeIcons.ArrayIcon());

                if (count >= 0)
                {
                    if (!string.IsNullOrEmpty(variableName))
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, count.ToString()), new TaggedText(TextTags.Operator, "]") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                    else
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Interface, "ICollection"), new TaggedText(TextTags.Operator, "<"), new TaggedText(TextTags.Class, enumerableType), new TaggedText(TextTags.Operator, ">"), new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, count.ToString()), new TaggedText(TextTags.Operator, "]") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(variableName))
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "[]") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                    else
                    {
                        valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Interface, "IEnumerable"), new TaggedText(TextTags.Operator, "<"), new TaggedText(TextTags.Class, enumerableType), new TaggedText(TextTags.Operator, ">") }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);
                    }
                }

                ToolTip.SetTip(this, "IEnumerable<" + enumerableType + ">");

                this.FindControl<Button>("ExpanderButton").IsVisible = true;

                bool expanderOpen = false;
                bool childrenInitialized = false;

                this.FindControl<Button>("ExpanderButton").Click += (s, e) =>
                {
                    if (expanderOpen)
                    {
                        this.FindControl<StackPanel>("ChildrenContainer").IsVisible = false;
                        this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M3.5,0.7 L3.5,11.3 L8.5,6 Z");
                        this.FindControl<Path>("ExpanderPath").Fill = Brushes.White;
                        expanderOpen = false;
                    }
                    else
                    {
                        if (!childrenInitialized)
                        {
                            InitializeChildren(variableId, propertyOrFieldGetter, itemsGetter);
                            childrenInitialized = true;
                        }

                        this.FindControl<StackPanel>("ChildrenContainer").IsVisible = true;
                        this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M2.5,9.5 L9.5,2.5 L9.5,9.5 Z");
                        this.FindControl<Path>("ExpanderPath").Fill = Brushes.Black;
                        expanderOpen = true;
                    }
                };
            }
            else if (variableType == VariableTypes.Enum)
            {
                (string typeName, string memberName) = ((string typeName, string memberName))vvariableValue;

                iconCanvas.Children.Add(new IntellisenseIcon.EnumIcon() { RenderTransform = new TranslateTransform(2, 2) });

                valueControl = FormattedText.FormatDescription(new TaggedText[] { new TaggedText(TextTags.Operator, "="), new TaggedText(TextTags.Space, " "), new TaggedText(TextTags.Enum, typeName), new TaggedText(TextTags.Punctuation, "."), new TaggedText(TextTags.EnumMember, memberName) }, null, labelFont, codeFont).Render(double.PositiveInfinity, false);

                ToolTip.SetTip(this, typeName + "." + memberName);
            }
            else
            {
                if (variableType == VariableTypes.Class)
                {
                    iconCanvas.Children.Add(new IntellisenseIcon.ClassIcon());
                }
                else if (variableType == VariableTypes.Delegate)
                {
                    iconCanvas.Children.Add(new IntellisenseIcon.DelegateIcon() { Margin = new Thickness(2, 2, 0, 0) });
                }
                else if (variableType == VariableTypes.Interface)
                {
                    iconCanvas.Children.Add(new IntellisenseIcon.InterfaceIcon());
                }
                else
                {
                    iconCanvas.Children.Add(new IntellisenseIcon.StructIcon() { Margin = new Thickness(1, 2, 0, 0) });
                }

                (string PropertyName, bool IsProperty, bool IsPrivate)[] propertyList = ((string PropertyName, bool IsProperty, bool IsPrivate)[])vvariableValue;


                valueControl = new Canvas();

                if (propertyList.Length > 0)
                {
                    this.FindControl<Button>("ExpanderButton").IsVisible = true;

                    bool expanderOpen = false;
                    bool childrenInitialized = false;

                    this.FindControl<Button>("ExpanderButton").Click += (s, e) =>
                    {
                        if (expanderOpen)
                        {
                            this.FindControl<StackPanel>("ChildrenContainer").IsVisible = false;
                            this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M3.5,0.7 L3.5,11.3 L8.5,6 Z");
                            this.FindControl<Path>("ExpanderPath").Fill = Brushes.White;
                            expanderOpen = false;
                        }
                        else
                        {
                            if (!childrenInitialized)
                            {
                                Dictionary<string, (bool, bool)> properties = new Dictionary<string, (bool, bool)>();
                                foreach ((string PropertyName, bool IsProperty, bool IsPrivate) in propertyList)
                                {
                                    if (!IsProperty)
                                    {
                                        try
                                        {
                                            properties.Add(PropertyName, (false, IsPrivate));
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            properties.Add(PropertyName, (true, IsPrivate));
                                        }
                                        catch { }
                                    }
                                }

                                InitializeChildren(variableId, properties, propertyOrFieldGetter, itemsGetter);
                                childrenInitialized = true;
                            }

                            this.FindControl<StackPanel>("ChildrenContainer").IsVisible = true;
                            this.FindControl<Path>("ExpanderPath").Data = Geometry.Parse("M2.5,9.5 L9.5,2.5 L9.5,9.5 Z");
                            this.FindControl<Path>("ExpanderPath").Fill = Brushes.Black;
                            expanderOpen = true;
                        }
                    };
                }
            }

            if (isProperty)
            {
                iconCanvas.Children.Clear();
                iconCanvas.Children.Add(new IntellisenseIcon.PropertyIcon());
            }

            if (isField)
            {
                iconCanvas.Children.Clear();
                iconCanvas.Children.Add(new IntellisenseIcon.FieldIcon());
            }

            if (isPrivate)
            {
                iconCanvas.Children.Add(new TypeIcons.PrivateIcon() { UseLayoutRounding = true });
                this.Bind<bool>(Grid.IsVisibleProperty, nonPublicVisibilityToggle.GetBindingObservable(ToggleButton.IsCheckedProperty).Select(x => x.Value.Value));
            }

            nameControl = FormattedText.FormatDescription(variableDisplayName, null, labelFont, codeFont).Render(double.PositiveInfinity, false, iconCanvas);

            valueControl.Margin = new Thickness(4, 0, 0, 0);



            this.FindControl<StackPanel>("VariableContainer").Children.Add(nameControl);
            this.FindControl<StackPanel>("VariableContainer").Children.Add(valueControl);
        }

        private void InitializeChildren(IEnumerable variableValue)
        {
            List<object> arrayContent = new List<object>();

            foreach (object obj in variableValue)
            {
                arrayContent.Add(obj);
            }

            if (arrayContent.Count <= 10)
            {
                for (int i = 0; i < arrayContent.Count; i++)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(null, arrayContent[i], new TaggedText[] { new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, i.ToString()), new TaggedText(TextTags.Operator, "]") }, NonPublicVisibilityToggle) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
            else
            {
                int depth = (int)Math.Floor(Math.Log10(arrayContent.Count - 1));

                int step = (int)Math.Pow(10, depth);

                for (int i = 0; i < arrayContent.Count; i += step)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(i, Math.Min(i + step - 1, arrayContent.Count - 1), arrayContent, NonPublicVisibilityToggle) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
        }

        private void InitializeChildren(string objectId, PropertyOrFieldGetter propertyOrFieldGetter, ItemsGetter itemsGetter)
        {
            (string itemId, VariableTypes itemType, object itemValue)[] arrayContent = itemsGetter(objectId);

            if (arrayContent.Length <= 10)
            {
                for (int i = 0; i < arrayContent.Length; i++)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(arrayContent[i].itemId, null, arrayContent[i].itemType, arrayContent[i].itemValue, new TaggedText[] { new TaggedText(TextTags.Operator, "["), new TaggedText(TextTags.NumericLiteral, i.ToString()), new TaggedText(TextTags.Operator, "]") }, NonPublicVisibilityToggle, propertyOrFieldGetter, itemsGetter) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
            else
            {
                int depth = (int)Math.Floor(Math.Log10(arrayContent.Length - 1));

                int step = (int)Math.Pow(10, depth);

                for (int i = 0; i < arrayContent.Length; i += step)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(objectId, i, Math.Min(i + step - 1, arrayContent.Length - 1), arrayContent, NonPublicVisibilityToggle, propertyOrFieldGetter, itemsGetter) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
        }


        private void InitializeChildren(Dictionary<string, (object, bool, bool)> properties)
        {
            if (properties.Count <= 10)
            {
                foreach (KeyValuePair<string, (object, bool, bool)> kvp in properties)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(null, kvp.Value.Item1, new TaggedText[] { new TaggedText(TextTags.Property, kvp.Key) }, NonPublicVisibilityToggle, isProperty: kvp.Value.Item2, isField: !kvp.Value.Item2, isPrivate: kvp.Value.Item3) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
            else
            {
                int depth = (int)Math.Floor(Math.Log10(properties.Count - 1));

                int step = (int)Math.Pow(10, depth);

                List<KeyValuePair<string, (object, bool, bool)>> propertyList = new List<KeyValuePair<string, (object, bool, bool)>>(properties);

                for (int i = 0; i < propertyList.Count; i += step)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(i, Math.Min(i + step - 1, propertyList.Count - 1), propertyList, NonPublicVisibilityToggle) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
        }

        private void InitializeChildren(string ownerObjectId, Dictionary<string, (bool, bool)> properties, PropertyOrFieldGetter propertyOrFieldGetter, ItemsGetter itemsGetter)
        {
            if (properties.Count <= 10)
            {
                foreach (KeyValuePair<string, (bool, bool)> kvp in properties)
                {
                    (string propertyId, VariableTypes propertyType, object propertyValue) = propertyOrFieldGetter(ownerObjectId, kvp.Key, kvp.Value.Item1);

                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new VariableExpander(propertyId, null, propertyType, propertyValue, new TaggedText[] { new TaggedText(TextTags.Property, kvp.Key) }, NonPublicVisibilityToggle, propertyOrFieldGetter, itemsGetter, isProperty: kvp.Value.Item1, isField: !kvp.Value.Item1, isPrivate: kvp.Value.Item2) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
            else
            {
                int depth = (int)Math.Floor(Math.Log10(properties.Count - 1));

                int step = (int)Math.Pow(10, depth);

                List<KeyValuePair<string, (bool, bool)>> propertyList = new List<KeyValuePair<string, (bool, bool)>>(properties);

                for (int i = 0; i < propertyList.Count; i += step)
                {
                    this.FindControl<StackPanel>("ChildrenContainer").Children.Add(new MultiItemContainer(ownerObjectId, i, Math.Min(i + step - 1, propertyList.Count - 1), propertyList, NonPublicVisibilityToggle, propertyOrFieldGetter, itemsGetter) { Margin = new Thickness(16, 0, 0, 0) });
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
