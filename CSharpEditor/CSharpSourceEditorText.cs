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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;

namespace CSharpEditor
{
    internal class CSharpSourceEditorText : Control, ILogicalScrollable
    {
        private CSharpSourceEditor Editor { get; }

        internal CSharpSourceEditorText(CSharpSourceEditor editor)
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

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            using (context.PushClip(new Rect(41 + this.Editor.LineNumbersWidth, 0, this.Bounds.Width - (41 + this.Editor.LineNumbersWidth), this.Bounds.Height)))
            {

                //context.DrawRectangle(new Pen(Brushes.Red), new Avalonia.Rect(0, 0, this.Viewport.Width, this.Viewport.Height));

                //context.DrawRectangle(new Pen(Brushes.Green), new Rect(this.Offset.X, this.Offset.Y, this.Viewport.Width, this.Viewport.Height));

                //Typeface normalFace = new Typeface(this.FontFamily);

                double lineHeight = this.Editor.FontSize * this.Editor.LineSpacing;

                int firstLine = Math.Max(0, (int)Math.Floor(this.Offset.Y / lineHeight));
                int lastLine = Math.Min(this.Editor.Text.Lines.Count - 1, (int)Math.Floor((this.Offset.Y + this.Viewport.Height) / lineHeight));

                int firstColumn = Math.Max(0, (int)Math.Floor(this.Offset.X / this.Editor.CharacterWidth));
                int lastColumn = (int)Math.Floor((this.Offset.X + this.Viewport.Width) / this.Editor.CharacterWidth);

                for (int i = firstLine; i <= lastLine; i++)
                {
                    DrawLine(context, i, firstColumn, lastColumn, lineHeight);
                }
            }
        }

        private void DrawLine(DrawingContext context, int line, int firstColumn, int lastColumn, double lineHeight)
        {
            TextLine tLine = this.Editor.Text.Lines[line];

            int length = tLine.End - tLine.Start;

            if (length >= firstColumn)
            {
                int start = tLine.Start + firstColumn;
                int end = Math.Min(tLine.End, tLine.Start + lastColumn + 1);

                TextSpan span = new TextSpan(start, end - start);

                SyntaxTree syntaxTree = this.Editor.GetSyntaxTreeNow();
                SemanticModel model = this.Editor.GetSemanticModelNow();

                if (Editor.SyntaxHighlightingMode != SyntaxHighlightingModes.None && syntaxTree != null)
                {
                    List<(int start, int end, Color color)> colorRanges = new List<(int start, int end, Color color)>();

                    foreach (SyntaxToken token in syntaxTree.GetRoot().DescendantTokens(span, descendIntoTrivia: true))
                    {
                        if (token.HasLeadingTrivia)
                        {
                            colorRanges.AddRange(ToColorRange(token.LeadingTrivia));
                        }

                        (int start, int end, Color? color) range = ToColorRange(token);

                        if (range.color != null || token.Kind() != SyntaxKind.IdentifierToken || Editor.SyntaxHighlightingMode != SyntaxHighlightingModes.Semantic || model == null)
                        {
                            colorRanges.Add((range.start, range.end, range.color ?? Colors.Black));
                        }
                        else if (token.Kind() == SyntaxKind.IdentifierToken)
                        {
                            colorRanges.Add((range.start, range.end, token.GetIdentifierColor(model)));
                        }

                        if (token.HasTrailingTrivia)
                        {
                            colorRanges.AddRange(ToColorRange(token.TrailingTrivia));
                        }
                    }

                    foreach ((int start, int end, Color color) range in Consolidate(colorRanges, start, end))
                    {
                        //context.DrawRectangle(new Pen(new SolidColorBrush(range.color)), new Rect((range.start - start) * CharacterWidth, line * lineHeight - this.Offset.Y, (range.end - range.start) * CharacterWidth, lineHeight));


                        Avalonia.Media.FormattedText formattedText = new Avalonia.Media.FormattedText() { Text = this.Editor.Text.ToString(new TextSpan(range.start, range.end - range.start)), Typeface = this.Editor.Typeface, FontSize = this.Editor.FontSize, TextWrapping = TextWrapping.NoWrap };
                        context.DrawText(new SolidColorBrush(range.color), new Point((range.start - start) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 41 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y), formattedText);
                    }
                }
                else
                {
                    string text = this.Editor.Text.ToString(span);
                    Avalonia.Media.FormattedText formattedText = new Avalonia.Media.FormattedText() { Text = text, Typeface = this.Editor.Typeface, FontSize = this.Editor.FontSize, TextWrapping = TextWrapping.NoWrap };
                    context.DrawText(Brushes.Black, new Point(-this.Offset.X % this.Editor.CharacterWidth + 41 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y), formattedText);
                }
            }
        }

        private (int start, int end, Color? color) ToColorRange(SyntaxToken token)
        {
            return (token.SpanStart, token.Span.End, token.GetColor());
        }

        private IEnumerable<(int start, int end, Color color)> Consolidate(IEnumerable<(int start, int end, Color color)> ranges, int start, int end)
        {
            int currPos = start;

            List<(int start, int end, Color color)> tbr = new List<(int start, int end, Color color)>();

            foreach ((int start, int end, Color color) range in ranges)
            {
                if (range.start < end)
                {
                    if (currPos < 0)
                    {
                        currPos = range.start;
                    }

                    int rangeStart = Math.Max(range.start, currPos);

                    if (currPos < rangeStart)
                    {
                        tbr.Add((currPos, rangeStart, Colors.Black));
                    }
                    else if (currPos > range.start)
                    {
                        currPos = Math.Max(start, range.start);
                        rangeStart = currPos;

                        for (int i = tbr.Count - 1; i >= 0; i--)
                        {
                            if (tbr[i].start > currPos)
                            {
                                tbr.RemoveAt(i);
                            }
                            else
                            {
                                if (tbr[i].end > currPos)
                                {
                                    tbr[i] = (tbr[i].start, currPos, tbr[i].color);
                                }

                                break;
                            }
                        }
                    }

                    int rangeEnd = Math.Min(range.end, end);

                    if (rangeStart < rangeEnd)
                    {
                        tbr.Add((rangeStart, rangeEnd, range.color));
                        currPos = rangeEnd;
                    }
                }
            }

            return tbr;
        }

        private IEnumerable<(int start, int end, Color color)> ToColorRange(SyntaxTriviaList triviaList)
        {
            foreach (SyntaxTrivia trivia in triviaList)
            {
                SyntaxKind kind = trivia.Kind();

                if (kind == SyntaxKind.SingleLineCommentTrivia || kind == SyntaxKind.MultiLineCommentTrivia || kind == SyntaxKind.MultiLineDocumentationCommentTrivia || kind == SyntaxKind.SingleLineDocumentationCommentTrivia)
                {
                    yield return (trivia.FullSpan.Start, trivia.FullSpan.End, SyntaxKindColor.CommentColor);
                }
            }
        }

    }
}
