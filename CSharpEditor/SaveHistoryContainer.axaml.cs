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
using Avalonia.Media;
using Avalonia.VisualTree;
using DiffPlex;
using DiffPlex.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CSharpEditor.DiagnosticIcons;
using VectSharp;
using VectSharp.Canvas;

namespace CSharpEditor
{
    internal partial class SaveHistoryContainer : UserControl
    {
        public SaveHistoryContainer()
        {
            this.InitializeComponent();
            this.FindControl<Button>("RefreshButton").Click += (s, e) =>
            {
                Refresh();
            };
            this.FindControl<Button>("SaveButton").Click += (s, e) =>
            {
                this.FindAncestorOfType<Editor>().Save();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        static Colour GreenColour = Colour.FromRgb(44, 190, 78);
        static Colour RedColour = Colour.FromRgb(203, 36, 49);
        static Colour GreyColour = Colour.FromRgb(209, 213, 218);

        public void Refresh()
        {
            this.FindControl<StackPanel>("FileContainer").Children.Clear();
            Editor editor = this.FindAncestorOfType<Editor>();

            string autosaveDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly().GetName().Name);
            string savePath = System.IO.Path.Combine(autosaveDirectory, editor.Guid);

            string[] files;

            try
            {
                files = System.IO.Directory.GetFiles(savePath, "*.cs");
            }
            catch
            {
                files = new string[0];
            }
            

            long result = -1;

            List<(long, string)> sortedFiles = (from el in files let filename = System.IO.Path.GetFileNameWithoutExtension(el) where filename.StartsWith("autosave") || long.TryParse(filename, out result) let result2 = result orderby !filename.StartsWith("autosave") ? result2 : ((DateTimeOffset)new System.IO.FileInfo(el).LastWriteTimeUtc).ToUnixTimeSeconds() descending select !filename.StartsWith("autosave") ? (result2, el) : (((DateTimeOffset)new System.IO.FileInfo(el).LastWriteTimeUtc).ToUnixTimeSeconds(), el)).ToList();

            sortedFiles.Add((editor.OriginalTimeStamp, "original"));
            sortedFiles.Sort((a, b) => Math.Sign(b.Item1 - a.Item1));

            int diffWidth = (int)Math.Max(10, Math.Floor(this.Bounds.Width - 32 - 150 - 32 - 32 - 22));

            string currentText = null;
            int lineCount = -1;
            Differ differ = new Differ();

            if (sortedFiles.Any())
            {
                currentText = editor.EditorControl.Text.ToString();
                lineCount = editor.EditorControl.Text.Lines.Count;
            }

            foreach ((long timestamp, string file) file in sortedFiles)
            {
                Grid itemGrid = new Grid();

                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(32, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(150, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(36, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(36, GridUnitType.Pixel));
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition(18, GridUnitType.Pixel));

                if (System.IO.Path.GetFileNameWithoutExtension(file.file).StartsWith("autosave"))
                {
                    AutosaveIcon icon = new DiagnosticIcons.AutosaveIcon();
                    ToolTip.SetTip(icon, "Autosave");
                    itemGrid.Children.Add(icon);
                }
                else if (file.file == "original")
                {
                    StartingIcon icon = new DiagnosticIcons.StartingIcon();
                    ToolTip.SetTip(icon, "Original file");
                    itemGrid.Children.Add(icon);
                }
                else
                {
                    SaveIcon icon = new DiagnosticIcons.SaveIcon();
                    ToolTip.SetTip(icon, "Manual save");
                    itemGrid.Children.Add(icon);
                }

                TextBlock timeBlock = new TextBlock() { Text = DateTimeOffset.FromUnixTimeSeconds(file.timestamp).DateTime.ToLocalTime().ToString("HH:mm:ss \\(dd MMM\\)", System.Globalization.CultureInfo.InvariantCulture), Margin = new Thickness(5, 2, 5, 2), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                Grid.SetColumn(timeBlock, 2);
                itemGrid.Children.Add(timeBlock);

                DiffResult diffResult;

                if (file.file != "original")
                {
                    diffResult = differ.CreateLineDiffs(currentText, System.IO.File.ReadAllText(file.file), false);
                }
                else
                {
                    diffResult = differ.CreateLineDiffs(currentText, editor.OriginalText, false);
                }


                List<(int pos, int count, Colour colour)> diffLines = new List<(int pos, int count, Colour colour)>(from el in diffResult.DiffBlocks let pos = el.DeleteCountA > 0 ? el.DeleteStartA : el.InsertStartB orderby pos ascending select (pos, el.DeleteCountA > 0 ? el.DeleteCountA : el.InsertCountB, el.DeleteCountA > 0 ? RedColour : GreenColour));

                int currPos = 0;

                List<(double width, Colour colour)> diffBlocks = new List<(double width, Colour colour)>();

                for (int i = 0; i < diffLines.Count; i++)
                {
                    if (diffLines[i].pos > currPos)
                    {
                        diffBlocks.Add((diffLines[i].pos - currPos, GreyColour));
                        currPos = diffLines[i].pos;
                    }

                    diffBlocks.Add((diffLines[i].count, diffLines[i].colour));
                    currPos += diffLines[i].count;
                }

                if (currPos < lineCount)
                {
                    diffBlocks.Add((lineCount - currPos, GreyColour));
                    currPos = lineCount;
                }

                Page diff = new Page(diffWidth, 20);
                Graphics gpr = diff.Graphics;

                double currX = 0;

                for (int i = 0; i < diffBlocks.Count; i++)
                {
                    double w = diffBlocks[i].width * diffWidth / currPos;
                    gpr.FillRectangle(currX, 0, w, 20, diffBlocks[i].colour);
                    currX += w;
                }

                Viewbox diffBox = new Viewbox() { Child = diff.PaintToCanvas(false), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Height = 20, Stretch = Stretch.Fill };
                Grid.SetColumn(diffBox, 4);
                itemGrid.Children.Add(diffBox);

                if (file.file != "original")
                {
                    Button restoreButton = new Button() { Content = new DiagnosticIcons.RestoreIcon(), Margin = new Thickness(3) };
                    Grid.SetColumn(restoreButton, 6);
                    ToolTip.SetTip(restoreButton, "Restore");
                    itemGrid.Children.Add(restoreButton);

                    Button deleteButton = new Button() { Content = new DiagnosticIcons.DeleteIcon(), Margin = new Thickness(3) };
                    Grid.SetColumn(deleteButton, 8);
                    ToolTip.SetTip(deleteButton, "Delete");
                    itemGrid.Children.Add(deleteButton);

                    restoreButton.Click += async (s, e) =>
                    {
                        try
                        {
                            string newText = System.IO.File.ReadAllText(file.file);
                            await editor.EditorControl.SetText(newText);

                            if (editor.EditorControl.ShowLineChanges)
                            {
                                DiffResult diffResultFromLastSaved = editor.Differ.CreateLineDiffs(editor.PreSource + "\n" + editor.LastSavedText + "\n" + editor.PostSource, editor.PreSource + "\n" + newText + "\n" + editor.PostSource, false);
                                IEnumerable<int> changesFromLastSaved = (from el in diffResultFromLastSaved.DiffBlocks select Enumerable.Range(el.InsertStartB - editor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                                DiffResult diffResultFromOriginal = editor.Differ.CreateLineDiffs(editor.PreSource + "\n" + editor.OriginalText + "\n" + editor.PostSource, editor.PreSource + "\n" + newText + "\n" + editor.PostSource, false);
                                IEnumerable<int> changesFromOriginal = (from el in diffResultFromOriginal.DiffBlocks select Enumerable.Range(el.InsertStartB - editor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                                editor.SetLineDiff(changesFromLastSaved, changesFromOriginal);
                            }

                        }
                        catch { }

                        Refresh();
                    };

                    deleteButton.Click += (s, e) =>
                    {
                        try
                        {
                            System.IO.File.Delete(file.file);
                        }
                        catch { }

                        Refresh();
                    };
                }
                else
                {
                    Button restoreButton = new Button() { Content = new DiagnosticIcons.RestoreIcon(), Margin = new Thickness(3) };
                    Grid.SetColumn(restoreButton, 6);
                    ToolTip.SetTip(restoreButton, "Restore");
                    itemGrid.Children.Add(restoreButton);

                    restoreButton.Click += async (s, e) =>
                    {
                        string newText = editor.OriginalText;
                        await editor.EditorControl.SetText(newText);

                        if (editor.EditorControl.ShowLineChanges)
                        {
                            DiffResult diffResultFromLastSaved = editor.Differ.CreateLineDiffs(editor.PreSource + "\n" + editor.LastSavedText + "\n" + editor.PostSource, editor.PreSource + "\n" + newText + "\n" + editor.PostSource, false);
                            IEnumerable<int> changesFromLastSaved = (from el in diffResultFromLastSaved.DiffBlocks select Enumerable.Range(el.InsertStartB - editor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                            DiffResult diffResultFromOriginal = editor.Differ.CreateLineDiffs(editor.PreSource + "\n" + editor.OriginalText + "\n" + editor.PostSource, editor.PreSource + "\n" + newText + "\n" + editor.PostSource, false);
                            IEnumerable<int> changesFromOriginal = (from el in diffResultFromOriginal.DiffBlocks select Enumerable.Range(el.InsertStartB - editor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                            editor.SetLineDiff(changesFromLastSaved, changesFromOriginal);
                        }

                        Refresh();
                    };
                }

                this.FindControl<StackPanel>("FileContainer").Children.Add(itemGrid);
            }
        }
    }
}
