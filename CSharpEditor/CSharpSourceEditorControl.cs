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
using Avalonia.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal class CSharpSourceEditorControl : UserControl
    {
        public SyntaxHighlightingModes SyntaxHighlightingMode
        {
            get
            {
                return Editor.SyntaxHighlightingMode;
            }
            set
            {
                Editor.SyntaxHighlightingMode = value;
                Editor.TextLayer.InvalidateVisual();
            }
        }

        public double CharacterWidth => Editor.CharacterWidth;

        public SourceText Text => Editor.Text;

        public Document Document => Editor.Document;

        public async Task<CSharpSyntaxTree> GetSyntaxTree()
        {
            return await Editor.GetSyntaxTree();
        }

        public int CaretOffset
        {
            get
            {
                return Editor.CaretOffset;
            }

            set
            {
                Editor.SelectionStart = Editor.SelectionEnd = Editor.CaretOffset = value;
                Editor.SelectionLayer.InvalidateVisual();
                Editor.CaretLayer.InvalidateVisual();
                Editor.ScrollBarMarker.InvalidateVisual();
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return this.Editor.IsReadOnly;
            }

            set
            {
                this.Editor.IsReadOnly = value;
            }
        }

        public double VerticalOffset => Editor.Offset.Y;
        public double HorizontalOffset => Editor.Offset.X;

        public double LineHeight => Editor.FontSize * Editor.LineSpacing;
        public ImmutableList<HighlightedLineRange> HighlightedLines
        {
            get
            {
                return Editor.HighlightedLines;
            }

            set
            {
                Editor.HighlightedLines = value;
                Editor.LineNumbersLayer.InvalidateVisual();
                Editor.ScrollBarMarker.InvalidateVisual();
            }
        }

        public ImmutableList<MarkerRange> Markers
        {
            get
            {
                return Editor.Markers;
            }

            set
            {
                Editor.Markers = value;
                Editor.SelectionLayer.InvalidateVisual();
                Editor.ScrollBarMarker.InvalidateVisual();
            }
        }

        public int ActiveBreakpoint
        {
            get
            {
                return Editor.ActiveBreakpoint;
            }

            set
            {
                Editor.ActiveBreakpoint = value;
                Editor.BreakpointsLayer.InvalidateVisual();
                Editor.ScrollBarMarker.InvalidateVisual();
            }
        }

        public bool ShowLineChanges
        {
            get
            {
                return Editor.ShowLineChanges;
            }

            set
            {
                Editor.ShowLineChanges = value;
                Editor.LineNumbersLayer.InvalidateVisual();
            }
        }

        public bool ShowScrollbarOverview
        {
            get
            {
                return Editor.ShowScrollbarOverview;
            }

            set
            {
                Editor.ShowScrollbarOverview = value;
                Editor.ScrollBarMarker.InvalidateVisual();
            }
        }

        public event EventHandler<ToggleBreakpointEventArgs> ToggleBreakpoint
        {
            add
            {
                Editor.ToggleBreakpoint += value;
            }

            remove
            {
                Editor.ToggleBreakpoint -= value;
            }
        }

        public event EventHandler<PointerWheelEventArgs> OnPreviewPointerWheelChanged
        {
            add
            {
                Editor.OnPreviewPointerWheelChanged += value;
            }

            remove
            {
                Editor.OnPreviewPointerWheelChanged -= value;
            }
        }

        public event EventHandler<PointerEventArgs> PointerHover
        {
            add
            {
                Editor.PointerHover += value;
            }

            remove
            {
                Editor.PointerHover -= value;
            }
        }

        public event EventHandler<PointerEventArgs> PointerHoverStopped
        {
            add
            {
                Editor.PointerHoverStopped += value;
            }

            remove
            {
                Editor.PointerHoverStopped -= value;
            }
        }

        public event EventHandler<PasteEventArgs> OnPaste
        {
            add
            {
                Editor.OnPaste += value;
            }

            remove
            {
                Editor.OnPaste -= value;
            }
        }

        public Func<TextInputEventArgs, Task> OnTextEntering
        {
            get
            {
                return Editor.OnTextEntering;
            }

            set
            {
                Editor.OnTextEntering = value;
            }
        }

        public Func<TextInputEventArgs, Task> OnTextEntered
        {
            get
            {
                return Editor.OnTextEntered;
            }

            set
            {
                Editor.OnTextEntered = value;
            }
        }

        public event EventHandler<EventArgs> TextChanged
        {
            add
            {
                this.Editor.TextChanged += value;
            }

            remove
            {
                this.Editor.TextChanged -= value;
            }
        }

        public Func<KeyEventArgs, Task> OnPreviewKeyDown
        {
            get
            {
                return Editor.OnPreviewKeyDown;
            }

            set
            {
                Editor.OnPreviewKeyDown = value;
            }
        }

        public Func<KeyEventArgs, Task> OnPreviewKeyUp
        {
            get
            {
                return Editor.OnPreviewKeyUp;
            }

            set
            {
                Editor.OnPreviewKeyUp = value;
            }
        }

        public async Task SetText(SourceText text)
        {
            await Editor.SetText(text);
            Editor.SelectionLayer.InvalidateVisual();
            Editor.TextLayer.InvalidateVisual();
            Editor.CaretLayer.InvalidateVisual();
            Editor.LineNumbersLayer.InvalidateVisual();
            Editor.BreakpointsLayer.InvalidateVisual();
            Editor.ScrollBarMarker.InvalidateVisual();
        }

        public async Task SetText(string text)
        {
            await Editor.SetText(SourceText.From(text));
            Editor.SelectionLayer.InvalidateVisual();
            Editor.TextLayer.InvalidateVisual();
            Editor.CaretLayer.InvalidateVisual();
            Editor.LineNumbersLayer.InvalidateVisual();
            Editor.BreakpointsLayer.InvalidateVisual();
            Editor.ScrollBarMarker.InvalidateVisual();
        }

        public async Task SetDocument(Document document)
        {
            await Editor.SetDocument(document);
            Editor.SelectionLayer.InvalidateVisual();
            Editor.TextLayer.InvalidateVisual();
            Editor.CaretLayer.InvalidateVisual();
            Editor.LineNumbersLayer.InvalidateVisual();
            Editor.BreakpointsLayer.InvalidateVisual();
            Editor.ScrollBarMarker.InvalidateVisual();
        }

        public int SelectionStart => Editor.SelectionStart;
        public int SelectionEnd => Editor.SelectionEnd;

        public void SetSelection(int selectionStart, int selectionLength)
        {
            Editor.SelectionStart = selectionStart;
            Editor.SelectionEnd = selectionStart + selectionLength;
            Editor.CaretOffset = Editor.SelectionEnd;
            Editor.CaretLayer.Show();
            Editor.SelectionLayer.InvalidateVisual();
            Editor.CaretLayer.InvalidateVisual();
            Editor.ScrollBarMarker.InvalidateVisual();
        }

        public Rect GetCaretRectangle()
        {
            Rect rect = this.Editor.GetCaretRectangle();

            return new Rect(rect.X + this.HorizontalOffset, rect.Y + this.VerticalOffset, rect.Width, rect.Height);
        }

        public LinePosition? GetPositionFromPoint(Point position)
        {
            int row = (int)Math.Floor((position.Y + this.Editor.Offset.Y) / (this.FontSize * this.Editor.LineSpacing));
            int column = Math.Max(0, (int)Math.Round((position.X + this.Editor.Offset.X + 2 - (41 + this.Editor.LineNumbersWidth)) / this.CharacterWidth));

            if (column >= 0 && row >= 0 && row < this.Text.Lines.Count)
            {
                TextLine line = this.Text.Lines[row];

                if (column <= line.End - line.Start)
                {
                    return new LinePosition(row, column);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public void ClearUndoStack()
        {
            Editor.ClearUndoStack();
        }


        private readonly CSharpSourceEditor Editor;

        public CSharpSourceEditorControl()
        {
            Grid gridContainer = new Grid();
            this.Content = gridContainer;

            ScrollViewer scroller = new ScrollViewer() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, AllowAutoHide = false };
            gridContainer.Children.Add(scroller);

            //Panel scrollerContent = new Panel();

            Editor = new CSharpSourceEditor()
            {

            };

            scroller.Content = Editor;

            this.PropertyChanged += (s, e) =>
            {
                if (e.Property == FontSizeProperty)
                {
                    this.Editor.FontSize = (double)e.NewValue;
                    Editor.SelectionLayer.InvalidateVisual();
                    Editor.TextLayer.InvalidateVisual();
                    Editor.CaretLayer.InvalidateVisual();
                    Editor.LineNumbersLayer.InvalidateVisual();
                    Editor.BreakpointsLayer.InvalidateVisual();
                    Editor.LineNumbersWidth = this.CharacterWidth * (Math.Floor(Math.Log10(Editor.Text.Lines.Count)) + 1);
                    Editor.InvalidateMeasure();
                }
            };

            gridContainer.Children.Add(Editor.ScrollBarMarker);

            //gridContainer.Children.Add(Editor.SearchReplace);

            //scrollerContent.Children.Add(editor);

            //scrollerContent.Children.Add(new CSharpSourceEditorCaret(editor));

            //scroller.Content = scrollerContent;


        }
    }
}
