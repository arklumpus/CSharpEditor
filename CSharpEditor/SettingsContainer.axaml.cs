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
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using DiffPlex.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CSharpEditor
{
    internal partial class SettingsContainer : UserControl
    {
        private Editor Editor;

        public SettingsContainer()
        {
            this.InitializeComponent();
        }

        public SettingsContainer(Shortcut[] additionalShortcuts)
        {
            this.InitializeComponent();

            this.AttachedToVisualTree += (s, e) =>
            {
                Editor = this.FindAncestorOfType<Editor>();

                string autosaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly().GetName().Name, Editor.Guid);

                this.FindControl<TextBox>("SaveDirectoryBox").Text = autosaveDirectory;

                this.FindControl<NumericUpDown>("AutosaveIntervalBox").Value = Editor.AutoSaver.MillisecondsInterval / 1000;

                this.FindControl<NumericUpDown>("CompilationTimeoutBox").Value = Editor.CompilationErrorChecker.MillisecondsInterval;

                this.Editor.LoadSettings();
            };

            this.FindControl<Button>("CopySaveDirectoryButton").Click += async (s, e) =>
            {
                await TopLevel.GetTopLevel(this).Clipboard.SetTextAsync(this.FindControl<TextBox>("SaveDirectoryBox").Text);
            };

            this.FindControl<NumericUpDown>("AutosaveIntervalBox").ValueChanged += (s, e) =>
            {
                int newInterval = (int)Math.Round(this.FindControl<NumericUpDown>("AutosaveIntervalBox").Value.Value) * 1000;

                if (newInterval > 0 && Editor.AutoSaver.IsRunning)
                {
                    Editor.AutoSaver.Stop();
                    Editor.AutoSaver.MillisecondsInterval = newInterval;
                    Editor.AutoSaver.Resume();
                }
                else if (newInterval > 0 && !Editor.AutoSaver.IsRunning)
                {
                    Editor.AutoSaver.Stop();
                    Editor.AutoSaver.MillisecondsInterval = newInterval;
                    Editor.AutoSaver.Resume();
                }
                else if (newInterval == 0 && Editor.AutoSaver.IsRunning)
                {
                    Editor.AutoSaver.Stop();
                    Editor.AutoSaver.MillisecondsInterval = newInterval;
                }
            };

            this.FindControl<CheckBox>("KeepSaveHistoryBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == CheckBox.IsCheckedProperty)
                {
                    Editor.KeepSaveHistory = this.FindControl<CheckBox>("KeepSaveHistoryBox").IsChecked == true;
                }
            };

            this.FindControl<Button>("DeleteSaveHistoryButton").Click += (s, e) =>
            {
                string autosaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly().GetName().Name, Editor.Guid);
                try
                {
                    foreach (string file in Directory.EnumerateFiles(autosaveDirectory, "*.cs"))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }
                }
                catch { }

            };

            this.FindControl<ComboBox>("SyntaxHighlightingModeBox").SelectionChanged += (s, e) =>
            {
                Editor.EditorControl.SyntaxHighlightingMode = (SyntaxHighlightingModes)this.FindControl<ComboBox>("SyntaxHighlightingModeBox").SelectedIndex;
            };

            this.FindControl<CheckBox>("OpenSuggestionsBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == CheckBox.IsCheckedProperty)
                {
                    Editor.AutoOpenSuggestions = this.FindControl<CheckBox>("OpenSuggestionsBox").IsChecked == true;
                }
            };

            this.FindControl<CheckBox>("OpenParametersBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == CheckBox.IsCheckedProperty)
                {
                    Editor.AutoOpenParameters = this.FindControl<CheckBox>("OpenParametersBox").IsChecked == true;
                }
            };

            this.FindControl<CheckBox>("AutoFormatBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == CheckBox.IsCheckedProperty)
                {
                    Editor.AutoFormat = this.FindControl<CheckBox>("AutoFormatBox").IsChecked == true;
                }
            };

            this.FindControl<NumericUpDown>("CompilationTimeoutBox").ValueChanged += (s, e) =>
            {
                int newInterval = (int)Math.Round(this.FindControl<NumericUpDown>("CompilationTimeoutBox").Value.Value);

                if (newInterval > 0 && Editor.CompilationErrorChecker.IsRunning)
                {
                    Editor.CompilationErrorChecker.Stop();
                    Editor.CompilationErrorChecker.MillisecondsInterval = newInterval;
                    Editor.CompilationErrorChecker.Resume();
                }
                else if (newInterval > 0 && !Editor.CompilationErrorChecker.IsRunning)
                {
                    Editor.CompilationErrorChecker.Stop();
                    Editor.CompilationErrorChecker.MillisecondsInterval = newInterval;
                    Editor.CompilationErrorChecker.Resume();
                }
                else if (newInterval == 0 && Editor.CompilationErrorChecker.IsRunning)
                {
                    Editor.CompilationErrorChecker.Stop();
                    Editor.CompilationErrorChecker.MillisecondsInterval = newInterval;
                }
            };

            this.FindControl<CheckBox>("ShowChangedLinesBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == CheckBox.IsCheckedProperty)
                {
                    Editor.EditorControl.ShowLineChanges = this.FindControl<CheckBox>("ShowChangedLinesBox").IsChecked == true;

                    if (Editor.EditorControl.ShowLineChanges)
                    {
                        DiffResult diffResultFromLastSaved = Editor.Differ.CreateLineDiffs(Editor.PreSource + "\n" + Editor.LastSavedText + "\n" + Editor.PostSource, Editor.FullSource, false);
                        IEnumerable<int> changesFromLastSaved = (from el in diffResultFromLastSaved.DiffBlocks select Enumerable.Range(el.InsertStartB - Editor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                        DiffResult diffResultFromOriginal = Editor.Differ.CreateLineDiffs(Editor.PreSource + "\n" + Editor.OriginalText + "\n" + Editor.PostSource, Editor.FullSource, false);
                        IEnumerable<int> changesFromOriginal = (from el in diffResultFromOriginal.DiffBlocks select Enumerable.Range(el.InsertStartB - Editor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                        Editor.SetLineDiff(changesFromLastSaved, changesFromOriginal);
                    }
                }
            };

            this.FindControl<CheckBox>("ShowScrollbarOverviewBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == CheckBox.IsCheckedProperty)
                {
                    Editor.EditorControl.ShowScrollbarOverview = this.FindControl<CheckBox>("ShowScrollbarOverviewBox").IsChecked == true;
                }
            };

            this.FindControl<Button>("SaveSettingsButton").Click += (s, e) =>
            {
                this.Editor.SaveSettings();
            };

            this.FindControl<Button>("LoadSettingsButton").Click += (s, e) =>
            {
                this.Editor.LoadSettings();
            };

            if (additionalShortcuts.Length > 0)
            {
                int additionalShortcutColumns = (int)Math.Ceiling((double)additionalShortcuts.Length / 11);

                ColumnDefinitions definitions = this.FindControl<Grid>("ShortcutGridContainer").ColumnDefinitions;

                for (int i = 0; i < additionalShortcutColumns; i++)
                {
                    this.FindControl<Grid>("ShortcutGridContainer").ColumnDefinitions.Insert(this.FindControl<Grid>("ShortcutGridContainer").ColumnDefinitions.Count - 1, new ColumnDefinition(30, GridUnitType.Pixel));
                    this.FindControl<Grid>("ShortcutGridContainer").ColumnDefinitions.Insert(this.FindControl<Grid>("ShortcutGridContainer").ColumnDefinitions.Count - 1, new ColumnDefinition(0, GridUnitType.Auto));
                }

                int itemsPerColumn = (int)Math.Ceiling((double)additionalShortcuts.Length / additionalShortcutColumns);

                int column = 0;
                int row = 0;

                Grid currentColumn = new Grid() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                Grid.SetColumn(currentColumn, 5 + 2 * column);
                currentColumn.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Auto));
                currentColumn.ColumnDefinitions.Add(new ColumnDefinition(10, GridUnitType.Pixel));
                currentColumn.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                this.FindControl<Grid>("ShortcutGridContainer").Children.Add(currentColumn);

                for (int i = 0; i < additionalShortcuts.Length; i++)
                {
                    currentColumn.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Auto));

                    TextBlock descriptionBlock = new TextBlock() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Text = additionalShortcuts[i].Name };
                    Grid.SetRow(descriptionBlock, row);
                    currentColumn.Children.Add(descriptionBlock);

                    StackPanel keyPanel = new StackPanel() { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(5) };
                    Grid.SetColumn(keyPanel, 2);
                    Grid.SetRow(keyPanel, row);

                    for (int j = 0; j < additionalShortcuts[i].Shortcuts.Length; j++)
                    {
                        for (int k = 0; k < additionalShortcuts[i].Shortcuts[j].Length; k++)
                        {
                            keyPanel.Children.Add(new DiagnosticIcons.KeyIcon() { KeyText = additionalShortcuts[i].Shortcuts[j][k], Margin = k > 0 ? new Thickness(5, 0, 0, 0) : new Thickness(0) });
                        }

                        if (j < additionalShortcuts[i].Shortcuts.Length - 1)
                        {
                            keyPanel.Children.Add(new TextBlock() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Text = "or", Margin = new Thickness(10, 0, 10, 0) });
                        }
                    }

                    currentColumn.Children.Add(keyPanel);

                    row++;

                    if (row == itemsPerColumn)
                    {
                        row = 0;
                        column++;
                        currentColumn = new Grid() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        Grid.SetColumn(currentColumn, 5 + 2 * column);
                        currentColumn.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Auto));
                        currentColumn.ColumnDefinitions.Add(new ColumnDefinition(10, GridUnitType.Pixel));
                        currentColumn.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                        this.FindControl<Grid>("ShortcutGridContainer").Children.Add(currentColumn);
                    }
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    internal class SavedSettings
    {
        public int AutosaveInterval { get; set; }
        public bool KeepSaveHistory { get; set; }
        public SyntaxHighlightingModes SyntaxHighlightingMode { get; set; }
        public bool AutoOpenSuggestionts { get; set; }
        public bool AutoOpenParameters { get; set; }
        public bool AutoFormat { get; set; }
        
        public int CompilationTimeout { get; set; }
        public bool ShowChangedLines { get; set; }
        public bool ShowScrollbarOverview { get; set; }

    }
}
