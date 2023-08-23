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
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DiffPlex;
using DiffPlex.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace CSharpEditor
{
    public partial class Editor : UserControl
    {
        #region Static
        internal static VectSharp.FontFamily RobotoMonoRegular;
        internal static VectSharp.FontFamily OpenSansRegular;
        internal static VectSharp.FontFamily OpenSansBold;
        internal static VectSharp.FontFamily OpenSansItalic;
        internal static VectSharp.FontFamily OpenSansBoldItalic;

        static Editor()
        {
            RobotoMonoRegular = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("CSharpEditor.Fonts.RobotoMono-Regular.ttf"), "resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Roboto Mono");

            OpenSansRegular = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("CSharpEditor.Fonts.OpenSans-Regular.ttf"), "resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Open Sans");
            OpenSansBold = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("CSharpEditor.Fonts.OpenSans-Bold.ttf"), "resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Open Sans");

            OpenSansItalic = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("CSharpEditor.Fonts.OpenSans-Italic.ttf"), "resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Open Sans");
            OpenSansBoldItalic = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("CSharpEditor.Fonts.OpenSans-BoldItalic.ttf"), "resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Open Sans");
        }
        #endregion

        #region Internal fields
        internal InputHandler InputHandler;
        internal SettingsContainer SettingsContainer;

        internal AutoSaver AutoSaver;
        internal CompilationErrorChecker CompilationErrorChecker;

        internal Document OriginalDocument;
        internal string LastSavedText;
        internal string OriginalText;
        internal long OriginalTimeStamp;

        #endregion

        #region Private fields
        private bool IsBottomPanelOpen = false;
        private double PreviousBottomPanelHeight = double.NaN;

        private bool IsSidePanelOpen = false;
        private double PreviousSidePanelWidth = double.NaN;
        #endregion

        #region Internal properties
        internal double CharacterWidth => EditorControl.CharacterWidth;

        internal SourceText PreSourceText { get; private set; } = SourceText.From("");

        internal object ReferencesLock = new object();
        internal async Task SetReferences(ImmutableList<MetadataReference> references, bool recreateList = true)
        {
            lock (ReferencesLock)
            {
                References = references;
            }

            if (recreateList)
            {
                ReferencesContainer.RecreateList(References);
            }

            await this.EditorControl.SetDocument(this.EditorControl.Document.Project.WithMetadataReferences(References).Documents.First());
        }

        internal Differ Differ { get; } = new Differ();
        #endregion

        internal enum BreakpointToggleResult
        {
            Added,
            Removed,
            InvalidPosition
        }

        /// <summary>
        /// Public constructor. This is only provided for compatibility with Avalonia (<a href="https://github.com/AvaloniaUI/Avalonia/issues/2593">see issue #2593</a>). Please use <see cref="Editor.Create"/> instead.
        /// </summary>
        [Obsolete("Please use the Editor.Create() static method instead", true)]
        public Editor()
        {
            this.InitializeComponent();
        }

        private Editor(bool _)
        {
            this.InitializeComponent();
        }

        private async Task Initialize(string sourceText, string preSource, string postSource, IEnumerable<CachedMetadataReference> references, CSharpCompilationOptions compilationOptions, string guid, Shortcut[] additionalShortcuts)
        {
            this.CompilationOptions = compilationOptions;
            this.Guid = guid;

            EditorControl = this.FindControl<CSharpSourceEditorControl>("EditorControl");

            await EditorControl.SetText(sourceText);

            this.PreSource = preSource;
            this.PostSource = postSource;

            OriginalText = EditorControl.Text.ToString();
            OriginalDocument = EditorControl.Document;
            LastSavedText = OriginalText;
            OriginalTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CompletionService service = CompletionService.GetService(OriginalDocument);

            this.FindControl<CompletionWindow>("CompletionWindow").Document = OriginalDocument;
            this.FindControl<CompletionWindow>("CompletionWindow").CompletionService = service;

            this.CompletionWindow = this.FindControl<CompletionWindow>("CompletionWindow");
            this.MethodOverloadList = this.FindControl<MethodOverloadList>("MethodOverloadList");

            this.FindControl<MethodOverloadList>("MethodOverloadList").Document = OriginalDocument;

            this.FindControl<CompletionWindow>("CompletionWindow").Committed += CompletionCommitted;

            StatusBar = this.FindControl<StatusBar>("StatusBar");

            ErrorContainer = this.FindControl<ErrorContainer>("ErrorContainer");
            ReferencesContainer = this.FindControl<ReferencesContainer>("ReferencesContainer");
            SaveHistoryContainer = this.FindControl<SaveHistoryContainer>("SaveHistoryContainer");
            SettingsContainer = new SettingsContainer(additionalShortcuts) { Margin = new Thickness(10, 0, 0, 10), IsVisible = false };
            Grid.SetRow(SettingsContainer, 2);
            this.FindControl<Grid>("ContainerGrid").Children.Add(SettingsContainer);



            await this.SetReferences(ImmutableList.Create((from el in references select (MetadataReference)el).ToArray()));

            EditorControl.ClearUndoStack();

            this.CompilationErrorChecker = CompilationErrorChecker.Attach(this);

            this.SymbolToolTip = this.FindControl<SymbolToolTip>("SymbolToolTip");
            this.SymbolToolTip.References = References;

            this.BreakpointPanel = this.FindControl<BreakpointPanel>("BreakpointPanel");

            string autosaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly().GetName().Name);
            Directory.CreateDirectory(Path.Combine(autosaveDirectory, Guid));
            AutoSaveFile = Path.Combine(autosaveDirectory, Guid, "autosave_" + System.Guid.NewGuid().ToString("N") + ".cs");
            SaveDirectory = Path.Combine(autosaveDirectory, Guid);
            this.AutoSaver = AutoSaver.Start(this, AutoSaveFile);

            InputHandler = new InputHandler(this, EditorControl, this.FindControl<CompletionWindow>("CompletionWindow"), this.FindControl<MethodOverloadList>("MethodOverloadList"), service);
            EditorControl.ToggleBreakpoint += async (s, e) =>
            {
                await this.TryToggleBreakpoint(e.LineStart, e.LineEnd);
            };
        }

        #region Internal methods

        internal void CloseBottomPanel()
        {
            if (IsBottomPanelOpen)
            {
                IsBottomPanelOpen = false;

                PreviousBottomPanelHeight = this.FindControl<Grid>("ContainerGrid").RowDefinitions[2].Height.Value;
                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(10, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("GridSplitter").IsVisible = false;
            }
        }

        internal void OpenBottomPanel()
        {
            if (!IsBottomPanelOpen)
            {
                IsBottomPanelOpen = true;

                double height = PreviousBottomPanelHeight;
                if (double.IsNaN(height))
                {
                    if (!double.IsNaN(this.Bounds.Width) && !double.IsNaN(this.Bounds.Height) && this.Bounds.Width > 0 && this.Bounds.Height > 0)
                    {
                        PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                        PreviousSidePanelWidth = Math.Min(400, this.Bounds.Width / 3);
                        height = PreviousBottomPanelHeight;
                    }
                    else
                    {
                        height = 250;
                        void layoutHandler(object s, EventArgs e)
                        {
                            if (double.IsNaN(PreviousBottomPanelHeight))
                            {
                                PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                                PreviousSidePanelWidth = Math.Min(400, this.Bounds.Width / 3);
                                this.LayoutUpdated -= layoutHandler;
                            }

                            if (IsSidePanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(PreviousSidePanelWidth, GridUnitType.Pixel);
                            }

                            if (IsBottomPanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(PreviousBottomPanelHeight, GridUnitType.Pixel);
                            }
                        }

                        this.LayoutUpdated += layoutHandler;
                    }
                }

                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(height, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("GridSplitter").IsVisible = true;
            }
        }

        internal void CloseSidePanel()
        {
            if (IsSidePanelOpen)
            {
                IsSidePanelOpen = false;

                BreakpointPanel.IsVisible = false;
                PreviousSidePanelWidth = this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2].Width.Value;
                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(10, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("VerticalGridSplitter").IsVisible = false;
            }
        }

        internal void OpenSidePanel()
        {
            if (!IsSidePanelOpen)
            {
                IsSidePanelOpen = true;

                double width = PreviousSidePanelWidth;
                if (double.IsNaN(width))
                {
                    if (!double.IsNaN(this.Bounds.Width) && !double.IsNaN(this.Bounds.Height) && this.Bounds.Width > 0 && this.Bounds.Height > 0)
                    {
                        PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                        PreviousSidePanelWidth = Math.Min(400, this.Bounds.Width / 3);
                        width = PreviousSidePanelWidth;
                    }
                    else
                    {
                        width = 400;
                        void layoutHandler(object s, EventArgs e)
                        {
                            if (double.IsNaN(PreviousBottomPanelHeight))
                            {
                                PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                                PreviousSidePanelWidth = Math.Min(400, this.Bounds.Width / 3);
                                this.LayoutUpdated -= layoutHandler;
                            }

                            if (IsSidePanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(PreviousSidePanelWidth, GridUnitType.Pixel);
                            }

                            if (IsBottomPanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(PreviousBottomPanelHeight, GridUnitType.Pixel);
                            }
                        }

                        this.LayoutUpdated += layoutHandler;
                    }
                }

                BreakpointPanel.IsVisible = true;
                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(width, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("VerticalGridSplitter").IsVisible = true;
            }
        }

        internal void SetLineDiff(IEnumerable<int> changesFromLastSave, IEnumerable<int> changesFromOriginal)
        {
            List<int> yellowLines = changesFromLastSave.ToList();

            IEnumerable<HighlightedLineRange> greenLineSpans = from el in (from el in changesFromOriginal where !yellowLines.Contains(el) select new TextSpan(el, 0)).Join() select new HighlightedLineRange(el, new SolidColorBrush(Color.FromRgb(108, 226, 108)));
            IEnumerable<HighlightedLineRange> yellowLineSpans = from el in (from el in yellowLines select new TextSpan(el, 0)).Join() select new HighlightedLineRange(el, new SolidColorBrush(Color.FromRgb(255, 238, 98)));

            EditorControl.HighlightedLines = ImmutableList.Create(greenLineSpans.Concat(yellowLineSpans).ToArray());
        }

        internal void UpdateLastSavedDocument()
        {
            this.LastSavedText = EditorControl.Text.ToString();

            if (EditorControl.ShowLineChanges)
            {
                DiffResult diffResult = Differ.CreateLineDiffs(this.PreSource + "\n" + this.OriginalText + "\n" + this.PostSource, this.PreSource + "\n" + this.LastSavedText + "\n" + this.PostSource, false);
                IEnumerable<int> changesFromOriginal = (from el in diffResult.DiffBlocks select Enumerable.Range(el.InsertStartB - this.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                SetLineDiff(new int[0], changesFromOriginal);
            }
        }

        internal void InvokeSaveRequested(SaveEventArgs e)
        {
            SaveRequested?.Invoke(this, e);
        }

        internal void InvokeAutosave(SaveEventArgs e)
        {
            Autosave?.Invoke(this, e);
        }

        internal void InvokeCompilationCompleted(CompilationEventArgs e)
        {
            CompilationCompleted?.Invoke(this, e);
        }

        internal async Task<BreakpointToggleResult> TryToggleBreakpoint(int lineStart, int lineEnd)
        {
            //string line = AvaloniaEditor.TextArea.Document.GetText(lineStart, lineEnd - lineStart);
            string line = EditorControl.Text.ToString(new TextSpan(lineStart, lineEnd - lineStart));

            if (line.Contains(Utils.BreakpointMarker))
            {
                line = line.Replace(Utils.BreakpointMarker, "");
                if (string.IsNullOrWhiteSpace(line))
                {
                    await EditorControl.SetText(EditorControl.Text.WithChanges(new TextChange(new TextSpan(lineStart, lineEnd - lineStart + Environment.NewLine.Length), "")));
                }
                else
                {
                    await EditorControl.SetText(EditorControl.Text.WithChanges(new TextChange(new TextSpan(lineStart, lineEnd - lineStart + Environment.NewLine.Length), line)));
                }
                return BreakpointToggleResult.Removed;
            }
            else
            {
                SyntaxTree tree = await EditorControl.GetSyntaxTree();

                TextSpan targetSpan = new TextSpan(lineStart, 1);

                SyntaxNode node = tree.GetRoot().FindNode(targetSpan);

                SyntaxNode fullNode = node;

                while (fullNode.Parent != null && !fullNode.Kind().IsStatement())
                {
                    fullNode = fullNode.Parent;
                }

                if (fullNode.Kind().IsStatement())
                {
                    int charInd = tree.GetMappedLineSpan(fullNode.Span).StartLinePosition.Character;

                    SyntaxTrivia commentTrivia = SyntaxFactory.Comment(Utils.BreakpointMarker + Environment.NewLine + new string(' ', charInd));

                    SyntaxNode newNode = fullNode.WithLeadingTrivia(fullNode.GetLeadingTrivia().Add(commentTrivia));

                    string newDocString = newNode.GetText().ToString();

                    await EditorControl.SetText(EditorControl.Text.WithChanges(new TextChange(new TextSpan(fullNode.FullSpan.Start, fullNode.FullSpan.Length), newDocString)));

                    //AvaloniaEditor.TextArea.Document.Replace(fullNode.FullSpan.Start, fullNode.FullSpan.Length, newDocString);
                    return BreakpointToggleResult.Added;
                }
                else
                {
                    return BreakpointToggleResult.InvalidPosition;
                }
            }
        }

        internal async Task FormatText()
        {
            Document newDocument = EditorControl.Document;
            SyntaxTree tree = await EditorControl.GetSyntaxTree();

            await EditorControl.SetText(Formatter.Format(tree.GetRoot(), newDocument.Project.Solution.Workspace).GetText());

            int caretOffset = EditorControl.CaretOffset;

            int initialCaretOffset = caretOffset;

            IEnumerable<TextChange> textChanges = await EditorControl.Document.GetTextChangesAsync(newDocument);

            foreach (TextChange change in from el in textChanges where el.Span.End <= initialCaretOffset select el)
            {
                caretOffset -= change.Span.Length - change.NewText.Length;
            }

            EditorControl.SetSelection(caretOffset, 0);
        }

        internal void SaveSettings()
        {
            SavedSettings settings = new SavedSettings()
            {
                AutosaveInterval = this.AutosaveInterval,
                KeepSaveHistory = this.KeepSaveHistory,
                SyntaxHighlightingMode = this.SyntaxHighlightingMode,
                AutoOpenSuggestionts = this.AutoOpenSuggestions,
                AutoOpenParameters = this.AutoOpenParameters,
                AutoFormat = this.AutoFormat,
                CompilationTimeout = this.CompilationTimeout,
                ShowChangedLines = this.ShowLineChanges,
                ShowScrollbarOverview = this.ShowScrollbarOverview
            };

            string serialized = JsonSerializer.Serialize(settings);

            string settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSharpEditor");

            Directory.CreateDirectory(settingsDirectory);

            File.WriteAllText(Path.Combine(settingsDirectory, "settings.json"), serialized);
        }

        internal void LoadSettings()
        {
            string settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSharpEditor");

            if (File.Exists(Path.Combine(settingsDirectory, "settings.json")))
            {
                string serialized = File.ReadAllText(Path.Combine(settingsDirectory, "settings.json"));

                try
                {

                    SavedSettings settings = JsonSerializer.Deserialize<SavedSettings>(serialized);

                    SettingsContainer.FindControl<NumericUpDown>("AutosaveIntervalBox").Value = settings.AutosaveInterval / 1000;
                    SettingsContainer.FindControl<CheckBox>("KeepSaveHistoryBox").IsChecked = settings.KeepSaveHistory;
                    SettingsContainer.FindControl<ComboBox>("SyntaxHighlightingModeBox").SelectedItem = SettingsContainer.FindControl<ComboBox>("SyntaxHighlightingModeBox").Items[(int)settings.SyntaxHighlightingMode];
                    SettingsContainer.FindControl<CheckBox>("OpenSuggestionsBox").IsChecked = settings.AutoOpenSuggestionts;
                    SettingsContainer.FindControl<CheckBox>("OpenParametersBox").IsChecked = settings.AutoOpenParameters;
                    SettingsContainer.FindControl<CheckBox>("AutoFormatBox").IsChecked = settings.AutoFormat;
                    SettingsContainer.FindControl<NumericUpDown>("CompilationTimeoutBox").Value = settings.CompilationTimeout;
                    SettingsContainer.FindControl<CheckBox>("ShowChangedLinesBox").IsChecked = settings.ShowChangedLines;
                    SettingsContainer.FindControl<CheckBox>("ShowScrollbarOverviewBox").IsChecked = settings.ShowScrollbarOverview;
                }
                catch { }
            }
        }
        #endregion

        #region Private methods
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        #endregion

        #region Event handlers
        private async void CompletionCommitted(object sender, CompletionCommitEventArgs e)
        {
            string insertionText = e.Item.DisplayText;

            if (e.Item.Properties.ContainsKey("InsertionText"))
            {
                insertionText = e.Item.Properties["InsertionText"];
            }

            TextSpan correctedSpan = new TextSpan(e.Item.Span.Start - PreSource.Length - 1, e.Item.Span.Length);

            await EditorControl.SetText(EditorControl.Text.WithChanges(new TextChange(correctedSpan, insertionText)));
            EditorControl.CaretOffset = correctedSpan.Start + insertionText.Length;
            this.FindControl<CompletionWindow>("CompletionWindow").IsVisible = false;
        }

        /// <inheritdoc/>
        protected override async void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            await this.SetReferences(ImmutableList<MetadataReference>.Empty);
            base.OnDetachedFromLogicalTree(e);
        }

        #endregion
    }
}

