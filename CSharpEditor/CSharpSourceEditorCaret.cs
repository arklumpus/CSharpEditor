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
using Avalonia.Threading;
using Microsoft.CodeAnalysis.Text;
using System;

namespace CSharpEditor
{
    internal class CSharpSourceEditorCaret : Control, ILogicalScrollable
    {
        private CSharpSourceEditor Editor { get; }

        private readonly DispatcherTimer _caretTimer = new DispatcherTimer();

        internal CSharpSourceEditorCaret(CSharpSourceEditor editor)
        {
            this.Editor = editor;
            this.IsHitTestVisible = false;
            _caretTimer.Tick += CaretTimerTick;
            _caretTimer.Interval = TimeSpan.FromMilliseconds(500);
            _caretTimer.Start();
        }

        private bool _visible = true;

        private void CaretTimerTick(object sender, EventArgs e)
        {
            _visible = !_visible;
            this.InvalidateVisual();
        }

        public bool IsCaretFocused = false;

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

        public void Show()
        {
            _caretTimer.Stop();
            _visible = true;
            this.InvalidateVisual();
            _caretTimer.Start();
        }

        static readonly IBrush OvestrikeBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_visible && IsCaretFocused)
            {
                LinePosition pos = Editor.Text.Lines.GetLinePosition(Editor.CaretOffset);

                int line = pos.Line;

                if (!Editor.OverstrikeMode || Editor.CaretOffset == Editor.Text.Lines[line].End)
                {
                    context.FillRectangle(Brushes.Black, GetCaretRectangle());
                }
                else
                {
                    context.FillRectangle(OvestrikeBrush, GetCaretRectangle());
                }
            }
        }

        public Rect GetCaretRectangle()
        {
            LinePosition pos = Editor.Text.Lines.GetLinePosition(Editor.CaretOffset);

            int line = pos.Line;

            int character = Math.Min(pos.Character, Editor.Text.Lines[pos.Line].End - Editor.Text.Lines[pos.Line].Start);

            double lineHeight = Editor.FontSize * Editor.LineSpacing;

            int firstColumn = Math.Max(0, (int)Math.Floor(this.Offset.X / Editor.CharacterWidth));

            if (!Editor.OverstrikeMode || Editor.CaretOffset == Editor.Text.Lines[line].End)
            {
                return Round(new Rect((character - firstColumn) * Editor.CharacterWidth - this.Offset.X % Editor.CharacterWidth + 41 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y, 0.5, lineHeight), new Size(1, 1));
            }
            else
            {
                return Round(new Rect((character - firstColumn) * Editor.CharacterWidth - this.Offset.X % Editor.CharacterWidth + 41 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y, Editor.CharacterWidth, lineHeight), new Size(1, 1));
            }
        }

        public static Rect Round(Rect rect, Size pixelSize)
        {
            return new Rect(Round(rect.X, pixelSize.Width), Round(rect.Y, pixelSize.Height),
                            Round(rect.Width, pixelSize.Width), Round(rect.Height, pixelSize.Height));
        }

        public static double Round(double value, double pixelSize)
        {
            return pixelSize * Math.Round(value / pixelSize, MidpointRounding.AwayFromZero);
        }
    }
}
