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

using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

namespace CSharpEditor
{
    internal class CSharpSourceEditorScrollBarMarker : Control
    {
        private CSharpSourceEditor Editor;

        public CSharpSourceEditorScrollBarMarker(CSharpSourceEditor editor)
        {
            this.Editor = editor;
            this.Width = 18;
            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            this.IsHitTestVisible = false;
        }

        private static IBrush CaretPositionBrush = new SolidColorBrush(Color.FromRgb(0, 0, 205));
        private static SolidColorBrush BreakpointBrush = new SolidColorBrush(Color.FromRgb(171, 97, 107));
        private static SolidColorBrush SearchBrush = new SolidColorBrush(Color.FromRgb(246, 185, 127));

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Editor.ShowScrollbarOverview && Editor.Extent.Height > Editor.Bounds.Height)
            {
                double totalHeight = this.Bounds.Height - 36;

                if (Editor.Extent.Width > Editor.Bounds.Width)
                {
                    totalHeight -= 18;
                }

                double totalLines = Editor.Text.Lines.Count;

                foreach (HighlightedLineRange range in this.Editor.HighlightedLines)
                {
                    context.FillRectangle(range.HighlightBrush, CSharpSourceEditorCaret.Round(new Avalonia.Rect(0, 18 + range.LineSpan.Start / totalLines * totalHeight, 4, (range.LineSpan.Length + 1) / totalLines * totalHeight), new Avalonia.Size(1, 1)));
                }

                HashSet<int> redLines = new HashSet<int>();
                HashSet<int> greenLines = new HashSet<int>();

                for (int i = 0; i < this.Editor.Markers.Count; i++)
                {
                    LinePositionSpan markerSpan = this.Editor.Text.Lines.GetLinePositionSpan(this.Editor.Markers[i].Span);
                    if (this.Editor.Markers[i].MarkerPen == ErrorContainer.WarningPen)
                    {
                        foreach (int j in Enumerable.Range(markerSpan.Start.Line, markerSpan.End.Line - markerSpan.Start.Line + 1))
                        {
                            greenLines.Add(j);
                        }
                    }
                    else
                    {
                        foreach (int j in Enumerable.Range(markerSpan.Start.Line, markerSpan.End.Line - markerSpan.Start.Line + 1))
                        {
                            redLines.Add(j);
                        }
                    }
                }


                List<int> sortedRedLines = new List<int>(redLines);
                List<int> sortedGreenLines = new List<int>(greenLines);

                sortedRedLines.Sort();
                sortedGreenLines.Sort();

                IEnumerable<HighlightedLineRange> greenLineSpans = from el in (from el in sortedGreenLines where !sortedRedLines.Contains(el) select new TextSpan(el, 0)).Join() select new HighlightedLineRange(el, Brushes.Green);
                IEnumerable<HighlightedLineRange> redLineSpans = from el in (from el in sortedRedLines select new TextSpan(el, 0)).Join() select new HighlightedLineRange(el, Brushes.Red);

                foreach (HighlightedLineRange range in greenLineSpans.Concat(redLineSpans))
                {
                    context.FillRectangle(range.HighlightBrush, CSharpSourceEditorCaret.Round(new Avalonia.Rect(14, 18 + range.LineSpan.Start / totalLines * totalHeight, 4, (range.LineSpan.Length + 1) / totalLines * totalHeight), new Avalonia.Size(1, 1)));
                }

                if (this.Editor.SearchReplace.IsVisible)
                {
                    HashSet<int> searchMatches = new HashSet<int>();

                    foreach (SearchSpan span in this.Editor.SearchSpans)
                    {
                        LinePositionSpan lineSpan = this.Editor.Text.Lines.GetLinePositionSpan(span.Span);
                        foreach (int j in Enumerable.Range(lineSpan.Start.Line, lineSpan.End.Line - lineSpan.Start.Line + 1))
                        {
                            searchMatches.Add(j);
                        }
                    }

                    foreach (int line in searchMatches)
                    {
                        context.FillRectangle(SearchBrush, CSharpSourceEditorCaret.Round(new Avalonia.Rect(7, 18 + line / totalLines * totalHeight, 4, 1.0 / totalLines * totalHeight), new Avalonia.Size(1, 1)));
                    }
                }

                foreach (int offset in this.Editor.Text.ToString().AllIndicesOf(Utils.BreakpointMarker))
                {
                    int line = this.Editor.Text.Lines.GetLinePosition(offset).Line;

                    context.FillRectangle(BreakpointBrush, CSharpSourceEditorCaret.Round(new Avalonia.Rect(7, 18 + line / totalLines * totalHeight, 4, 1.0 / totalLines * totalHeight), new Avalonia.Size(1, 1)));
                }

                context.FillRectangle(CaretPositionBrush, CSharpSourceEditorCaret.Round(new Avalonia.Rect(0, 18 + (Editor.Text.Lines.GetLinePosition(Editor.CaretOffset).Line + 0.5) / totalLines * totalHeight - 1, 18, 2), new Avalonia.Size(1, 1)));
            }
        }
    }
}
