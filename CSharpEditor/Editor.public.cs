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
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpEditor
{
    /// <summary>
    /// A C# source code editor for Avalonia.
    /// </summary>
    public partial class Editor
    {
        /// <summary>
        /// Event raised when the user uses the keyboard shortcut or pressed the button to save the document.
        /// </summary>
        public event EventHandler<SaveEventArgs> SaveRequested;

        /// <summary>
        /// Event raised when the document is automatically saved.
        /// </summary>
        public event EventHandler<SaveEventArgs> Autosave;

        /// <summary>
        /// Event raised when a background compilation of the document completes.
        /// </summary>
        public event EventHandler<CompilationEventArgs> CompilationCompleted;

        /// <summary>
        /// Event raised when the document text is changed.
        /// </summary>
        public event EventHandler<EventArgs> TextChanged
        {
            add
            {
                EditorControl.TextChanged += value;
            }

            remove
            {
                EditorControl.TextChanged -= value;
            }
        }

        private string _preSource = "";

        /// <summary>
        /// Source code to be prepended to the text of the document when compiling it.
        /// </summary>
        public string PreSource
        {
            get
            {
                return _preSource;
            }

            private set
            {
                _preSource = value;
                PreSourceText = SourceText.From(_preSource);
            }
        }

        /// <summary>
        /// Source code to be appended after the text of the document when compiling it.
        /// </summary>
        public string PostSource { get; private set; } = "";

        /// <summary>
        /// The source code of the document as a <see cref="string"/>.
        /// </summary>
        public string Text
        {
            get
            {
                VerifyAccess();
                return EditorControl.Text.ToString();
            }
        }

        /// <summary>
        /// The source code of the document as a <see cref="SourceText"/>.
        /// </summary>
        public SourceText SourceText
        {
            get
            {
                VerifyAccess();
                return EditorControl.Text;
            }
        }

        /// <summary>
        /// Full source code, including the <see cref="PreSource"/>, the <see cref="Text"/>, and the <see cref="PostSource"/>.
        /// </summary>
        public string FullSource
        {
            get
            {
                VerifyAccess();
                return PreSource + "\n" + EditorControl.Text.ToString() + "\n" + PostSource;
            }
        }

        /// <summary>
        /// Describes the actions that the user can perform on the code.
        /// </summary>
        public enum AccessTypes
        {
            /// <summary>
            /// The code can be edited freely.
            /// </summary>
            ReadWrite,

            /// <summary>
            /// The code cannot be edited, but the list of errors and warnings is displayed, and the user can load previous versions of the file.
            /// </summary>
            ReadOnlyWithHistoryAndErrors,

            /// <summary>
            /// The code can only be read. No advanced features are provided beyond syntax highlighting.
            /// </summary>
            ReadOnly
        }

        private AccessTypes accessType = AccessTypes.ReadWrite;

        /// <summary>
        /// Determines whether the text of the document can be edited by the user.
        /// </summary>
        public AccessTypes AccessType
        {
            get
            {
                return this.accessType;
            }

            set
            {
                this.accessType = value;
                this.EditorControl.IsReadOnly = value != AccessTypes.ReadWrite;

                if (value == AccessTypes.ReadOnly)
                {
                    this.StatusBar.FindControl<ToggleButton>("ToggleErrorContainerButton").IsVisible = false;
                    this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsVisible = false;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsVisible = false;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsVisible = false;
                }
                else if (value == AccessTypes.ReadWrite)
                {
                    this.StatusBar.FindControl<ToggleButton>("ToggleErrorContainerButton").IsVisible = true;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsVisible = true;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsVisible = true;

                    if (IsReferencesButtonEnabled)
                    {
                        ((Grid)this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").Parent).ColumnDefinitions[5] = new ColumnDefinition(1, GridUnitType.Star);
                        this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsVisible = true;
                    }
                }
                else
                {
                    this.StatusBar.FindControl<ToggleButton>("ToggleErrorContainerButton").IsVisible = true;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsVisible = false;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsVisible = true;
                    this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsVisible = false;
                }
            }
        }

        /// <summary>
        /// Compilation options used to compile the source code.
        /// </summary>
        public CSharpCompilationOptions CompilationOptions { get; private set; }

        /// <summary>
        /// A unique identifier for the document being edited.
        /// </summary>
        public string Guid { get; private set; }

        /// <summary>
        /// The full path to the directory where the autosave file and the save history for the current document are kept.
        /// </summary>
        public string SaveDirectory { get; private set; }

        /// <summary>
        /// The full path to the autosave file.
        /// </summary>
        public string AutoSaveFile { get; private set; }

        /// <summary>
        /// A boolean value indicating whether a history of the saved versions of the document is kept.
        /// </summary>
        public bool KeepSaveHistory { get; internal set; } = true;

        /// <summary>
        /// A boolean value indicating whether the suggestion panel should open automatically while the user is typing.
        /// </summary>
        public bool AutoOpenSuggestions { get; internal set; } = true;

        /// <summary>
        /// A boolean value indicating whether the parameter list tooltip should open automatically while the user is typing.
        /// </summary>
        public bool AutoOpenParameters { get; internal set; } = true;

        /// <summary>
        /// A boolean value indicating whether the source text should be formatted automatically while the user is typing.
        /// </summary>
        public bool AutoFormat { get; internal set; } = true;

        /// <summary>
        /// The current syntax highlighting mode.
        /// </summary>
        public SyntaxHighlightingModes SyntaxHighlightingMode => this.EditorControl.SyntaxHighlightingMode;

        /// <summary>
        /// A boolean value indicating whether changed lines are highlighted on the left side of the control.
        /// </summary>
        public bool ShowLineChanges => this.EditorControl.ShowLineChanges;

        /// <summary>
        /// A boolean value indicating whether a summary of the changed lines, errors/warning, search results, breakpoints and the position of the caret should be shown over the vertical scrollbar.
        /// </summary>
        public bool ShowScrollbarOverview => this.EditorControl.ShowScrollbarOverview;

        /// <summary>
        /// The timeout between consecutive autosaves, in milliseconds.
        /// </summary>
        public int AutosaveInterval => this.AutoSaver.MillisecondsInterval;

        /// <summary>
        /// The timeout for automatic compilation after the user stops typing, in milliseconds.
        /// </summary>
        public int CompilationTimeout => this.CompilationErrorChecker.MillisecondsInterval;

        /// <summary>
        /// The list of <see cref="MetadataReference"/>s for which the compiled assembly will have bindings. 
        /// </summary>
        public ImmutableList<MetadataReference> References { get; private set; }

        private bool _isReferencesButtonEnabled = true;

        /// <summary>
        /// A boolean value indicating whether the button allowing the user to add or remove assembly references is enabled or not.
        /// </summary>
        public bool IsReferencesButtonEnabled
        {
            get
            {
                return _isReferencesButtonEnabled;
            }

            set
            {
                if (value && AccessType == AccessTypes.ReadWrite)
                {
                    ((Grid)this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").Parent).ColumnDefinitions[5] = new ColumnDefinition(1, GridUnitType.Star);
                    this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsVisible = true;
                }
                else
                {
                    ((Grid)this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").Parent).ColumnDefinitions[5] = new ColumnDefinition(0, GridUnitType.Pixel);
                    this.StatusBar.FindControl<ToggleButton>("ToggleReferencesContainerButton").IsVisible = false;
                }

                _isReferencesButtonEnabled = value;
            }
        }

        /// <summary>
        /// Gets or sets the selected text span.
        /// </summary>
        public TextSpan Selection
        {
            get
            {
                return new TextSpan(EditorControl.SelectionStart, EditorControl.SelectionEnd - EditorControl.SelectionStart);
            }

            set
            {
                EditorControl.SetSelection(value.Start, value.Length);
            }
        }

        /// <summary>
        /// Create a new <see cref="Editor"/> instance.
        /// </summary>
        /// <param name="initialText">The initial text of the editor.</param>
        /// <param name="preSource">The source code that should be prepended to the text of the document when compiling it.</param>
        /// <param name="postSource">The source code that should be appended to the text of the document when compiling it.</param>
        /// <param name="references">A list of <see cref="MetadataReference"/>s for which the compiled assembly will have bindings. Make sure to include an appropriate <see cref="DocumentationProvider"/>, if you would like documentation comments to appear in code completion windows. If this is <see langword="null"/>, references to all of the assemblies loaded in the current <see cref="AppDomain"/> will be added.</param>
        /// <param name="compilationOptions">The compilation options used to compile the code. If this is <see langword="null"/>, a <c>new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)</c> will be used.</param>
        /// <param name="guid">A unique identifier for the document being edited. If this is <see langword="null"/>, a new <see cref="System.Guid"/> is generated. If the same identifier is used multiple times, the save history of the document will be available, even if the application has been closed between different sessions.</param>
        /// <param name="additionalShortcuts">Additional application-specific shortcuts (for display purposes only - you need to implement your own logic).</param>
        /// <returns>A fully initialised <see cref="Editor"/> instance.</returns>
        public static async Task<Editor> Create(string initialText = "", string preSource = "", string postSource = "", IEnumerable<CachedMetadataReference> references = null, CSharpCompilationOptions compilationOptions = null, string guid = null, Shortcut[] additionalShortcuts = null)
        {

            if (references == null)
            {
                List<CachedMetadataReference> referencesList = new List<CachedMetadataReference>();

                foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string location = null;

                    try
                    {
                        location = ass.Location;
                    }
                    catch (NotSupportedException) { };

                    if (!string.IsNullOrEmpty(location))
                    {
                        referencesList.Add(CachedMetadataReference.CreateFromFile(location, Path.Combine(Path.GetDirectoryName(location), Path.GetFileNameWithoutExtension(location) + ".xml")));
                    }
                }

                references = referencesList;
            }

            if (compilationOptions == null)
            {
                compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            }

            if (string.IsNullOrEmpty(guid))
            {
                guid = System.Guid.NewGuid().ToString("N");
            }
            else
            {
                foreach (char c in Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()))
                {
                    if (guid.Contains(c))
                    {
                        throw new ArgumentException("The provided Guid \"" + guid + "\" is not valid!\nThe Guid must be a valid identifier for a path or a file.", nameof(guid));
                    }
                }
            }

            Editor tbr = new Editor(false);
            await tbr.Initialize(initialText, preSource, postSource, references, compilationOptions, guid, additionalShortcuts ?? new Shortcut[0]);
            return tbr;
        }

        /// <summary>
        /// Sets the text of the document.
        /// </summary>
        /// <param name="text">The new text of the document.</param>
        /// <returns>A <see cref="Task"/> that completes when the text has been updated.</returns>
        public async Task SetText(string text)
        {
            await EditorControl.SetText(text);
        }

        /// <summary>
        /// Sets the text of the document.
        /// </summary>
        /// <param name="text">The new text of the document.</param>
        /// <returns>A <see cref="Task"/> that completes when the text has been updated.</returns>
        public async Task SetText(SourceText text)
        {
            await EditorControl.SetText(text);
        }


        /// <summary>
        /// A function to handle breakpoints in synchronous methods. Pass this as an argument to <see cref="Compile(Func{BreakpointInfo, bool}, Func{BreakpointInfo, Task{bool}})"/>. To prevent deadlocks, this function will have no effect if called from the UI thread.
        /// </summary>
        /// <param name="info">A <see cref="BreakpointInfo"/> object containing information about the location of the breakpoint and the current value of local variables.</param>
        /// <returns><see langword="true" /> if further occurrences of the same breakpoint should be ignored; <see langword="false"/> otherwise.</returns>
        public bool SynchronousBreak(BreakpointInfo info)
        {
            if (!CheckAccess())
            {
                EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

                bool tbr = false;

                async void resumeHandler(object sender, EventArgs e)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => tbr = BreakpointPanel.IgnoreFurtherOccurrences);
                    waitHandle.Set();
                }

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    EditorControl.ActiveBreakpoint = info.BreakpointSpan.Start - PreSource.Length - 1;
                    EditorControl.SetSelection(info.BreakpointSpan.End - PreSource.Length - 1, 0);
                    BreakpointPanel.SetContent(info);
                    BreakpointPanel.ResumeClicked += resumeHandler;
                    this.FindAncestorOfType<Window>().Closing += resumeHandler;
                    OpenSidePanel();
                });

                waitHandle.WaitOne();

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CloseSidePanel();
                    BreakpointPanel.ResumeClicked -= resumeHandler;
                    this.FindAncestorOfType<Window>().Closing -= resumeHandler;
                    EditorControl.ActiveBreakpoint = -1;
                });

                return tbr;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// A function to handle breakpoints in asynchronous methods. Pass this as an argument to <see cref="Compile(Func{BreakpointInfo, bool}, Func{BreakpointInfo, Task{bool}})"/>.
        /// </summary>
        /// <param name="info">A <see cref="BreakpointInfo"/> object containing information about the location of the breakpoint and the current value of local variables.</param>
        /// <returns>A <see cref="Task"/> that completes when code execution resumes after the breakpoint.</returns>
        public async Task<bool> AsynchronousBreak(BreakpointInfo info)
        {
            using (SemaphoreSlim semaphore = new SemaphoreSlim(0, 1))
            {
                void resumeHandler(object sender, EventArgs e)
                {
                    semaphore.Release();
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    EditorControl.ActiveBreakpoint = info.BreakpointSpan.Start - PreSource.Length - 1;
                    EditorControl.SetSelection(info.BreakpointSpan.End - PreSource.Length - 1, 0);
                    BreakpointPanel.SetContent(info);
                    BreakpointPanel.ResumeClicked += resumeHandler;
                    this.FindAncestorOfType<Window>().Closing += resumeHandler;
                    OpenSidePanel();
                });

                await semaphore.WaitAsync();

                bool tbr = false;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tbr = BreakpointPanel.IgnoreFurtherOccurrences;
                    CloseSidePanel();
                    BreakpointPanel.ResumeClicked -= resumeHandler;
                    this.FindAncestorOfType<Window>().Closing -= resumeHandler;
                    EditorControl.ActiveBreakpoint = -1;
                });

                semaphore.Dispose();

                return tbr;
            }
        }

        internal async Task<bool> AsynchronousBreak(RemoteBreakpointInfo info)
        {
            using (SemaphoreSlim semaphore = new SemaphoreSlim(0, 1))
            {
                void resumeHandler(object sender, EventArgs e)
                {
                    semaphore.Release();
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    EditorControl.ActiveBreakpoint = info.BreakpointSpan.Start - PreSource.Length - 1;
                    EditorControl.SetSelection(info.BreakpointSpan.End - PreSource.Length - 1, 0);
                    BreakpointPanel.SetContent(info);
                    BreakpointPanel.ResumeClicked += resumeHandler;
                    this.FindAncestorOfType<Window>().Closing += resumeHandler;
                    OpenSidePanel();
                });

                await semaphore.WaitAsync();

                bool tbr = false;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tbr = BreakpointPanel.IgnoreFurtherOccurrences;
                    CloseSidePanel();
                    BreakpointPanel.ResumeClicked -= resumeHandler;
                    this.FindAncestorOfType<Window>().Closing -= resumeHandler;
                    EditorControl.ActiveBreakpoint = -1;
                });

                semaphore.Dispose();

                return tbr;
            }
        }

        /// <summary>
        /// Compile the source code to an <see cref="Assembly"/>.
        /// </summary>
        /// <param name="synchronousBreak">The function to handle synchronous breakpoints. If this is <see langword="null" />, these breakpoints will be skipped. If you want to enable the default UI for breakpoints, use <see cref="SynchronousBreak(BreakpointInfo)"/> (or a function that calls it after performing additional operations).</param>
        /// <param name="asynchronousBreak">The function to handle asynchronous breakpoints. If this is <see langword="null" />, these breakpoints will be skipped. If you want to enable the default UI for breakpoints, use <see cref="AsynchronousBreak(BreakpointInfo)"/> (or a function that calls it after performing additional operations).</param>
        /// <returns>An <see cref="Assembly"/> containing the compiled code, or <see langword="null"/> if the compilation fails, as well as a <see cref="CSharpCompilation"/> that also contains information about any compilation errors.</returns>
        public async Task<(Assembly Assembly, CSharpCompilation Compilation)> Compile(Func<BreakpointInfo, bool> synchronousBreak = null, Func<BreakpointInfo, Task<bool>> asynchronousBreak = null)
        {
            string source = null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                source = this.FullSource;
            });

            SourceText sourceText = SourceText.From(source);

            ImmutableList<MetadataReference> references;

            lock (ReferencesLock)
            {
                references = References;
            }

            SyntaxTree tree = await this.OriginalDocument.WithText(sourceText).GetSyntaxTreeAsync();

            CSharpCompilation comp = CSharpCompilation.Create("compilation", new[] { tree }, references, this.CompilationOptions);

            string debuggerGuid = "_" + System.Guid.NewGuid().ToString("N");

            if (synchronousBreak != null || asynchronousBreak != null)
            {
                List<(TextSpan, bool)> validBreakpoints = new List<(TextSpan, bool)>();

                foreach (int i in source.AllIndicesOf(Utils.BreakpointMarker))
                {
                    SyntaxNode node = tree.GetRoot().FindNode(new TextSpan(i, 1));

                    SyntaxNode fullNode = node;

                    while (fullNode.Parent != null && !fullNode.Kind().IsStatement())
                    {
                        fullNode = fullNode.Parent;
                    }

                    if (fullNode.Kind().IsStatement())
                    {
                        SyntaxNode methodNode = fullNode;

                        while (methodNode.Parent != null && !methodNode.IsKind(SyntaxKind.MethodDeclaration) && !methodNode.IsKind(SyntaxKind.ParenthesizedLambdaExpression) && !methodNode.IsKind(SyntaxKind.AnonymousMethodExpression))
                        {
                            methodNode = methodNode.Parent;
                        }

                        if (methodNode.IsKind(SyntaxKind.MethodDeclaration) || methodNode.IsKind(SyntaxKind.ParenthesizedLambdaExpression) || methodNode.IsKind(SyntaxKind.AnonymousMethodExpression))
                        {
                            bool isAsync = false;

                            if (methodNode.IsKind(SyntaxKind.MethodDeclaration))
                            {
                                MethodDeclarationSyntax method = (MethodDeclarationSyntax)methodNode;

                                foreach (SyntaxToken token in method.Modifiers)
                                {
                                    if (token.Text == "async")
                                    {
                                        isAsync = true;
                                    }
                                }
                            }
                            else if (methodNode.IsKind(SyntaxKind.ParenthesizedLambdaExpression))
                            {
                                ParenthesizedLambdaExpressionSyntax method = (ParenthesizedLambdaExpressionSyntax)methodNode;

                                if (method.AsyncKeyword.Text == "async")
                                {
                                    isAsync = true;
                                }
                            }
                            else if (methodNode.IsKind(SyntaxKind.AnonymousMethodExpression))
                            {
                                AnonymousMethodExpressionSyntax method = (AnonymousMethodExpressionSyntax)methodNode;

                                if (method.AsyncKeyword.Text == "async")
                                {
                                    isAsync = true;
                                }
                            }

                            if ((!isAsync && synchronousBreak != null) || (isAsync && asynchronousBreak != null))
                            {
                                validBreakpoints.Add((new TextSpan(i, Utils.BreakpointMarker.Length), isAsync));
                            }
                        }
                    }
                }

                SyntaxTree debuggerTree = BreakpointInfo.GetDebuggerSyntaxTree(debuggerGuid);

                SemanticModel model = comp.GetSemanticModel(tree, false);

                for (int i = validBreakpoints.Count - 1; i >= 0; i--)
                {
                    ILocalSymbol[] locals = (from el in model.LookupSymbols(validBreakpoints[i].Item1.Start) where el.Kind == SymbolKind.Local && !el.IsInaccessibleLocal(model, validBreakpoints[i].Item1.Start, tree.GetRoot().FindNode(validBreakpoints[i].Item1)) && !string.IsNullOrEmpty(el.Name) select (ILocalSymbol)el).ToArray();

                    string breakpointSource = BreakpointInfo.GetBreakpointSource(validBreakpoints[i].Item1, locals, model, debuggerGuid, validBreakpoints[i].Item2);

                    sourceText = sourceText.Replace(validBreakpoints[i].Item1, breakpointSource);
                }

                string text = sourceText.ToString();

                tree = await this.OriginalDocument.WithText(sourceText).GetSyntaxTreeAsync();

                comp = CSharpCompilation.Create("compilation", new[] { debuggerTree, tree }, references, this.CompilationOptions);
            }

            using (MemoryStream ms = new MemoryStream())
            {
                EmitResult result = comp.Emit(ms);

                if (!result.Success)
                {
                    return (null, comp);
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    if (synchronousBreak != null || asynchronousBreak != null)
                    {
                        assembly.GetType(debuggerGuid + ".Debugger").InvokeMember("Breakpoint", BindingFlags.Static | BindingFlags.Public | BindingFlags.SetField, null, null, new object[] { BreakpointInfo.GetBreakpointFunction(synchronousBreak) });
                        assembly.GetType(debuggerGuid + ".Debugger").InvokeMember("BreakpointAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.SetField, null, null, new object[] { BreakpointInfo.GetBreakpointAsyncFunction(asynchronousBreak) });
                    }

                    return (assembly, comp);
                }
            }
        }

        /// <summary>
        /// Compile the source code to a <see cref="CSharpCompilation"/>. Note that breakpoints will be disabled.
        /// </summary>
        /// <returns>A <see cref="CSharpCompilation"/> containing the compiled code, which can be used to <c>Emit</c> an assembly.</returns>
        public async Task<CSharpCompilation> CreateCompilation()
        {
            string source = null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                source = this.FullSource;
            });

            SourceText sourceText = SourceText.From(source);

            ImmutableList<MetadataReference> references;

            lock (ReferencesLock)
            {
                references = References;
            }

            SyntaxTree tree = await this.OriginalDocument.WithText(sourceText).GetSyntaxTreeAsync();

            CSharpCompilation comp = CSharpCompilation.Create("compilation", new[] { tree }, references, this.CompilationOptions);

            return comp;
        }

        /// <summary>
        /// Add the current text of the document to the save history (if enabled) and invoke the <see cref="SaveRequested"/> event.
        /// </summary>
        public void Save()
        {
            string text = this.EditorControl.Text.ToString();

            if (KeepSaveHistory)
            {
                string autosaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly().GetName().Name);
                Directory.CreateDirectory(Path.Combine(autosaveDirectory, this.Guid));
                System.IO.File.WriteAllText(System.IO.Path.Combine(autosaveDirectory, this.Guid, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() + ".cs"), text);
            }

            this.UpdateLastSavedDocument();
            if (this.SaveHistoryContainer.IsVisible)
            {
                this.SaveHistoryContainer.Refresh();
            }

            this.InvokeSaveRequested(new SaveEventArgs(text));
        }

        /// <summary>
        /// Add the specified references to the loaded references.
        /// </summary>
        /// <param name="references">The reference to add.</param>
        /// <returns>A <see cref="Task"/> that completes when the references have been added and the document has been updated.</returns>
        public async Task AddReferences(params MetadataReference[] references)
        {
            await AddReferences((IEnumerable<MetadataReference>)references);
        }

        /// <summary>
        /// Add the specified references to the loaded references.
        /// </summary>
        /// <param name="references">The reference to add.</param>
        /// <returns>A <see cref="Task"/> that completes when the references have been added and the document has been updated.</returns>
        public async Task AddReferences(IEnumerable<MetadataReference> references)
        {
            foreach (MetadataReference reference in references)
            {
                this.ReferencesContainer.AddReferenceLine(reference, this.ReferencesContainer.FindControl<ToggleButton>("CoreReferencesButton"), this.ReferencesContainer.FindControl<ToggleButton>("AdditionalReferencesButton"));
            }

            this.ReferencesContainer.References = this.ReferencesContainer.References.AddRange(references);

            await this.SetReferences(this.ReferencesContainer.References, false);
        }

        /// <summary>
        /// Remove the specified references from the loaded references.
        /// </summary>
        /// <param name="references">The references to remove.</param>
        /// <returns>A <see cref="Task"/> that completes when the references have been removed and the document has been updated.</returns>
        public async Task RemoveReferences(params MetadataReference[] references)
        {
            await RemoveReferences((IEnumerable<MetadataReference>)references);
        }

        /// <summary>
        /// Remove the specified references from the loaded references.
        /// </summary>
        /// <param name="references">The references to remove.</param>
        /// <returns>A <see cref="Task"/> that completes when the references have been removed and the document has been updated.</returns>
        public async Task RemoveReferences(IEnumerable<MetadataReference> references)
        {
            foreach (MetadataReference reference in references)
            {
                Control referenceGrid = null;

                foreach (Control ctrl in this.ReferencesContainer.FindControl<StackPanel>("ReferencesContainer").Children)
                {
                    if (ctrl.Tag == reference)
                    {
                        referenceGrid = ctrl;
                        break;
                    }
                }

                this.ReferencesContainer.FindControl<StackPanel>("ReferencesContainer").Children.Remove(referenceGrid);
            }

            this.ReferencesContainer.References = this.ReferencesContainer.References.RemoveRange(references);

            await this.SetReferences(this.ReferencesContainer.References, false);
        }
    }

    /// <summary>
    /// A class to hold data for an event where the user has requested to save the document.
    /// </summary>
    public class SaveEventArgs : EventArgs
    {
        /// <summary>
        /// The text of the document to save (not including any prepended or appended source code).
        /// </summary>
        public string Text { get; }

        internal SaveEventArgs(string text) : base()
        {
            this.Text = text;
        }
    }

    /// <summary>
    /// A class to hold data for an event where a background compilation has completed.
    /// </summary>
    public class CompilationEventArgs : EventArgs
    {
        /// <summary>
        /// A <see cref="CSharpCompilation"/> object containing information about the compilation that has completed, which can be used to <c>Emit</c> an assembly, if successful.
        /// </summary>
        public CSharpCompilation Compilation { get; }

        internal CompilationEventArgs(CSharpCompilation compilation) : base()
        {
            this.Compilation = compilation;
        }
    }

    /// <summary>
    /// Represents a keyboard shortcut.
    /// </summary>
    public class Shortcut
    {
        /// <summary>
        /// The name of the action performed by the shortcut.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The keys that have to be pressed together to perform the action.
        /// </summary>
        public string[][] Shortcuts { get; }

        /// <summary>
        /// Creates a new <see cref="Shortcut"/> instance.
        /// </summary>
        /// <param name="name">The name of the action performed by the shortcut (e.g. "Copy").</param>
        /// <param name="shortcuts">The keys that have to be pressed together to perform the action (e.g. [ [ "Ctrl", "C" ], [ "Ctrl", "Ins" ] ] to specify that either <c>Ctrl+C</c> or <c>Ctrl+Ins</c> can be used. "Ctrl" will automatically be converted to "Cmd" on macOS.</param>
        public Shortcut(string name, string[][] shortcuts)
        {
            this.Name = name;
            this.Shortcuts = shortcuts;
        }
    }

    /// <summary>
    /// Represents syntax highlighting modes.
    /// </summary>
    public enum SyntaxHighlightingModes
    {
        /// <summary>
        /// No syntax highliting is perfomed.
        /// </summary>
        None,

        /// <summary>
        /// Syntax highlighting is performed only based on syntactic information.
        /// </summary>
        Syntactic,

        /// <summary>
        /// Syntax highlighting is performed based on syntactic and semantic information.
        /// </summary>
        Semantic
    }
}
