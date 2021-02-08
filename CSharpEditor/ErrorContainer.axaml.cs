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
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive.Linq;

namespace CSharpEditor
{
    internal class ErrorContainer : UserControl
    {
        public ErrorContainer()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        internal static Pen ErrorPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 72, 72)));
        internal static Pen WarningPen = new Pen(new SolidColorBrush(Color.FromRgb(64, 160, 64)));
        public void SetContent(SourceText source, IEnumerable<Diagnostic> diagnostics, int linesToIgnore)
        {
            this.FindControl<StackPanel>("ErrorContainer").Children.Clear();

            int errors = 0;
            int warnings = 0;
            int infos = 0;

            ToggleButton errorButton = this.FindControl<ToggleButton>("ErrorButton");
            ToggleButton warningButton = this.FindControl<ToggleButton>("WarningButton");
            ToggleButton messageButton = this.FindControl<ToggleButton>("MessageButton");
            Editor editor = this.FindAncestorOfType<Editor>();
            //editor.ErrorMarkerService.RemoveAll(m => true);

            List<(TextSpan, Diagnostic)> errorSpans = new List<(TextSpan, Diagnostic)>();

            List<(TextSpan, Diagnostic)> warningSpans = new List<(TextSpan, Diagnostic)>();

            foreach (Diagnostic diag in diagnostics)
            {
                if (diag.Severity != DiagnosticSeverity.Hidden)
                {
                    TextSpan? span = null;
                    string lineNum = "";

                    try
                    {
                        LinePosition startPos = diag.Location.GetLineSpan().StartLinePosition;
                        LinePosition endPos = diag.Location.GetLineSpan().EndLinePosition;

                        LinePosition correctedStartPos = new LinePosition(startPos.Line - linesToIgnore, startPos.Character);
                        LinePosition correctedEndPos = new LinePosition(endPos.Line - linesToIgnore, endPos.Character);
                        span = source.Lines.GetTextSpan(new LinePositionSpan(correctedStartPos, correctedEndPos));

                        lineNum = (diag.Location.GetLineSpan().StartLinePosition.Line - linesToIgnore + 1).ToString();
                    }
                    catch { }

                    Grid errorGrid = new Grid();

                    errorGrid.Classes.Add("ErrorLine");

                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(32, GridUnitType.Pixel));
                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(64, GridUnitType.Pixel));
                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(64, GridUnitType.Pixel));
                    errorGrid.ColumnDefinitions.Add(new ColumnDefinition(18, GridUnitType.Pixel));

                    switch (diag.Severity)
                    {
                        case DiagnosticSeverity.Error:
                            errorGrid.Children.Add(new DiagnosticIcons.ErrorIcon());
                            errorGrid.Bind<bool>(Grid.IsVisibleProperty, errorButton.GetBindingObservable(ToggleButton.IsCheckedProperty).Select(x => x.Value.Value));
                            errors++;
                            if (span != null)
                            {
                                errorSpans.Add((new TextSpan(span.Value.Start, span.Value.Length), diag));
                            }

                            break;
                        case DiagnosticSeverity.Info:
                            errorGrid.Children.Add(new DiagnosticIcons.InfoIcon());
                            errorGrid.Bind<bool>(Grid.IsVisibleProperty, messageButton.GetBindingObservable(ToggleButton.IsCheckedProperty).Select(x => x.Value.Value));
                            infos++;
                            break;
                        case DiagnosticSeverity.Warning:
                            errorGrid.Children.Add(new DiagnosticIcons.WarningIcon());
                            errorGrid.Bind<bool>(Grid.IsVisibleProperty, warningButton.GetBindingObservable(ToggleButton.IsCheckedProperty).Select(x => x.Value.Value));
                            warnings++;
                            if (span != null)
                            {
                                warningSpans.Add((new TextSpan(span.Value.Start, span.Value.Length), diag));
                            }
                            break;
                    }

                    TextBlock codeBlock = new TextBlock() { Text = diag.Id, Margin = new Thickness(5, 2, 5, 2), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                    Grid.SetColumn(codeBlock, 2);
                    errorGrid.Children.Add(codeBlock);

                    TextBlock descriptionBlock = new TextBlock() { Text = diag.GetMessage(System.Globalization.CultureInfo.InvariantCulture), Margin = new Thickness(5, 2, 5, 2), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
                    Grid.SetColumn(descriptionBlock, 4);
                    errorGrid.Children.Add(descriptionBlock);

                    TextBlock lineBlock = new TextBlock() { Text = lineNum, Margin = new Thickness(5, 2, 5, 2), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                    Grid.SetColumn(lineBlock, 6);
                    errorGrid.Children.Add(lineBlock);

                    this.FindControl<StackPanel>("ErrorContainer").Children.Add(errorGrid);

                    errorGrid.PointerPressed += (s, e) =>
                    {
                        if (span != null)
                        {
                            editor.EditorControl.SetSelection(span.Value.Start, span.Value.Length);
                        }
                    };
                }
            }

            List<MarkerRange> markers = new List<MarkerRange>();

            foreach ((TextSpan, List<Diagnostic>) span in errorSpans.Join())
            {
                /*try
                {
                    ITextMarker marker = editor.ErrorMarkerService.Create(span.Start, span.Length);
                    marker.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
                    marker.MarkerColor = Color.FromRgb(255, 72, 72);
                }
                catch { }*/

                markers.Add(new MarkerRange(span.Item1, ErrorPen, span.Item2));
            }

            foreach ((TextSpan, List<Diagnostic>) span in warningSpans.Join())
            {
                /*try
                {
                    ITextMarker marker = editor.ErrorMarkerService.Create(span.Start, span.Length);
                    marker.MarkerTypes = TextMarkerTypes.SquigglyUnderline;
                    marker.MarkerColor = Color.FromRgb(64, 160, 64);
                }
                catch { }*/

                markers.Add(new MarkerRange(span.Item1, WarningPen, span.Item2));
            }

            if (editor != null)
            {
                editor.EditorControl.Markers = ImmutableList.Create(markers.ToArray());

                editor.StatusBar.ErrorCount = errors;
                editor.StatusBar.WarningCount = warnings;
                editor.StatusBar.InfoCount = infos;

                this.FindControl<TextBlock>("ErrorText").Text = errors.ToString() + " Error" + (errors == 1 ? "" : "s");
                this.FindControl<TextBlock>("WarningText").Text = warnings.ToString() + " Warning" + (warnings == 1 ? "" : "s");
                this.FindControl<TextBlock>("MessageText").Text = infos.ToString() + " Message" + (infos == 1 ? "" : "s");
            }
        }
    }
}
