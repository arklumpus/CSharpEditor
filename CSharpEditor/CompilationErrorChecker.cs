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

using Avalonia.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal class CompilationErrorChecker
    {
        public Editor Editor { get; }
        public EventWaitHandle ExitHandle { get; }
        public EventWaitHandle LastEditHandle { get; }
        public int MillisecondsInterval { get; set; } = 250;
        
        public bool IsRunning { get; private set; } = false;

        private Thread LoopThread;
        private readonly object StatusObject = new object();

        public static CompilationErrorChecker Attach(Editor editor)
        {
            CompilationErrorChecker checker = new CompilationErrorChecker(editor);

            editor.DetachedFromLogicalTree += (s, e) =>
            {
                checker.ExitHandle.Set();
                checker.LoopThread.Join();
            };

            return checker;
        }

        public void Resume()
        {
            lock (StatusObject)
            {
                ExitHandle.Reset();
                LoopThread.Join();
                LoopThread = new Thread(CheckerLoop);
                LoopThread.Start();
            }
        }

        public void Stop()
        {
            lock (StatusObject)
            {
                ExitHandle.Set();
                LoopThread.Join();
            }
        }

        private CompilationErrorChecker(Editor editor)
        {
            this.Editor = editor;
            this.ExitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.LastEditHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

            LoopThread = new Thread(CheckerLoop);
            LoopThread.Start();
        }

        private async void CheckerLoop()
        {
            EventWaitHandle[] handles = new EventWaitHandle[] { LastEditHandle, ExitHandle };

            bool editSinceLastCompilation = true;

            IsRunning = true;

            while (true)
            {
                int handle = EventWaitHandle.WaitAny(handles, MillisecondsInterval);

                if (handle == 0)
                {
                    LastEditHandle.Reset();
                    editSinceLastCompilation = true;
                }
                else if (handle == 1)
                {
                    break;
                }
                else
                {
                    if (editSinceLastCompilation && Editor.AccessType != Editor.AccessTypes.ReadOnly)
                    {
                        await CheckCompilation();
                        editSinceLastCompilation = false;
                    }
                }
            }

            IsRunning = false;
        }

        public async Task CheckCompilation()
        {
            string source = null;
            string innerSource = null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                source = this.Editor.FullSource;
                innerSource = this.Editor.EditorControl.Text.ToString();
            });

            SourceText preText = this.Editor.PreSourceText;

            SourceText newText = SourceText.From(source);

            SourceText sourceOnlyText = SourceText.From(innerSource);

            ImmutableList<MetadataReference> references;

            lock (Editor.ReferencesLock)
            {
                references = Editor.References;
            }

            SyntaxTree tree = await this.Editor.OriginalDocument.WithText(newText).GetSyntaxTreeAsync();

            CSharpCompilation comp = CSharpCompilation.Create("compilation", new[] { tree }, references, Editor.CompilationOptions);

            ImmutableArray<Diagnostic> diagnostics = comp.GetDiagnostics();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.Editor.ErrorContainer.SetContent(sourceOnlyText, diagnostics, preText.Lines.Count);
            });

            this.Editor.InvokeCompilationCompleted(new CompilationEventArgs(comp));
        }
    }
}
