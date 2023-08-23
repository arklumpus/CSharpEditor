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
using Avalonia.VisualTree;

namespace CSharpEditor
{
    internal partial class StatusBar : UserControl
    {
        private int _errorCount = 0;
        public int ErrorCount
        {
            set
            {
                _errorCount = value;
                
                this.FindControl<TextBlock>("ErrorCountBox").Text = value.ToString();

                if (ErrorCount == 0 && WarningCount == 0 && InfoCount == 0)
                {
                    this.FindControl<StackPanel>("DiagnosticSummaryPanel").IsVisible = false;
                    this.FindControl<StackPanel>("NoProblemsPanel").IsVisible = true;
                }
                else
                {
                    this.FindControl<StackPanel>("DiagnosticSummaryPanel").IsVisible = true;
                    this.FindControl<StackPanel>("NoProblemsPanel").IsVisible = false;
                }
            }

            private get
            {
                return _errorCount;
            }
        }

        private int _warningCount = 0;
        public int WarningCount
        {
            set
            {
                _warningCount = value;

                this.FindControl<TextBlock>("WarningCountBox").Text = value.ToString();

                if (ErrorCount == 0 && WarningCount == 0 && InfoCount == 0)
                {
                    this.FindControl<StackPanel>("DiagnosticSummaryPanel").IsVisible = false;
                    this.FindControl<StackPanel>("NoProblemsPanel").IsVisible = true;
                }
                else
                {
                    this.FindControl<StackPanel>("DiagnosticSummaryPanel").IsVisible = true;
                    this.FindControl<StackPanel>("NoProblemsPanel").IsVisible = false;
                }
            }

            private get
            {
                return _warningCount;
            }
        }

        private int _infoCount = 0;
        public int InfoCount
        {
            set
            {
                _infoCount = value;

                this.FindControl<TextBlock>("MessageCount").Text = value.ToString();

                if (ErrorCount == 0 && WarningCount == 0 && InfoCount == 0)
                {
                    this.FindControl<StackPanel>("DiagnosticSummaryPanel").IsVisible = false;
                    this.FindControl<StackPanel>("NoProblemsPanel").IsVisible = true;
                }
                else
                {
                    this.FindControl<StackPanel>("DiagnosticSummaryPanel").IsVisible = true;
                    this.FindControl<StackPanel>("NoProblemsPanel").IsVisible = false;
                }
            }

            private get
            {
                return _infoCount;
            }
        }

        public StatusBar()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<ComboBox>("FontSizeBox").SelectionChanged += FontSizeChanged;
            this.FindControl<ToggleButton>("ToggleErrorContainerButton").PropertyChanged += ToggleErrorContainerButtonChanged;
            this.FindControl<ToggleButton>("ToggleReferencesContainerButton").PropertyChanged += ToggleReferencesContainerButtonChanged;
            this.FindControl<ToggleButton>("ToggleSettingsContainerButton").PropertyChanged += ToggleSettingsContainerButtonButtonChanged;
            this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").PropertyChanged += ToggleSaveHistoryContainerButtonChanged;
        }

        static readonly double[] FontSizes = new double[] { 8, 9, 10, 11, 12, 14, 16, 20, 24, 30, 36 };

        private void FontSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            this.FindAncestorOfType<Editor>().FontSize = FontSizes[this.FindControl<ComboBox>("FontSizeBox").SelectedIndex];
        }

        public void IncreaseFontSize()
        {
            if (this.FindControl<ComboBox>("FontSizeBox").SelectedIndex < this.FindControl<ComboBox>("FontSizeBox").ItemCount - 1)
            {
                this.FindControl<ComboBox>("FontSizeBox").SelectedIndex++;
            }
        }

        public void DecreaseFontSize()
        {
            if (this.FindControl<ComboBox>("FontSizeBox").SelectedIndex > 0)
            {
                this.FindControl<ComboBox>("FontSizeBox").SelectedIndex--;
            }
        }

        private void ToggleErrorContainerButtonChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                bool isChecked = this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked == true;

                this.FindAncestorOfType<Editor>().ErrorContainer.IsVisible = isChecked;

                if (isChecked)
                {
                    this.FindAncestorOfType<Editor>().OpenBottomPanel();
                    this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked = false;
                }
                else if (!(this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true))
                {
                    this.FindAncestorOfType<Editor>().CloseBottomPanel();
                }
            }
        }

        private void ToggleReferencesContainerButtonChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                bool isChecked = this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked == true;

                this.FindAncestorOfType<Editor>().ReferencesContainer.IsVisible = isChecked;

                if (isChecked)
                {
                    this.FindAncestorOfType<Editor>().OpenBottomPanel();
                    this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked = false;
                }
                else if (!(this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true))
                {
                    this.FindAncestorOfType<Editor>().CloseBottomPanel();
                }
            }
        }

        private void ToggleSettingsContainerButtonButtonChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                bool isChecked = this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true;

                this.FindAncestorOfType<Editor>().SettingsContainer.IsVisible = isChecked;

                if (isChecked)
                {
                    this.FindAncestorOfType<Editor>().OpenBottomPanel();
                    this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked = false;
                }
                else if (!(this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true))
                {
                    this.FindAncestorOfType<Editor>().CloseBottomPanel();
                }
            }
        }

        private void ToggleSaveHistoryContainerButtonChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                bool isChecked = this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true;

                this.FindAncestorOfType<Editor>().SaveHistoryContainer.IsVisible = isChecked;

                if (isChecked)
                {
                    this.FindAncestorOfType<Editor>().SaveHistoryContainer.Refresh();
                    this.FindAncestorOfType<Editor>().OpenBottomPanel();
                    this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked = false;
                    this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked = false;
                }
                else if (!(this.FindControl<ToggleButton>("ToggleErrorContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true))
                {
                    this.FindAncestorOfType<Editor>().CloseBottomPanel();
                }
            }
        }
    }
}
