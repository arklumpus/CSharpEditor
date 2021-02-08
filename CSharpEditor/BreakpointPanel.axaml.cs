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
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpEditor
{
    internal class BreakpointPanel : UserControl
    {
        public BreakpointPanel()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<Button>("ResumeButton").Click += (s, e) =>
            {
                ResumeClicked?.Invoke(this, e);
            };
        }

        public bool IgnoreFurtherOccurrences => this.FindControl<ToggleButton>("IgnoreFurtherOccurrences").IsChecked == true;

        public event EventHandler<EventArgs> ResumeClicked;

        internal void InvokeResumeClicked()
        {
            ResumeClicked?.Invoke(this, new EventArgs());
        }

        public void SetContent(BreakpointInfo breakpointInfo)
        {
            this.FindControl<StackPanel>("LocalVariablesContainer").Children.Clear();
            this.FindControl<ToggleButton>("IgnoreFurtherOccurrences").IsChecked = false;

            foreach (KeyValuePair<string, object> kvp in (from el in breakpointInfo.LocalVariables orderby el.Key ascending select el))
            {
                this.FindControl<StackPanel>("LocalVariablesContainer").Children.Add(new VariableExpander(kvp.Key, kvp.Value, breakpointInfo.LocalVariableDisplayParts[kvp.Key], this.FindControl<ToggleButton>("ToggleNonPublicVisibility")) { Margin = new Thickness(0, 0, 5, 0) });
            }
        }

        public void SetContent(RemoteBreakpointInfo breakpointInfo)
        {
            this.FindControl<StackPanel>("LocalVariablesContainer").Children.Clear();
            this.FindControl<ToggleButton>("IgnoreFurtherOccurrences").IsChecked = false;

            foreach (KeyValuePair<string, (string, VariableTypes, object)> kvp in (from el in breakpointInfo.LocalVariables orderby el.Key ascending select el))
            {
                this.FindControl<StackPanel>("LocalVariablesContainer").Children.Add(new VariableExpander(kvp.Value.Item1, kvp.Key, kvp.Value.Item2, kvp.Value.Item3, breakpointInfo.LocalVariableDisplayParts[kvp.Key], this.FindControl<ToggleButton>("ToggleNonPublicVisibility"), breakpointInfo.PropertyOrFieldGetter, breakpointInfo.ItemsGetter) { Margin = new Thickness(0, 0, 5, 0) });
            }
        }
    }
}
