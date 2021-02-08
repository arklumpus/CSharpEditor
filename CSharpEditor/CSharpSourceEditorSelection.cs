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
using Avalonia.Input;
using Avalonia.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;

namespace CSharpEditor
{
    internal class CSharpSourceEditorSelection : Control, ILogicalScrollable
    {
        private CSharpSourceEditor Editor { get; }

        internal CSharpSourceEditorSelection(CSharpSourceEditor editor)
        {
            this.Editor = editor;
            this.IsHitTestVisible = false;
        }

        private bool _visible = true;

        private void CaretTimerTick(object sender, EventArgs e)
        {
            _visible = !_visible;
            this.InvalidateVisual();
        }

        public bool CanHorizontallyScroll { get => ((ILogicalScrollable)Editor).CanHorizontallyScroll; set { } }
        public bool CanVerticallyScroll { get => ((ILogicalScrollable)Editor).CanVerticallyScroll; set { } }

        public bool IsLogicalScrollEnabled => ((ILogicalScrollable)Editor).IsLogicalScrollEnabled;

        public Size ScrollSize => ((ILogicalScrollable)Editor).ScrollSize;

        public Size PageScrollSize => ((ILogicalScrollable)Editor).PageScrollSize;

        public Size Extent => ((IScrollable)Editor).Extent;

        public Vector Offset { get => ((IScrollable)Editor).Offset; set { } }

        public Size Viewport => ((IScrollable)Editor).Viewport;

        public event EventHandler ScrollInvalidated
        {
            add
            {

            }

            remove
            {

            }
        }

        public bool BringIntoView(IControl target, Rect targetRect)
        {
            return false;
        }

        public IControl GetControlInDirection(NavigationDirection direction, IControl from)
        {
            return null;
        }

        public void RaiseScrollInvalidated(EventArgs e)
        {

        }

        private static IBrush SearchBrush = new SolidColorBrush(Color.FromArgb(128, 237, 115, 0));

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double lineHeight = this.Editor.FontSize * this.Editor.LineSpacing;

            int firstLine = Math.Max(0, (int)Math.Floor(this.Offset.Y / lineHeight));
            int lastLine = Math.Min(this.Editor.Text.Lines.Count - 1, (int)Math.Floor((this.Offset.Y + this.Viewport.Height) / lineHeight));

            int firstColumn = Math.Max(0, (int)Math.Floor(this.Offset.X / this.Editor.CharacterWidth));
            int lastColumn = (int)Math.Floor((this.Offset.X + this.Viewport.Width) / this.Editor.CharacterWidth);

            int selStart = Math.Min(this.Editor.SelectionStart, this.Editor.SelectionEnd);
            int selEnd = Math.Max(this.Editor.SelectionStart, this.Editor.SelectionEnd);

            if (selEnd > selStart)
            {
                LinePosition startPosition = this.Editor.Text.Lines.GetLinePosition(selStart);
                LinePosition endPosition = this.Editor.Text.Lines.GetLinePosition(selEnd);

                List<Rect> rectsToFill = new List<Rect>(endPosition.Line - startPosition.Line + 1);

                for (int i = startPosition.Line; i <= endPosition.Line; i++)
                {
                    TextLine line = this.Editor.Text.Lines[i];

                    if (i >= firstLine && i <= lastLine)
                    {
                        if (i == startPosition.Line)
                        {
                            if (firstColumn < line.End - line.Start + 1)
                            {
                                rectsToFill.Add(CSharpSourceEditorCaret.Round(new Rect(Math.Max(0, startPosition.Character - firstColumn) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 41 + this.Editor.LineNumbersWidth, i * lineHeight - this.Offset.Y, (Math.Min(endPosition.Line != startPosition.Line ? int.MaxValue : endPosition.Character, Math.Min(lastColumn + 1, line.End - line.Start + 1)) - Math.Max(firstColumn, startPosition.Character)) * this.Editor.CharacterWidth, lineHeight), new Size(1, 1)));
                            }
                        }
                        else if (i == endPosition.Line && endPosition.Line != startPosition.Line)
                        {
                            if (firstColumn < selEnd - line.Start)
                            {
                                rectsToFill.Add(CSharpSourceEditorCaret.Round(new Rect(-this.Offset.X % this.Editor.CharacterWidth + 41 + this.Editor.LineNumbersWidth, i * lineHeight - this.Offset.Y, (Math.Min(lastColumn + 1, selEnd - line.Start) - firstColumn) * this.Editor.CharacterWidth, lineHeight), new Size(1, 1)));
                            }
                        }
                        else
                        {
                            if (firstColumn < line.End - line.Start + 1)
                            {
                                rectsToFill.Add(CSharpSourceEditorCaret.Round(new Rect(-this.Offset.X % this.Editor.CharacterWidth + 41 + this.Editor.LineNumbersWidth, i * lineHeight - this.Offset.Y, (Math.Min(lastColumn + 1, line.End - line.Start + 1) - firstColumn) * this.Editor.CharacterWidth, lineHeight), new Size(1, 1)));
                            }
                        }
                    }
                }

                for (int i = 0; i < rectsToFill.Count; i++)
                {
                    context.FillRectangle(this.Editor.SelectionBrush, rectsToFill[i]);
                }
            }

            foreach (MarkerRange range in this.Editor.Markers)
            {
                foreach (LinePositionSpan span in range.Span.ToLinePositionSpans(this.Editor.Text))
                {
                    Point startPoint = new Point(span.Start.Character * this.Editor.CharacterWidth - this.Offset.X + 41 + this.Editor.LineNumbersWidth, span.Start.Line * lineHeight - this.Offset.Y + this.Editor.FontSize * 1.3);

                    PathGeometry markerGeometry = new PathGeometry();
                    PathFigure markerFigure = new PathFigure() { StartPoint = startPoint, IsClosed = false };
                    Point currPoint = startPoint;

                    int direction = -1;
                    int deriv = 1;
                    while (currPoint.X - startPoint.X < (span.End.Character - span.Start.Character) * this.Editor.CharacterWidth)
                    {
                        double delta = Math.Min(this.Editor.CharacterWidth / 5, (span.End.Character - span.Start.Character) * this.Editor.CharacterWidth - currPoint.X + startPoint.X);

                        if (delta < 1e-5)
                        {
                            break;
                        }

                        currPoint = new Point(currPoint.X + delta, currPoint.Y + delta * direction);

                        direction += deriv;
                        deriv -= 2 * direction;

                        markerFigure.Segments.Add(new LineSegment() { Point = currPoint });
                    }

                    markerGeometry.Figures.Add(markerFigure);

                    context.DrawGeometry(null, range.MarkerPen, markerGeometry);
                }
            }

            if (this.Editor.SearchSpans.Count > 0 && this.Editor.SearchReplace.IsVisible)
            {
                foreach (SearchSpan matchSpan in this.Editor.SearchSpans)
                {
                    foreach (LinePositionSpan span in matchSpan.Span.ToLinePositionSpans(this.Editor.Text))
                    {
                        if (span.Start.Character <= lastColumn && span.End.Character >= firstColumn && span.Start.Line >= firstLine && span.Start.Line <= lastLine)
                        {
                            context.FillRectangle(SearchBrush, CSharpSourceEditorCaret.Round(new Rect(span.Start.Character * this.Editor.CharacterWidth - this.Offset.X + 41 + this.Editor.LineNumbersWidth, span.Start.Line * lineHeight - this.Offset.Y, (span.End.Character - span.Start.Character) * this.Editor.CharacterWidth, lineHeight), new Size(1, 1)));
                        }
                    }
                }
            }
        }
    }

    internal class MarkerRange
    {
        public TextSpan Span { get; }
        public IPen MarkerPen { get; }
        public List<Diagnostic> Diagnostics { get; }

        public MarkerRange(TextSpan span, IPen markerPen, List<Diagnostic> tag)
        {
            this.Span = span;
            this.MarkerPen = markerPen;
            this.Diagnostics = tag;
        }
    }
}
