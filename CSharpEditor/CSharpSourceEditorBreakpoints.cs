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
using System;

namespace CSharpEditor
{
    internal class CSharpSourceEditorBreakpoints : Control, ILogicalScrollable
    {
        private CSharpSourceEditor Editor { get; }

        internal CSharpSourceEditorBreakpoints(CSharpSourceEditor editor)
        {
            this.Editor = editor;
            this.IsHitTestVisible = false;

            PathFigure arrowFigure = new PathFigure() { IsClosed = true, IsFilled = true, StartPoint = new Point(2, 3.5) };
            arrowFigure.Segments.Add(new LineSegment() { Point = new Point(6, 3.5) });
            arrowFigure.Segments.Add(new LineSegment() { Point = new Point(6, 1) });
            arrowFigure.Segments.Add(new LineSegment() { Point = new Point(11, 6) });
            arrowFigure.Segments.Add(new LineSegment() { Point = new Point(6, 11) });
            arrowFigure.Segments.Add(new LineSegment() { Point = new Point(6, 8.5) });
            arrowFigure.Segments.Add(new LineSegment() { Point = new Point(2, 8.5) });

            BreakpointArrowGeometry = new PathGeometry();
            BreakpointArrowGeometry.Figures.Add(arrowFigure);
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

        public bool BringIntoView(Control target, Rect targetRect)
        {
            return false;
        }

        public Control GetControlInDirection(NavigationDirection direction, Control from)
        {
            return null;
        }

        public void RaiseScrollInvalidated(EventArgs e)
        {

        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            e.Handled = true;
            base.OnPointerPressed(e);
        }

        private static readonly SolidColorBrush GreyBrush = new SolidColorBrush(Color.FromRgb(243, 243, 243));
        private static readonly SolidColorBrush BreakpointBrush = new SolidColorBrush(Color.FromRgb(228, 20, 0));
        private static readonly SolidColorBrush BreakpointHighlightBrush = new SolidColorBrush(Color.FromArgb(191, 143, 44, 58));
        private static readonly SolidColorBrush ActiveBreakpointBrush = new SolidColorBrush(Color.FromRgb(255, 216, 55));
        private static readonly Pen ActiveBreakpointPen = new Pen(Brushes.Black, 0.5);
        private readonly PathGeometry BreakpointArrowGeometry;

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            context.FillRectangle(GreyBrush, new Rect(0, 0, 16, this.Bounds.Height));

            using (context.PushClip(new Rect(0, 0, this.Bounds.Width, this.Bounds.Height)))
            {
                double lineHeight = this.Editor.FontSize * this.Editor.LineSpacing;

                int firstLine = Math.Max(0, (int)Math.Floor(this.Offset.Y / lineHeight));
                int lastLine = Math.Min(this.Editor.Text.Lines.Count - 1, (int)Math.Floor((this.Offset.Y + this.Viewport.Height) / lineHeight));

                for (int i = firstLine; i <= lastLine; i++)
                {
                    string lineText = this.Editor.Text.ToString(this.Editor.Text.Lines[i].Span);

                    int breakpointIndex = lineText.IndexOf(Utils.BreakpointMarker);

                    if (breakpointIndex >= 0)
                    {
                        EllipseGeometry geometry = new EllipseGeometry(new Rect(2, i * lineHeight - this.Offset.Y + lineHeight * 0.5 - 6, 12, 12));
                        context.DrawGeometry(BreakpointBrush, null, geometry);

                        Avalonia.Media.FormattedText formattedText = new Avalonia.Media.FormattedText(Utils.BreakpointMarker, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, this.Editor.Typeface, this.Editor.FontSize, this.Editor.Text.Lines[i].Span.Start + breakpointIndex != this.Editor.ActiveBreakpoint ? Brushes.White : Brushes.Black);

                        if (this.Editor.Text.Lines[i].Span.Start + breakpointIndex != this.Editor.ActiveBreakpoint)
                        {
                            context.FillRectangle(BreakpointHighlightBrush, CSharpSourceEditorCaret.Round(new Rect(breakpointIndex * this.Editor.CharacterWidth - this.Offset.X + 41 + this.Editor.LineNumbersWidth, i * lineHeight - this.Offset.Y, Utils.BreakpointMarker.Length * this.Editor.CharacterWidth, lineHeight), new Size(1, 1)));
                            context.DrawText(formattedText, new Point(breakpointIndex * this.Editor.CharacterWidth - this.Offset.X + 41 + this.Editor.LineNumbersWidth, i * lineHeight - this.Offset.Y));
                        }
                        else
                        {
                            BreakpointArrowGeometry.Transform = new TranslateTransform(2, (i + 0.5) * lineHeight - this.Offset.Y - 6);
                            context.DrawGeometry(ActiveBreakpointBrush, ActiveBreakpointPen, BreakpointArrowGeometry);

                            context.FillRectangle(ActiveBreakpointBrush, CSharpSourceEditorCaret.Round(new Rect(breakpointIndex * this.Editor.CharacterWidth - this.Offset.X + 41 + this.Editor.LineNumbersWidth, i * lineHeight - this.Offset.Y, Utils.BreakpointMarker.Length * this.Editor.CharacterWidth, lineHeight), new Size(1, 1)));
                            context.DrawText(formattedText, new Point(breakpointIndex * this.Editor.CharacterWidth - this.Offset.X + 41 + this.Editor.LineNumbersWidth, i * lineHeight - this.Offset.Y));
                        }
                    }
                }
            }
        }
    }
}
