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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal class CSharpSourceEditor : Control, ILogicalScrollable
    {
        internal static readonly DirectProperty<CSharpSourceEditor, ImmutableList<HighlightedLineRange>> HighlightedLinesProperty =
        AvaloniaProperty.RegisterDirect<CSharpSourceEditor, ImmutableList<HighlightedLineRange>>(
            nameof(HighlightedLines),
            o => o.HighlightedLines,
            (o, v) => o.HighlightedLines = v);

        private ImmutableList<HighlightedLineRange> _highlightedLines = ImmutableList<HighlightedLineRange>.Empty;

        internal ImmutableList<HighlightedLineRange> HighlightedLines
        {
            get { return _highlightedLines; }
            set { SetAndRaise(HighlightedLinesProperty, ref _highlightedLines, value); }
        }

        public int ActiveBreakpoint { get; set; } = -1;

        public ImmutableList<MarkerRange> Markers { get; set; } = ImmutableList<MarkerRange>.Empty;

        private string _searchText = "";
        public string SearchText
        {
            get
            {
                return _searchText;
            }
            set
            {
                _searchText = value;
                UpdateSearchResults();
                this.SelectionLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
            }
        }

        public bool IsReadOnly { get; set; } = false;
        public string ReplaceText { get; set; } = "";

        private bool _isSearchRegex = false;
        public bool IsSearchRegex
        {
            get
            {
                return _isSearchRegex;
            }
            set
            {
                _isSearchRegex = value;
                UpdateSearchResults();
                this.SelectionLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
            }
        }

        private bool _isSearchCaseSensitive = false;
        public bool IsSearchCaseSensitive
        {
            get
            {
                return _isSearchCaseSensitive;
            }
            set
            {
                _isSearchCaseSensitive = value;
                UpdateSearchResults();
                this.SelectionLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
            }
        }

        public ImmutableList<SearchSpan> SearchSpans { get; private set; } = ImmutableList<SearchSpan>.Empty;

        private readonly Stack<IEnumerable<TextChange>> UndoStack = new Stack<IEnumerable<TextChange>>();
        private readonly Stack<IEnumerable<TextChange>> RedoStack = new Stack<IEnumerable<TextChange>>();

        public Document Document { get; private set; }

        public SourceText Text { get; private set; } = SourceText.From("");

        public Func<TextInputEventArgs, Task> OnTextEntering { get; set; }
        public Func<TextInputEventArgs, Task> OnTextEntered { get; set; }

        public event EventHandler<EventArgs> TextChanged;
        public event EventHandler<PasteEventArgs> OnPaste;
        public Func<KeyEventArgs, Task> OnPreviewKeyDown { get; set; }
        public Func<KeyEventArgs, Task> OnPreviewKeyUp { get; set; }

        public event EventHandler<PointerWheelEventArgs> OnPreviewPointerWheelChanged;

        public async Task SetText(SourceText text)
        {
            text = ReplaceTabsWithSpaces(text);
            await SetSourceText(text);
        }

        internal async Task SetDocument(Document document, bool ignoreStacks = false)
        {
            IEnumerable<TextChange> changes = await Document.GetTextChangesAsync(document);

            if (!ignoreStacks)
            {
                RedoStack.Clear();
                UndoStack.Push(changes);
            }
            Text = await document.GetTextAsync();
            Document = document;
            List<MarkerRange> newMarkers = new List<MarkerRange>();
            foreach (MarkerRange marker in Markers)
            {
                TextSpan? newSpan = marker.Span.ApplyChanges(changes);

                if (newSpan != null)
                {
                    newMarkers.Add(new MarkerRange(newSpan.Value, marker.MarkerPen, marker.Diagnostics));
                }
            }
            this.Markers = ImmutableList.Create(newMarkers.ToArray());
            UpdateSyntaxTreeAndSemanticModel(document);
            this.LineNumbersWidth = this.CharacterWidth * (Math.Floor(Math.Log10(Text.Lines.Count)) + 1);
            this.InvalidateMeasure();
            if (SearchReplace.IsVisible)
            {
                UpdateSearchResults();
            }
            TextChanged?.Invoke(this, new EventArgs());
        }

        internal async Task SetSourceText(SourceText text, bool ignoreStacks = false)
        {
            Document newDocument = Document.WithText(text);

            IEnumerable<TextChange> changes = await Document.GetTextChangesAsync(newDocument);

            if (!ignoreStacks)
            {
                RedoStack.Clear();
                UndoStack.Push(changes);
            }
            Text = text;
            Document = newDocument;
            List<MarkerRange> newMarkers = new List<MarkerRange>();
            foreach (MarkerRange marker in Markers)
            {
                TextSpan? newSpan = marker.Span.ApplyChanges(changes);

                if (newSpan != null)
                {
                    newMarkers.Add(new MarkerRange(newSpan.Value, marker.MarkerPen, marker.Diagnostics));
                }
            }
            this.Markers = ImmutableList.Create(newMarkers.ToArray());
            UpdateSyntaxTreeAndSemanticModel(newDocument);
            this.LineNumbersWidth = this.CharacterWidth * (Math.Floor(Math.Log10(text.Lines.Count)) + 1);
            this.InvalidateMeasure();
            if (SearchReplace.IsVisible)
            {
                UpdateSearchResults();
            }
            TextChanged?.Invoke(this, new EventArgs());
        }

        private async void UpdateSyntaxTreeAndSemanticModel(Document doc)
        {
            await _syntaxTreeSemaphore.WaitAsync();
            await _semanticModelSemaphore.WaitAsync();

            _syntaxTree = (CSharpSyntaxTree)await doc.GetSyntaxTreeAsync();
            _syntaxTreeSemaphore.Release();

            this.TextLayer.InvalidateVisual();

            _semanticModel = await doc.GetSemanticModelAsync();
            _semanticModelSemaphore.Release();

            this.TextLayer.InvalidateVisual();
        }

        private CSharpSyntaxTree _syntaxTree;
        private readonly SemaphoreSlim _syntaxTreeSemaphore = new SemaphoreSlim(1, 1);

        public async Task<CSharpSyntaxTree> GetSyntaxTree()
        {
            await _syntaxTreeSemaphore.WaitAsync();

            CSharpSyntaxTree tree = _syntaxTree;

            _syntaxTreeSemaphore.Release();

            return tree;
        }

        public CSharpSyntaxTree GetSyntaxTreeNow()
        {
            CSharpSyntaxTree tree = null;

            if (_syntaxTreeSemaphore.Wait(0))
            {
                tree = _syntaxTree;
                _syntaxTreeSemaphore.Release();
            }

            return tree;
        }

        private SemanticModel _semanticModel;
        private readonly SemaphoreSlim _semanticModelSemaphore = new SemaphoreSlim(1, 1);

        public async Task<SemanticModel> GetSemanticModel()
        {
            await _semanticModelSemaphore.WaitAsync();

            SemanticModel model = _semanticModel;

            _semanticModelSemaphore.Release();

            return model;
        }

        public SemanticModel GetSemanticModelNow()
        {
            SemanticModel model = null;

            if (_semanticModelSemaphore.Wait(0))
            {
                model = _semanticModel;
                _semanticModelSemaphore.Release();
            }

            return model;
        }

        private double _fontSize = 14;
        public double FontSize
        {
            get
            {
                return _fontSize;
            }

            set
            {
                _fontSize = value;
                MeasureCharacter();
            }
        }

        private FontFamily _fontFamily = FontFamily.Parse("resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Roboto Mono");
        public FontFamily FontFamily
        {
            get
            {
                return _fontFamily;
            }

            set
            {
                _fontFamily = value;
                Typeface = new Typeface(_fontFamily);
                MeasureCharacter();
            }
        }


        public double LineSpacing { get; set; } = 1.4;
        public bool CanHorizontallyScroll { get => true; set { } }
        public bool CanVerticallyScroll { get => true; set { } }

        public bool IsLogicalScrollEnabled => true;

        public Size ScrollSize => new Size(10, 50);

        public Size PageScrollSize => new Size(10, 100);

        public Size Extent
        {
            get
            {
                int maxWidth = 0;

                foreach (TextLine line in Text.Lines)
                {
                    maxWidth = Math.Max(maxWidth, line.End - line.Start + 1);
                }

                double height = Text.Lines.Count * LineSpacing * FontSize + 20;

                return new Size(maxWidth * CharacterWidth + 41 + this.LineNumbersWidth + 20, height);

            }
        }

        private Vector offset;
        public Vector Offset
        {
            get
            {
                return offset;
            }

            set
            {
                offset = value;
                this.SelectionLayer.InvalidateVisual();
                this.TextLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.LineNumbersLayer.InvalidateVisual();
                this.BreakpointsLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
                this.InvalidateVisual();

            }
        }

        public Size Viewport { get; private set; }

        public IBrush Background { get; set; } = Brushes.White;
        public IBrush SelectionBrush { get; set; } = new SolidColorBrush(Color.FromRgb(153, 201, 239));
        public IBrush LineNumbersBrush { get; set; } = new SolidColorBrush(Color.FromRgb(43, 145, 175));

        internal Typeface Typeface { get; private set; } = new Typeface(FontFamily.Parse("resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Roboto Mono"));
        internal double CharacterWidth = double.NaN;
        internal double LineNumbersWidth = 0;
        public SyntaxHighlightingModes SyntaxHighlightingMode { get; set; } = SyntaxHighlightingModes.Semantic;
        public bool ShowLineChanges { get; set; } = true;
        public bool ShowScrollbarOverview { get; set; } = true;

        private int _caretOffset = 0;
        public int CaretOffset
        {
            get
            {
                return _caretOffset;
            }
            set
            {
                _caretOffset = value;
                ScrollToCaret();
            }
        }
        internal bool OverstrikeMode { get; set; } = false;

        internal Rect GetCaretRectangle()
        {
            return this.CaretLayer.GetCaretRectangle();
        }

        public event EventHandler ScrollInvalidated;

        internal CSharpSourceEditorCaret CaretLayer { get; }
        internal CSharpSourceEditorText TextLayer { get; }
        internal CSharpSourceEditorSelection SelectionLayer { get; }
        internal CSharpSourceEditorLineNumbers LineNumbersLayer { get; }
        internal CSharpSourceEditorBreakpoints BreakpointsLayer { get; }
        internal CSharpSourceEditorSearchReplace SearchReplace { get; }
        internal CSharpSourceEditorScrollBarMarker ScrollBarMarker { get; }


        public event EventHandler<PointerEventArgs> PointerHover;
        public event EventHandler<PointerEventArgs> PointerHoverStopped;
        private readonly DispatcherTimer _hoverTimer;
        private bool _hovering;
        private PointerEventArgs _lastHoverEventArgs;
        private Point _lastHoverPosition;
        public event EventHandler<ToggleBreakpointEventArgs> ToggleBreakpoint;

        public CSharpSourceEditor()
        {
            MefHostServices host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
            AdhocWorkspace workspace = new AdhocWorkspace(host);
            ProjectInfo projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "CSharpEditor", "CSharpEditor", LanguageNames.CSharp).WithMetadataReferences(new MetadataReference[] { });
            workspace.AddProject(projectInfo);
            this.Document = workspace.AddDocument(projectInfo.Id, "Source.cs", this.Text);

            MeasureCharacter();
            this.Cursor = new Cursor(StandardCursorType.Ibeam);

            try
            {
                FocusableProperty.OverrideDefaultValue<CSharpSourceEditor>(true);
            }
            catch (InvalidOperationException) { }


            this.GotFocus += (s, e) =>
            {
                if (!this.SearchReplace.HasFocus)
                {
                    this.CaretLayer.IsCaretFocused = true;
                }
            };

            this.LostFocus += (s, e) =>
            {
                this.CaretLayer.IsCaretFocused = false;
            };


            this.SelectionLayer = new CSharpSourceEditorSelection(this);
            this.TextLayer = new CSharpSourceEditorText(this);
            this.CaretLayer = new CSharpSourceEditorCaret(this);
            this.LineNumbersLayer = new CSharpSourceEditorLineNumbers(this);
            this.BreakpointsLayer = new CSharpSourceEditorBreakpoints(this);
            this.VisualChildren.Add(SelectionLayer);
            this.VisualChildren.Add(TextLayer);
            this.VisualChildren.Add(CaretLayer);
            this.VisualChildren.Add(LineNumbersLayer);
            this.VisualChildren.Add(BreakpointsLayer);

            this._hoverTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, HoverTimerElapsed);

            this.SearchReplace = new CSharpSourceEditorSearchReplace(this) { Margin = new Thickness(0, 0, 18, 0) };
            this.LogicalChildren.Add(SearchReplace);
            this.VisualChildren.Add(SearchReplace);

            this.SearchReplace.SearchBox.GotFocus += (s, e) =>
            {
                this.CaretLayer.IsCaretFocused = false;
            };

            this.SearchReplace.ReplaceBox.GotFocus += (s, e) =>
            {
                this.CaretLayer.IsCaretFocused = false;
            };

            this.ScrollBarMarker = new CSharpSourceEditorScrollBarMarker(this);
        }

        internal void InvokeToggleBreakpoint(ToggleBreakpointEventArgs e)
        {
            ToggleBreakpoint?.Invoke(this, e);
        }

        private void HoverTimerElapsed(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            _hovering = true;
            PointerHover?.Invoke(this, _lastHoverEventArgs);
        }

        private void UpdateSearchResults()
        {
            List<SearchSpan> matches = new List<SearchSpan>();

            if (!string.IsNullOrEmpty(this.SearchText) && this.SearchReplace.IsVisible)
            {
                if (!this.IsSearchRegex)
                {
                    string searchString = this.SearchText;
                    foreach (int match in this.Text.ToString().AllIndicesOf(searchString, !this.IsSearchCaseSensitive))
                    {
                        matches.Add(new SearchSpan(new TextSpan(match, searchString.Length)));
                    }
                }
                else
                {
                    try
                    {
                        Regex reg = new Regex(this.SearchText, this.IsSearchCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

                        foreach (Match match in reg.Matches(this.Text.ToString()))
                        {
                            if (match.Success)
                            {
                                matches.Add(new SearchSpan(new TextSpan(match.Index, match.Length), match, reg));
                            }
                        }
                    }
                    catch (ArgumentException)
                    {

                    }
                }
            }

            this.SearchSpans = ImmutableList.Create(matches.ToArray());

            if (this.SearchSpans.Count == 0 && !string.IsNullOrEmpty(this.SearchText))
            {
                this.SearchReplace.SearchBox.Classes.Add("NoMatch");
            }
            else
            {
                this.SearchReplace.SearchBox.Classes.Remove("NoMatch");
            }
        }

        public SearchSpan FindNext()
        {
            SearchSpan matchSpan = null;

            for (int i = 0; i < SearchSpans.Count; i++)
            {
                if (SearchSpans[i].Span.Start >= this.CaretOffset)
                {
                    matchSpan = SearchSpans[i];
                    break;
                }
            }

            if (matchSpan == null)
            {
                if (SearchSpans.Count > 0)
                {
                    matchSpan = SearchSpans[0];
                }
            }

            if (matchSpan != null)
            {
                //this.Focus();
                this.SelectionStart = matchSpan.Span.Start;
                this.SelectionEnd = this.CaretOffset = matchSpan.Span.End;
                this.SelectionLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
            }

            return matchSpan;
        }

        public SearchSpan FindPrevious()
        {
            SearchSpan matchSpan = null;

            for (int i = SearchSpans.Count - 1; i >= 0; i--)
            {
                if (SearchSpans[i].Span.End < this.CaretOffset)
                {
                    matchSpan = SearchSpans[i];
                    break;
                }
            }

            if (matchSpan == null)
            {
                if (SearchSpans.Count > 0)
                {
                    matchSpan = SearchSpans[SearchSpans.Count - 1];
                }
            }

            if (matchSpan != null)
            {
                //this.Focus();
                this.SelectionStart = matchSpan.Span.Start;
                this.SelectionEnd = this.CaretOffset = matchSpan.Span.End;
                this.SelectionLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
            }

            return matchSpan;
        }

        public async Task ReplaceNext()
        {
            if (!this.IsReadOnly)
            {
                this.CaretOffset = this.SelectionStart;
                SearchSpan match = FindNext();

                if (match != null)
                {
                    if (!IsSearchRegex)
                    {
                        await SetSourceText(this.Text.WithChanges(new TextChange(match.Span, this.ReplaceText)));
                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = match.Span.Start + this.ReplaceText.Length;
                    }
                    else
                    {
                        string replacement = match.Match.Result(this.ReplaceText);
                        await SetSourceText(this.Text.WithChanges(new TextChange(match.Span, replacement)));
                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = match.Span.Start + replacement.Length;
                    }
                }
            }
            else
            {
                FindNext();
            }
        }

        public async Task ReplaceAll()
        {
            if (!this.IsReadOnly)
            {
                int pos = -1;

                Document currentDocument = this.Document;

                for (int i = this.SearchSpans.Count - 1; i >= 0; i--)
                {
                    SearchSpan match = this.SearchSpans[i];
                    if (!IsSearchRegex)
                    {
                        await SetSourceText(this.Text.WithChanges(new TextChange(match.Span, this.ReplaceText)), true);

                        if (pos < 0)
                        {
                            pos = match.Span.Start + this.ReplaceText.Length;
                        }
                        else
                        {
                            pos -= match.Span.Length - this.ReplaceText.Length;
                        }
                    }
                    else
                    {
                        string replacement = match.Match.Result(this.ReplaceText);
                        await SetSourceText(this.Text.WithChanges(new TextChange(match.Span, replacement)), true);

                        if (pos < 0)
                        {
                            pos = match.Span.Start + replacement.Length;
                        }
                        else
                        {
                            pos -= match.Span.Length - replacement.Length;
                        }
                    }
                }

                if (pos >= 0)
                {
                    this.SelectionStart = this.SelectionEnd = this.CaretOffset = pos;

                    IEnumerable<TextChange> changes = await currentDocument.GetTextChangesAsync(Document);
                    RedoStack.Clear();
                    UndoStack.Push(changes);
                }
            }
        }

        private void MeasureCharacter()
        {
            Avalonia.Media.FormattedText fmt = new Avalonia.Media.FormattedText() { Text = "m", Typeface = Typeface, FontSize = _fontSize };
            this.CharacterWidth = fmt.Bounds.Width;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int maxWidth = 0;

            foreach (TextLine line in Text.Lines)
            {
                maxWidth = Math.Max(maxWidth, line.End - line.Start);
            }

            return new Size(maxWidth * CharacterWidth + 31 + this.LineNumbersWidth, Text.Lines.Count * LineSpacing * FontSize);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            context.FillRectangle(this.Background, new Rect(0, 0, this.Bounds.Width, this.Bounds.Height));
        }


        protected override Size ArrangeOverride(Size finalSize)
        {
            Viewport = finalSize;
            RaiseScrollInvalidated(new EventArgs());
            return base.ArrangeOverride(finalSize);
        }

        public bool BringIntoView(IControl target, Rect targetRect)
        {
            //throw new NotImplementedException();
            return false;
        }

        public IControl GetControlInDirection(NavigationDirection direction, IControl from)
        {
            return null;
        }

        public void RaiseScrollInvalidated(EventArgs e)
        {
            ScrollInvalidated?.Invoke(this, e);
        }

        internal bool IsPointerPressed = false;
        internal bool IsShiftPressed = false;
        internal int SelectionStart { get; set; } = 0;
        internal int SelectionEnd { get; set; } = 0;


#pragma warning disable CS0618 // Il tipo o il membro è obsoleto
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                Point position = e.GetPosition(this);

                int row = (int)Math.Floor((position.Y + this.Offset.Y) / (this.FontSize * this.LineSpacing));
                int column = Math.Max(0, (int)Math.Round((position.X + this.Offset.X + 2 - (41 + this.LineNumbersWidth)) / this.CharacterWidth));

                row = Math.Max(0, Math.Min(row, this.Text.Lines.Count - 1));

                if (column >= 0 && row >= 0 && row < this.Text.Lines.Count)
                {
                    if (e.ClickCount == 1)
                    {
                        TextLine line = this.Text.Lines[row];

                        column = Math.Min(column, line.End - line.Start);

                        this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(row, column));

                        if (!IsShiftPressed)
                        {
                            this.SelectionStart = this.CaretOffset;
                        }

                        this.SelectionEnd = this.CaretOffset;
                        this.CaretLayer.Show();
                        this.SelectionLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.ClickCount == 2)
                    {
                        TextLine line = this.Text.Lines[row];

                        column = Math.Min(column, line.End - line.Start);
                        int off = this.Text.Lines.GetPosition(new LinePosition(row, column));

                        string textString = this.Text.ToString();

                        Match forwardsMatch = wordBoundaryRegex.Match(textString, off);
                        Match backwardsMatch = wordBoundaryReverseRegex.Match(textString, off);

                        if (forwardsMatch.Success && backwardsMatch.Success)
                        {
                            this.SelectionStart = backwardsMatch.Index;
                            this.CaretOffset = this.SelectionEnd = forwardsMatch.Index;
                        }
                        else
                        {
                            this.CaretOffset = off;
                        }

                        this.SelectionEnd = this.CaretOffset;
                        this.CaretLayer.Show();
                        this.SelectionLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.ClickCount == 3)
                    {
                        TextLine line = this.Text.Lines[row];

                        this.SelectionStart = line.Start;
                        this.CaretOffset = this.SelectionEnd = line.EndIncludingLineBreak;
                        this.CaretLayer.Show();
                        this.SelectionLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }

                    this.IsPointerPressed = true;
                }
            }

            base.OnPointerPressed(e);
        }
#pragma warning restore CS0618 // Il tipo o il membro è obsoleto

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            OnPreviewPointerWheelChanged?.Invoke(this, e);

            if (!e.Handled)
            {
                base.OnPointerWheelChanged(e);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (IsPointerPressed)
            {
                Point position = e.GetPosition(this);

                int row = (int)Math.Floor((position.Y + this.Offset.Y) / (this.FontSize * this.LineSpacing));
                int column = Math.Max(0, (int)Math.Round((position.X + this.Offset.X + 2 - (41 + this.LineNumbersWidth)) / this.CharacterWidth));

                if (column >= 0 && row >= 0 && row < this.Text.Lines.Count)
                {
                    TextLine line = this.Text.Lines[row];

                    column = Math.Min(column, line.End - line.Start);

                    this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(row, column));
                    this.SelectionEnd = this.CaretOffset;
                    this.CaretLayer.Show();
                    this.SelectionLayer.InvalidateVisual();
                    this.ScrollBarMarker.InvalidateVisual();
                }
            }
            else
            {
                Point pos = e.GetPosition(this);
                Point delta = pos - _lastHoverPosition;

                if (Math.Abs(delta.X) > 2 || Math.Abs(delta.Y) > 2)
                {
                    if (_hovering)
                    {
                        _hovering = false;
                        PointerHoverStopped?.Invoke(this, _lastHoverEventArgs);
                    }

                    _lastHoverEventArgs = e;
                    _lastHoverPosition = pos;
                    _hoverTimer.Stop();
                    _hoverTimer.Start();
                }
            }


            base.OnPointerMoved(e);
        }

        protected override void OnPointerEnter(PointerEventArgs e)
        {
            Point pos = e.GetPosition(this);
            if (_hovering)
            {
                _hovering = false;
                PointerHoverStopped?.Invoke(this, _lastHoverEventArgs);
            }

            _lastHoverEventArgs = e;
            _lastHoverPosition = pos;
            _hoverTimer.Stop();
            _hoverTimer.Start();

            base.OnPointerEnter(e);
        }

        protected override void OnPointerLeave(PointerEventArgs e)
        {
            if (_hovering)
            {
                _hovering = false;
                PointerHoverStopped?.Invoke(this, _lastHoverEventArgs);
            }

            _hoverTimer.Stop();

            base.OnPointerLeave(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            IsPointerPressed = false;
            base.OnPointerReleased(e);
        }

        protected override async void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);

            if (!e.Handled && !this.IsReadOnly && !SearchReplace.HasFocus)
            {
                if (e.Text != "\u007f" && e.Text != "\x1b" && e.Text != "\b")
                {
                    await PerformTextInput(e.Text, true);
                }

                e.Handled = true;
            }
        }

        private async Task<bool> PerformTextInput(string text, bool fromTextInput = false)
        {
            TextInputEventArgs e = new TextInputEventArgs() { Text = text };

            if (OnTextEntering != null)
            {
                await OnTextEntering?.Invoke(e);
            }

            if (!e.Handled)
            {
                int selStart = Math.Min(SelectionStart, SelectionEnd);
                int selEnd = Math.Max(SelectionStart, SelectionEnd);

                if (this.OverstrikeMode && selStart == selEnd && fromTextInput)
                {
                    LinePosition pos = this.Text.Lines.GetLinePosition(this.CaretOffset);
                    if (this.CaretOffset < this.Text.Lines[pos.Line].End)
                    {
                        selEnd++;
                    }
                }

                await SetSourceText(this.Text.WithChanges(new TextChange(new TextSpan(selStart, selEnd - selStart), text)));
                this.SelectionStart = this.SelectionEnd = this.CaretOffset = selStart + text.Length;
                this.CaretLayer.Show();

                this.SelectionLayer.InvalidateVisual();
                this.TextLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.LineNumbersLayer.InvalidateVisual();
                this.BreakpointsLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
                this.InvalidateVisual();

                if (OnTextEntered != null)
                {
                    await OnTextEntered?.Invoke(e);
                }
            }

            return e.Handled;
        }

        private async Task NewLine()
        {
            int selStart = Math.Min(SelectionStart, SelectionEnd);

            TextLine line = this.Text.Lines.GetLineFromPosition(selStart);
            string lineText = this.Text.ToString(line.Span);

            int spaceCount = 0;

            while (spaceCount < lineText.Length && char.IsWhiteSpace(lineText[spaceCount]))
            {
                spaceCount++;
            }

            int indentationLevel = (int)Math.Floor((double)spaceCount / Utils.Tab.Length);

            bool handled = await PerformTextInput(Environment.NewLine);

            if (!handled)
            {
                if (lineText.Trim().EndsWith("{"))
                {
                    await PerformTextInput(new string(' ', Utils.Tab.Length * (indentationLevel + 1)));
                }
                else
                {
                    await PerformTextInput(new string(' ', Utils.Tab.Length * indentationLevel));
                }

                if (lineText.TrimStart().StartsWith("/// "))
                {
                    await PerformTextInput("/// ");
                }
                else if (lineText.TrimStart().StartsWith("///"))
                {
                    await PerformTextInput("///");
                }
            }
        }

        private async Task TabForwards()
        {
            if (this.SelectionStart == this.SelectionEnd)
            {
                await this.PerformTextInput(Utils.Tab);
            }
            else
            {
                int selStart = Math.Min(SelectionStart, SelectionEnd);
                int selEnd = Math.Max(SelectionStart, SelectionEnd);

                int firstLine = this.Text.Lines.GetLinePosition(selStart).Line;
                int lastLine = this.Text.Lines.GetLinePosition(selEnd).Line;

                List<TextChange> changes = new List<TextChange>();

                int deltaSelection = 0;

                for (int i = lastLine; i >= firstLine; i--)
                {
                    string lineText = this.Text.ToString(this.Text.Lines[i].Span);

                    if (!string.IsNullOrEmpty(lineText))
                    {
                        int spaceCount = 0;

                        while (spaceCount < lineText.Length && char.IsWhiteSpace(lineText[spaceCount]))
                        {
                            spaceCount++;
                        }

                        int indentationLevel = (int)Math.Floor((double)spaceCount / Utils.Tab.Length);

                        changes.Add(new TextChange(new TextSpan(this.Text.Lines[i].Start, 0), new string(' ', (indentationLevel + 1) * Utils.Tab.Length - spaceCount)));

                        deltaSelection += (indentationLevel + 1) * Utils.Tab.Length - spaceCount;

                        if (i == firstLine)
                        {
                            if (SelectionStart < SelectionEnd)
                            {
                                SelectionStart += (indentationLevel + 1) * Utils.Tab.Length - spaceCount;
                            }
                            else
                            {
                                SelectionEnd += (indentationLevel + 1) * Utils.Tab.Length - spaceCount;
                                CaretOffset = SelectionEnd;
                            }
                        }
                    }
                }

                if (SelectionStart < SelectionEnd)
                {
                    SelectionEnd += deltaSelection;
                    CaretOffset = SelectionEnd;
                }
                else
                {
                    SelectionStart += deltaSelection;
                }

                await this.SetSourceText(Text.WithChanges(changes));
                this.SelectionLayer.InvalidateVisual();
                this.TextLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.LineNumbersLayer.InvalidateVisual();
                this.BreakpointsLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
                this.InvalidateVisual();
            }
        }

        private async Task TabBackwards()
        {
            if (this.SelectionStart != this.SelectionEnd)
            {
                int selStart = Math.Min(SelectionStart, SelectionEnd);
                int selEnd = Math.Max(SelectionStart, SelectionEnd);

                int firstLine = this.Text.Lines.GetLinePosition(selStart).Line;
                int lastLine = this.Text.Lines.GetLinePosition(selEnd).Line;

                List<TextChange> changes = new List<TextChange>();

                int deltaSelection = 0;

                for (int i = lastLine; i >= firstLine; i--)
                {
                    string lineText = this.Text.ToString(this.Text.Lines[i].Span);

                    if (!string.IsNullOrEmpty(lineText))
                    {
                        int spaceCount = 0;

                        while (spaceCount < lineText.Length && char.IsWhiteSpace(lineText[spaceCount]))
                        {
                            spaceCount++;
                        }

                        int indentationLevel = (int)Math.Ceiling((double)spaceCount / Utils.Tab.Length);

                        if (indentationLevel > 0)
                        {
                            changes.Add(new TextChange(new TextSpan(this.Text.Lines[i].Start, Math.Max(0, spaceCount - (indentationLevel - 1) * Utils.Tab.Length)), ""));

                            deltaSelection -= spaceCount - (indentationLevel - 1) * Utils.Tab.Length;

                            if (i == firstLine)
                            {
                                if (SelectionStart < SelectionEnd)
                                {
                                    SelectionStart = Math.Max(this.Text.Lines[i].Start, SelectionStart - (spaceCount - (indentationLevel - 1) * Utils.Tab.Length));
                                }
                                else
                                {
                                    SelectionEnd = Math.Max(this.Text.Lines[i].Start, SelectionEnd - (spaceCount - (indentationLevel - 1) * Utils.Tab.Length));
                                    CaretOffset = SelectionEnd;
                                }
                            }
                        }
                    }
                }

                if (SelectionStart < SelectionEnd)
                {
                    SelectionEnd += deltaSelection;
                    CaretOffset = SelectionEnd;
                }
                else
                {
                    SelectionStart += deltaSelection;
                }

                await this.SetSourceText(Text.WithChanges(changes));
                this.SelectionLayer.InvalidateVisual();
                this.TextLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.LineNumbersLayer.InvalidateVisual();
                this.BreakpointsLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
                this.InvalidateVisual();
            }
        }

        private async Task DeleteBackwards()
        {
            int selStart = Math.Min(SelectionStart, SelectionEnd);
            int selEnd = Math.Max(SelectionStart, SelectionEnd);

            selStart = Math.Min(selStart, Math.Max(0, selEnd - 1));

            TextSpan deleteSpan = new TextSpan(selStart, selEnd - selStart);

            if (selEnd - selStart == 1)
            {
                TextSpan deleteSpanPlusOne = new TextSpan(selStart, Math.Min(selEnd + 1, this.Text.Length) - selStart);
                TextSpan deleteSpanMinusOne = new TextSpan(Math.Max(0, selStart - 1), selEnd - Math.Max(0, selStart - 1));

                if (this.Text.ToString(deleteSpan) == "\r" && this.Text.ToString(deleteSpanPlusOne) == "\r\n")
                {
                    deleteSpan = deleteSpanPlusOne;
                }
                if (this.Text.ToString(deleteSpan) == "\n" && this.Text.ToString(deleteSpanMinusOne) == "\r\n")
                {
                    deleteSpan = deleteSpanMinusOne;
                    selStart = Math.Max(0, selStart - 1);
                }
            }

            await SetSourceText(this.Text.WithChanges(new TextChange(deleteSpan, "")));
            this.SelectionStart = this.SelectionEnd = this.CaretOffset = selStart;
            this.CaretLayer.Show();

            this.SelectionLayer.InvalidateVisual();
            this.TextLayer.InvalidateVisual();
            this.CaretLayer.InvalidateVisual();
            this.LineNumbersLayer.InvalidateVisual();
            this.BreakpointsLayer.InvalidateVisual();
            this.ScrollBarMarker.InvalidateVisual();
            this.InvalidateVisual();
        }

        private async Task DeleteForwards()
        {
            int selStart = Math.Min(SelectionStart, SelectionEnd);
            int selEnd = Math.Max(SelectionStart, SelectionEnd);

            selEnd = Math.Max(selEnd, Math.Min(selStart + 1, this.Text.Length));

            TextSpan deleteSpan = new TextSpan(selStart, selEnd - selStart);

            if (selEnd - selStart == 1)
            {
                TextSpan deleteSpanPlusOne = new TextSpan(selStart, Math.Min(selEnd + 1, this.Text.Length) - selStart);
                TextSpan deleteSpanMinusOne = new TextSpan(Math.Max(0, selStart - 1), selEnd - Math.Max(0, selStart - 1));

                if (this.Text.ToString(deleteSpan) == "\r" && this.Text.ToString(deleteSpanPlusOne) == "\r\n")
                {
                    deleteSpan = deleteSpanPlusOne;
                }
                if (this.Text.ToString(deleteSpan) == "\n" && this.Text.ToString(deleteSpanMinusOne) == "\r\n")
                {
                    deleteSpan = deleteSpanMinusOne;
                }
            }


            await SetSourceText(this.Text.WithChanges(new TextChange(deleteSpan, "")));
            this.SelectionStart = this.SelectionEnd = this.CaretOffset = selStart;
            this.CaretLayer.Show();

            this.SelectionLayer.InvalidateVisual();
            this.TextLayer.InvalidateVisual();
            this.CaretLayer.InvalidateVisual();
            this.LineNumbersLayer.InvalidateVisual();
            this.BreakpointsLayer.InvalidateVisual();
            this.ScrollBarMarker.InvalidateVisual();
            this.InvalidateVisual();
        }

        internal void ClearUndoStack()
        {
            UndoStack.Clear();
            RedoStack.Clear();
        }

        private async Task Undo()
        {
            if (UndoStack.Count > 0)
            {
                IEnumerable<TextChange> pop = UndoStack.Pop();

                Document currentDocument = Document;
                SourceText previousText = Text.WithChanges(pop);
                await SetSourceText(previousText, true);

                RedoStack.Push(await currentDocument.GetTextChangesAsync(Document));

                TextChange? lastChange = pop.LastOrNull();

                if (lastChange != null)
                {
                    this.SelectionStart = this.SelectionEnd = this.CaretOffset = lastChange.Value.Span.Start + lastChange.Value.NewText.Length;
                }

                this.SelectionLayer.InvalidateVisual();
                this.TextLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.LineNumbersLayer.InvalidateVisual();
                this.BreakpointsLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
                this.InvalidateVisual();
            }
        }

        private async Task Redo()
        {
            if (RedoStack.Count > 0)
            {
                IEnumerable<TextChange> pop = RedoStack.Pop();

                Document currentDocument = Document;
                SourceText previousText = Text.WithChanges(pop);
                await SetSourceText(previousText, true);
                UndoStack.Push(await currentDocument.GetTextChangesAsync(Document));

                TextChange? lastChange = pop.LastOrNull();
                if (lastChange != null)
                {
                    this.SelectionStart = this.SelectionEnd = this.CaretOffset = lastChange.Value.Span.Start + lastChange.Value.NewText.Length;
                }

                this.SelectionLayer.InvalidateVisual();
                this.TextLayer.InvalidateVisual();
                this.CaretLayer.InvalidateVisual();
                this.LineNumbersLayer.InvalidateVisual();
                this.BreakpointsLayer.InvalidateVisual();
                this.ScrollBarMarker.InvalidateVisual();
                this.InvalidateVisual();
            }
        }

        private int _verticalMovementStartColumn = -1;
        static readonly Regex wordBoundaryRegex = new Regex("\\b");
        static readonly Regex wordBoundaryReverseRegex = new Regex("\\b", RegexOptions.RightToLeft);

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (OnPreviewKeyDown != null)
            {
                await OnPreviewKeyDown?.Invoke(e);
            }

            if (!e.Handled)
            {

                if (e.Key != Key.Up && e.Key != Key.Down && e.Key != Key.PageDown && e.Key != Key.PageUp)
                {
                    _verticalMovementStartColumn = -1;
                }

                if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
                {
                    IsShiftPressed = true;
                }
                else if (e.Key == Key.Enter && !this.IsReadOnly && !SearchReplace.HasFocus)
                {
                    await NewLine();
                    e.Handled = true;
                }
                else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && !this.IsReadOnly && !SearchReplace.HasFocus)
                {
                    await TabForwards();
                    e.Handled = true;
                }
                else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift && !this.IsReadOnly && !SearchReplace.HasFocus)
                {
                    await TabBackwards();
                    e.Handled = true;
                }
                else if (e.Key == Key.Back && !this.IsReadOnly && !SearchReplace.HasFocus)
                {
                    await DeleteBackwards();
                    e.Handled = true;
                }
                else if (e.Key == Key.Delete && !this.IsReadOnly && !SearchReplace.HasFocus)
                {
                    await DeleteForwards();
                    e.Handled = true;
                }
                else if (e.Key == Key.Z && e.KeyModifiers == Utils.ControlCmdModifier && !this.IsReadOnly && !SearchReplace.HasFocus)
                {
                    await Undo();
                    e.Handled = true;
                }
                else if (e.Key == Key.Y && e.KeyModifiers == Utils.ControlCmdModifier && !this.IsReadOnly && !SearchReplace.HasFocus)
                {
                    await Redo();
                    e.Handled = true;
                }
                else if (e.Key == Key.F && e.KeyModifiers == Utils.ControlCmdModifier)
                {
                    this.SearchReplace.IsVisible = true;
                    this.SearchReplace.ReplaceToggle.IsChecked = false;

                    if (this.SelectionEnd != this.SelectionStart)
                    {
                        this.SearchReplace.SearchBox.Text = this.Text.ToString(new TextSpan(Math.Min(this.SelectionStart, this.SelectionEnd), Math.Abs(this.SelectionStart - this.SelectionEnd))) ?? "";
                    }

                    this.SearchReplace.SearchBox.SelectionStart = 0;
                    this.SearchReplace.SearchBox.SelectionEnd = this.SearchReplace.SearchBox.Text?.Length ?? 0;

                    UpdateSearchResults();
                    await Dispatcher.UIThread.InvokeAsync(() => { this.SearchReplace.SearchBox.Focus(); }, DispatcherPriority.Layout);
                    e.Handled = true;
                }
                else if ((e.Key == Key.G || e.Key == Key.H) && e.KeyModifiers == Utils.ControlCmdModifier)
                {
                    this.SearchReplace.IsVisible = true;
                    this.SearchReplace.ReplaceToggle.IsChecked = true;
                    UpdateSearchResults();
                    await Dispatcher.UIThread.InvokeAsync(() => { this.SearchReplace.SearchBox.Focus(); }, DispatcherPriority.Layout);
                    e.Handled = true;
                }
                else if (e.Key == Key.F3 && e.KeyModifiers == KeyModifiers.None)
                {
                    FindNext();
                }
                else if (e.Key == Key.F3 && e.KeyModifiers == KeyModifiers.Shift)
                {
                    FindPrevious();
                }
                else if (e.Key == Key.Escape)
                {
                    if (this.SearchReplace.IsVisible)
                    {
                        this.SearchReplace.IsVisible = false;
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Right)
                {
                    if (e.KeyModifiers == KeyModifiers.None)
                    {
                        int targetPos = Math.Min(this.CaretOffset + 1, this.Text.Length);

                        if (targetPos < this.Text.Length)
                        {
                            string charAtPos = this.Text.ToString(new TextSpan(targetPos, 1));
                            string charAtPrevvPos = this.Text.ToString(new TextSpan(targetPos - 1, 1));

                            if (charAtPos == "\n" && charAtPrevvPos == "\r")
                            {
                                targetPos++;
                            }
                        }

                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = targetPos;
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == KeyModifiers.Shift)
                    {
                        int targetPos = Math.Min(this.CaretOffset + 1, this.Text.Length);

                        if (targetPos < this.Text.Length)
                        {
                            string charAtPos = this.Text.ToString(new TextSpan(targetPos, 1));
                            string charAtPrevvPos = this.Text.ToString(new TextSpan(targetPos - 1, 1));

                            if (charAtPos == "\n" && charAtPrevvPos == "\r")
                            {
                                targetPos++;
                            }
                        }

                        this.SelectionEnd = this.CaretOffset = targetPos;
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == Utils.ControlCmdModifier)
                    {
                        if (this.CaretOffset < this.Text.Length - 1)
                        {
                            Match match = wordBoundaryRegex.Match(this.Text.ToString(), this.CaretOffset + 1);
                            if (match.Success)
                            {
                                this.SelectionStart = this.SelectionEnd = this.CaretOffset = Math.Min(match.Index, this.Text.Length);
                                this.SelectionLayer.InvalidateVisual();
                                this.CaretLayer.Show();
                                this.CaretLayer.InvalidateVisual();
                                this.ScrollBarMarker.InvalidateVisual();
                            }
                        }
                        e.Handled = true;
                    }
                    else if (e.KeyModifiers == (Utils.ControlCmdModifier | KeyModifiers.Shift))
                    {
                        if (this.CaretOffset < this.Text.Length - 1)
                        {
                            Match match = wordBoundaryRegex.Match(this.Text.ToString(), this.CaretOffset + 1);
                            if (match.Success)
                            {
                                this.SelectionEnd = this.CaretOffset = Math.Min(match.Index, this.Text.Length);
                                this.SelectionLayer.InvalidateVisual();
                                this.CaretLayer.Show();
                                this.CaretLayer.InvalidateVisual();
                                this.ScrollBarMarker.InvalidateVisual();
                            }
                        }
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Left)
                {
                    if (e.KeyModifiers == KeyModifiers.None)
                    {
                        int targetPos = Math.Max(this.CaretOffset - 1, 0);

                        if (targetPos > 0)
                        {
                            string charAtPos = this.Text.ToString(new TextSpan(targetPos, 1));
                            string charAtPrevvPos = this.Text.ToString(new TextSpan(targetPos - 1, 1));

                            if (charAtPos == "\n" && charAtPrevvPos == "\r")
                            {
                                targetPos--;
                            }
                        }

                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = targetPos;
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == KeyModifiers.Shift)
                    {
                        int targetPos = Math.Max(this.CaretOffset - 1, 0);

                        if (targetPos > 0)
                        {
                            string charAtPos = this.Text.ToString(new TextSpan(targetPos, 1));
                            string charAtPrevvPos = this.Text.ToString(new TextSpan(targetPos - 1, 1));

                            if (charAtPos == "\n" && charAtPrevvPos == "\r")
                            {
                                targetPos--;
                            }
                        }

                        this.SelectionEnd = this.CaretOffset = targetPos;
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == Utils.ControlCmdModifier)
                    {
                        if (this.CaretOffset > 0)
                        {
                            Match match = wordBoundaryReverseRegex.Match(this.Text.ToString(), this.CaretOffset - 1);
                            if (match.Success)
                            {
                                this.SelectionStart = this.SelectionEnd = this.CaretOffset = Math.Min(match.Index, this.Text.Length);
                                this.SelectionLayer.InvalidateVisual();
                                this.CaretLayer.Show();
                                this.CaretLayer.InvalidateVisual();
                                this.ScrollBarMarker.InvalidateVisual();
                            }
                        }
                        e.Handled = true;
                    }
                    else if (e.KeyModifiers == (Utils.ControlCmdModifier | KeyModifiers.Shift))
                    {
                        if (this.CaretOffset > 0)
                        {
                            Match match = wordBoundaryReverseRegex.Match(this.Text.ToString(), this.CaretOffset - 1);
                            if (match.Success)
                            {
                                this.SelectionEnd = this.CaretOffset = Math.Min(match.Index, this.Text.Length);
                                this.SelectionLayer.InvalidateVisual();
                                this.CaretLayer.Show();
                                this.CaretLayer.InvalidateVisual();
                                this.ScrollBarMarker.InvalidateVisual();
                            }
                        }
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Down)
                {
                    LinePosition pos = this.Text.Lines.GetLinePosition(this.CaretOffset);

                    if (_verticalMovementStartColumn < 0)
                    {
                        _verticalMovementStartColumn = pos.Character;
                    }

                    int newLine = Math.Min(this.Text.Lines.Count - 1, pos.Line + 1);

                    int column = Math.Min(_verticalMovementStartColumn, this.Text.Lines[newLine].End - this.Text.Lines[newLine].Start);

                    if (e.KeyModifiers == KeyModifiers.None)
                    {
                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == KeyModifiers.Shift)
                    {
                        this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }

                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    LinePosition pos = this.Text.Lines.GetLinePosition(this.CaretOffset);

                    if (_verticalMovementStartColumn < 0)
                    {
                        _verticalMovementStartColumn = pos.Character;
                    }

                    int newLine = Math.Max(0, pos.Line - 1);

                    int column = Math.Min(_verticalMovementStartColumn, this.Text.Lines[newLine].End - this.Text.Lines[newLine].Start);

                    if (e.KeyModifiers == KeyModifiers.None)
                    {
                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == KeyModifiers.Shift)
                    {
                        this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }

                    e.Handled = true;
                }
                else if (e.Key == Key.PageDown)
                {
                    int rowNum = (int)Math.Floor(this.Bounds.Height / (this.FontSize * this.LineSpacing));

                    LinePosition pos = this.Text.Lines.GetLinePosition(this.CaretOffset);

                    if (_verticalMovementStartColumn < 0)
                    {
                        _verticalMovementStartColumn = pos.Character;
                    }

                    int newLine = Math.Min(this.Text.Lines.Count - 1, pos.Line + rowNum);

                    int column = Math.Min(_verticalMovementStartColumn, this.Text.Lines[newLine].End - this.Text.Lines[newLine].Start);

                    if (e.KeyModifiers == KeyModifiers.None)
                    {
                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == KeyModifiers.Shift)
                    {
                        this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }

                    e.Handled = true;
                }
                else if (e.Key == Key.PageUp)
                {
                    int rowNum = (int)Math.Floor(this.Bounds.Height / (this.FontSize * this.LineSpacing));

                    LinePosition pos = this.Text.Lines.GetLinePosition(this.CaretOffset);

                    if (_verticalMovementStartColumn < 0)
                    {
                        _verticalMovementStartColumn = pos.Character;
                    }

                    int newLine = Math.Max(0, pos.Line - rowNum);

                    int column = Math.Min(_verticalMovementStartColumn, this.Text.Lines[newLine].End - this.Text.Lines[newLine].Start);

                    if (e.KeyModifiers == KeyModifiers.None)
                    {
                        this.SelectionStart = this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }
                    else if (e.KeyModifiers == KeyModifiers.Shift)
                    {
                        this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(newLine, column));
                        e.Handled = true;
                        this.SelectionLayer.InvalidateVisual();
                        this.CaretLayer.Show();
                        this.CaretLayer.InvalidateVisual();
                        this.ScrollBarMarker.InvalidateVisual();
                    }

                    e.Handled = true;
                }
                else if (e.Key == Key.End)
                {
                    if (!e.KeyModifiers.HasFlag(Utils.ControlCmdModifier))
                    {
                        LinePosition pos = this.Text.Lines.GetLinePosition(this.CaretOffset);

                        int column = this.Text.Lines[pos.Line].End - this.Text.Lines[pos.Line].Start;

                        if (e.KeyModifiers == KeyModifiers.None)
                        {
                            this.SelectionStart = this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(pos.Line, column));
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                        else if (e.KeyModifiers == KeyModifiers.Shift)
                        {
                            this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(pos.Line, column));
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                    }
                    else
                    {
                        if (e.KeyModifiers == Utils.ControlCmdModifier)
                        {
                            this.SelectionStart = this.SelectionEnd = this.CaretOffset = this.Text.Length;
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                        else if (e.KeyModifiers == (KeyModifiers.Shift | Utils.ControlCmdModifier))
                        {
                            this.SelectionEnd = this.CaretOffset = this.Text.Length;
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                    }

                    e.Handled = true;
                }
                else if (e.Key == Key.Home)
                {
                    if (!e.KeyModifiers.HasFlag(Utils.ControlCmdModifier))
                    {
                        LinePosition pos = this.Text.Lines.GetLinePosition(this.CaretOffset);

                        string lineText = this.Text.ToString(this.Text.Lines[pos.Line].Span);

                        int indentation = lineText.Length - lineText.TrimStart().Length;

                        int column = indentation;

                        if (pos.Character <= indentation && pos.Character > 0)
                        {
                            column = 0;
                        }

                        if (e.KeyModifiers == KeyModifiers.None)
                        {
                            this.SelectionStart = this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(pos.Line, column));
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                        else if (e.KeyModifiers == KeyModifiers.Shift)
                        {
                            this.SelectionEnd = this.CaretOffset = this.Text.Lines.GetPosition(new LinePosition(pos.Line, column));
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                    }
                    else
                    {
                        if (e.KeyModifiers == Utils.ControlCmdModifier)
                        {
                            this.SelectionStart = this.SelectionEnd = this.CaretOffset = 0;
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                        else if (e.KeyModifiers == (KeyModifiers.Shift | Utils.ControlCmdModifier))
                        {
                            this.SelectionEnd = this.CaretOffset = 0;
                            e.Handled = true;
                            this.SelectionLayer.InvalidateVisual();
                            this.CaretLayer.Show();
                            this.CaretLayer.InvalidateVisual();
                            this.ScrollBarMarker.InvalidateVisual();
                        }
                    }

                    e.Handled = true;
                }
                else if (e.Key == Key.A && e.KeyModifiers == Utils.ControlCmdModifier)
                {
                    this.SelectionStart = 0;
                    this.SelectionEnd = this.CaretOffset = this.Text.Length;
                    e.Handled = true;
                    this.SelectionLayer.InvalidateVisual();
                    this.CaretLayer.Show();
                    this.CaretLayer.InvalidateVisual();
                    this.ScrollBarMarker.InvalidateVisual();
                }
                else if ((e.Key == Key.C && e.KeyModifiers == Utils.ControlCmdModifier) || (e.Key == Key.Insert && e.KeyModifiers == Utils.ControlCmdModifier))
                {
                    if (this.SelectionStart != this.SelectionEnd)
                    {
                        int selStart = Math.Min(this.SelectionStart, this.SelectionEnd);
                        int selEnd = Math.Max(this.SelectionStart, this.SelectionEnd);

                        await Application.Current.Clipboard.SetTextAsync(this.Text.ToString(new TextSpan(selStart, selEnd - selStart)));
                    }
                    e.Handled = true;
                }
                else if (((e.Key == Key.V && e.KeyModifiers == Utils.ControlCmdModifier) || (e.Key == Key.Insert && e.KeyModifiers == KeyModifiers.Shift)) && !this.IsReadOnly)
                {
                    string text = await Application.Current.Clipboard.GetTextAsync();

                    if (text != null)
                    {
                        await this.PerformTextInput(text);
                        this.OnPaste?.Invoke(this, new PasteEventArgs(text));
                    }

                    e.Handled = true;
                }
                else if (((e.Key == Key.X && e.KeyModifiers == Utils.ControlCmdModifier) || (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.Shift)) && !this.IsReadOnly)
                {
                    if (this.SelectionStart != this.SelectionEnd)
                    {
                        int selStart = Math.Min(this.SelectionStart, this.SelectionEnd);
                        int selEnd = Math.Max(this.SelectionStart, this.SelectionEnd);

                        await Application.Current.Clipboard.SetTextAsync(this.Text.ToString(new TextSpan(selStart, selEnd - selStart)));

                        await this.DeleteBackwards();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Insert && e.KeyModifiers == KeyModifiers.None && !this.IsReadOnly)
                {
                    this.OverstrikeMode = !this.OverstrikeMode;

                    this.CaretLayer.InvalidateVisual();
                    this.ScrollBarMarker.InvalidateVisual();
                    e.Handled = true;
                }

                base.OnKeyDown(e);
            }
        }

        protected override async void OnKeyUp(KeyEventArgs e)
        {
            if (OnPreviewKeyUp != null)
            {
                await OnPreviewKeyUp?.Invoke(e);
            }

            if (!e.Handled)
            {
                if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
                {
                    IsShiftPressed = false;
                }

                base.OnKeyUp(e);
            }
        }

        internal SourceText ReplaceTabsWithSpaces(SourceText text)
        {
            string textString = text.ToString();
            IEnumerable<TextChange> changes = (from el in textString.AllIndicesOf("\t").ToArray() orderby el descending select new TextChange(new TextSpan(el, 1), Utils.Tab));

            return text.WithChanges(changes);
        }

        public void ScrollToCaret()
        {
            LinePosition pos = this.Text.Lines.GetLinePosition(Math.Max(0, Math.Min(this.CaretOffset, this.Text.Length)));
            double caretTop = pos.Line * (this.FontSize * this.LineSpacing);
            double caretBottom = (pos.Line + 1) * (this.FontSize * this.LineSpacing);
            double caretX = pos.Character * this.CharacterWidth + 41 + this.LineNumbersWidth;

            double oY = this.Offset.Y;

            if (caretBottom - this.Offset.Y > this.Bounds.Height - 20)
            {
                oY = caretBottom - this.Bounds.Height + 20;
            }

            if (caretTop - this.Offset.Y < 0)
            {
                oY = caretTop;
            }

            double oX = this.Offset.X;

            if (this.Bounds.Height >= this.Extent.Height && caretX - this.Offset.X > this.Bounds.Width - 1)
            {
                oX = caretX - this.Bounds.Width + 1 + 5;
            }
            else if (this.Bounds.Height < this.Extent.Height && caretX - this.Offset.X > this.Bounds.Width - 1 - 20)
            {
                oX = caretX - this.Bounds.Width + 1 + 20;
            }

            if (caretX - this.Offset.X < 41 + this.LineNumbersWidth)
            {
                oX = caretX - (41 + this.LineNumbersWidth);
            }

            this.Offset = new Vector(oX, oY);
            this.RaiseScrollInvalidated(new EventArgs());
        }
    }

    internal class ToggleBreakpointEventArgs : EventArgs
    {
        public int LineNumber { get; }
        public int LineStart { get; }
        public int LineEnd { get; }

        public ToggleBreakpointEventArgs(int lineNumber, int lineStart, int lineEnd) : base()
        {
            this.LineNumber = lineNumber;
            this.LineStart = lineStart;
            this.LineEnd = lineEnd;
        }
    }

    internal class PasteEventArgs : EventArgs
    {
        public string PastedText { get; }

        public PasteEventArgs(string pastedText)
        {
            this.PastedText = pastedText;
        }

    }

    internal class SearchSpan
    {
        public TextSpan Span { get; }
        public Match Match { get; }
        public Regex Regex { get; }

        public SearchSpan(TextSpan span, Match match = null, Regex regex = null)
        {
            this.Span = span;
            this.Match = match;
            this.Regex = regex;
        }
    }
}