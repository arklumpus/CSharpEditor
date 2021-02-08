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
using Microsoft.CodeAnalysis.Text;
using System;

namespace CSharpEditor
{
    internal class CSharpSourceEditorLineNumbers : Control, ILogicalScrollable
    {
        private CSharpSourceEditor Editor { get; }

        internal CSharpSourceEditorLineNumbers(CSharpSourceEditor editor)
        {
            this.Editor = editor;
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

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            Point position = e.GetPosition(this);



            int row = (int)Math.Floor((position.Y + this.Offset.Y) / (this.Editor.FontSize * this.Editor.LineSpacing));

            if (row >= 0 && row < this.Editor.Text.Lines.Count)
            {
                TextLine line = this.Editor.Text.Lines[row];

                if (position.X >= 16)
                {
                    this.Editor.CaretOffset = line.End;
                    this.Editor.SelectionStart = line.Start;
                    this.Editor.SelectionEnd = line.End;
                    this.Editor.CaretLayer.Show();
                    this.Editor.SelectionLayer.InvalidateVisual();

                    this.Editor.IsPointerPressed = true;
                }
                else
                {
                    this.Editor.InvokeToggleBreakpoint(new ToggleBreakpointEventArgs(row, line.Start, line.End));
                }
            }

            e.Handled = true;

            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (this.Editor.IsPointerPressed)
            {
                Point position = e.GetPosition(this);

                int row = (int)Math.Floor((position.Y + this.Offset.Y) / (this.Editor.FontSize * this.Editor.LineSpacing));

                if (row >= 0 && row < this.Editor.Text.Lines.Count)
                {
                    TextLine line = this.Editor.Text.Lines[row];

                    if (this.Editor.SelectionStart < line.End)
                    {
                        this.Editor.SelectionStart = Math.Min(this.Editor.SelectionEnd, this.Editor.SelectionStart);
                        this.Editor.CaretOffset = line.End;
                        this.Editor.SelectionEnd = line.End;
                    }
                    else
                    {
                        this.Editor.SelectionStart = Math.Max(this.Editor.SelectionEnd, this.Editor.SelectionStart);
                        this.Editor.CaretOffset = line.Start;
                        this.Editor.SelectionEnd = line.Start;
                    }

                    this.Editor.CaretLayer.Show();
                    this.Editor.SelectionLayer.InvalidateVisual();
                }

                e.Handled = true;
            }

            base.OnPointerMoved(e);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            context.FillRectangle(Brushes.White, new Rect(0, 0, this.Editor.LineNumbersWidth + 15 + 16, this.Bounds.Height));

            using (context.PushClip(new Rect(0, 0, this.Bounds.Width, this.Bounds.Height)))
            {
                double lineHeight = this.Editor.FontSize * this.Editor.LineSpacing;

                int firstLine = Math.Max(0, (int)Math.Floor(this.Offset.Y / lineHeight));
                int lastLine = Math.Min(this.Editor.Text.Lines.Count - 1, (int)Math.Floor((this.Offset.Y + this.Viewport.Height) / lineHeight));

                /*int firstColumn = Math.Max(0, (int)Math.Floor(this.Offset.X / this.Editor.CharacterWidth));
                int lastColumn = (int)Math.Floor((this.Offset.X + this.Viewport.Width) / this.Editor.CharacterWidth);*/

                for (int i = firstLine; i <= lastLine; i++)
                {
                    Avalonia.Media.FormattedText formattedText = new Avalonia.Media.FormattedText() { Text = (i + 1).ToString(), Typeface = this.Editor.Typeface, FontSize = this.Editor.FontSize, TextWrapping = TextWrapping.NoWrap };
                    context.DrawText(this.Editor.LineNumbersBrush, new Point(21 + this.Editor.LineNumbersWidth - (Math.Floor(Math.Log10(i + 1)) + 1) * this.Editor.CharacterWidth, i * lineHeight - this.Offset.Y), formattedText);
                }

                if (this.Editor.ShowLineChanges)
                {
                    foreach (HighlightedLineRange lines in this.Editor.HighlightedLines)
                    {
                        if (lines.LineSpan.End >= firstLine && lastLine > lines.LineSpan.Start - firstLine)
                        {
                            double topY = lines.LineSpan.Start * lineHeight - this.Offset.Y;
                            double bottomY = (lines.LineSpan.End + 1) * lineHeight - this.Offset.Y;

                            context.FillRectangle(lines.HighlightBrush, new Avalonia.Rect(21 + this.Editor.LineNumbersWidth + 5, topY, 5, bottomY - topY));
                        }
                    }
                }
            }
        }
    }

    internal class HighlightedLineRange
    {
        public Microsoft.CodeAnalysis.Text.TextSpan LineSpan { get; }
        public IBrush HighlightBrush { get; }

        public HighlightedLineRange(Microsoft.CodeAnalysis.Text.TextSpan lineSpan, IBrush higlightBrush)
        {
            this.LineSpan = lineSpan;
            this.HighlightBrush = higlightBrush;
        }
    }
}
