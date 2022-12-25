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
using Avalonia.Input;
using Avalonia.Media;
using DiffPlex.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal class InputHandler
    {
        private readonly  Editor OwnerEditor;
        private readonly CSharpSourceEditorControl EditorControl;
        private readonly CompletionWindow CompletionWindow;
        private readonly CompletionService CompletionService;
        private readonly MethodOverloadList MethodOverloadList;
        private Document Document => OwnerEditor.EditorControl.Document;

        private int MethodOverloadsOpenPosition;
        private Document MethodOverloadsOpenDocument;

        private int CompletionOpenPosition;
        private Document CompletionOpenDocument;

        private bool IsCtrlPressed = false;

        public InputHandler(Editor owner, CSharpSourceEditorControl editorControl, CompletionWindow completionWindow, MethodOverloadList overloadList, CompletionService completionService)
        {
            OwnerEditor = owner;
            EditorControl = editorControl;
            EditorControl.OnTextEntered = OnTextEntered;
            EditorControl.OnTextEntering = OnTextEntering;
            EditorControl.PointerPressed += OnPointerPressed;
            EditorControl.TextChanged += OnDocumentChanged;

            EditorControl.LostFocus += (s, e) =>
            {
                if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.IsVisible = false;
                }

                if (CompletionWindow.IsVisible)
                {
                    CompletionWindow.IsVisible = false;
                }
            };

            EditorControl.OnPreviewKeyDown = OnPreviewKeyDown;
            EditorControl.OnPreviewKeyUp = OnPreviewKeyUp;

            EditorControl.PointerHover += OnPointerHover;
            EditorControl.PointerHoverStopped += OnPointerHoverStopped;

            EditorControl.OnPaste += OnPaste;

            EditorControl.OnPreviewPointerWheelChanged += OnWheelChanged;

            CompletionWindow = completionWindow;
            MethodOverloadList = overloadList;
            CompletionService = completionService;
        }

        private void OnWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (IsCtrlPressed)
            {
                if (e.Delta.Y > 0)
                {
                    OwnerEditor.StatusBar.IncreaseFontSize();
                }
                else
                {
                    OwnerEditor.StatusBar.DecreaseFontSize();
                }
                e.Handled = true;
            }
        }

        private async void OnPaste(object sender, PasteEventArgs e)
        {
            if (OwnerEditor.AutoFormat)
            {
                await OwnerEditor.FormatText();
            }
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            OwnerEditor.CompilationErrorChecker.LastEditHandle.Set();
        }

        private async void OnPointerHover(object sender, PointerEventArgs e)
        {
            Point position = e.GetPosition(EditorControl);

            Point absolutePosition = e.GetPosition(OwnerEditor);

            LinePosition? location = EditorControl.GetPositionFromPoint(position);

            if (location != null)
            {
                int offset = EditorControl.Text.Lines.GetPosition(location.Value);

                List<Diagnostic> found = null;

                foreach (MarkerRange range in EditorControl.Markers)
                {
                    if (range.Span.Contains(offset))
                    {
                        found = range.Diagnostics;
                        break;
                    }
                }

                if (found == null)
                {
                    offset += OwnerEditor.PreSource.Length;
                    SourceText fullSource = SourceText.From(OwnerEditor.FullSource);
                    await OwnerEditor.SymbolToolTip.SetContent(Document.WithText(fullSource), offset, absolutePosition.X, 10 + (location.Value.Line + 1) * EditorControl.LineHeight - EditorControl.VerticalOffset, EditorControl.LineHeight);
                }
                else
                {
                    OwnerEditor.SymbolToolTip.SetContent(found, absolutePosition.X, 10 + (location.Value.Line + 1) * EditorControl.LineHeight - EditorControl.VerticalOffset, EditorControl.LineHeight);
                }
            }
        }

        private void OnPointerHoverStopped(object sender, PointerEventArgs e)
        {
            if (OwnerEditor.SymbolToolTip.IsVisible)
            {
                OwnerEditor.SymbolToolTip.IsVisible = false;
            }
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (MethodOverloadList.IsVisible)
            {
                MethodOverloadList.IsVisible = false;
            }

            if (CompletionWindow.IsVisible)
            {
                CompletionWindow.IsVisible = false;
            }
        }

        public async Task OnPreviewKeyDown(KeyEventArgs e)
        {
            if (OwnerEditor.SymbolToolTip.IsVisible)
            {
                OwnerEditor.SymbolToolTip.IsVisible = false;
            }

            if (e.Key.IsNavigation())
            {
                CompletionCurrentlyDisabled = false;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                IsCtrlPressed = true;
            }
            else if (e.Key == Avalonia.Input.Key.Up)
            {
                if (CompletionWindow.IsVisible)
                {
                    if (CompletionWindow.SelectedIndex >= 0)
                    {
                        _ = CompletionWindow.UpdateSelectedIndex(-1);
                    }
                    e.Handled = true;
                }
                else if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.SelectedIndex = Math.Max(MethodOverloadList.SelectedIndex - 1, 0);
                    await MethodOverloadList.RenderDescription(MethodOverloadList.Items[MethodOverloadList.SelectedIndex]);
                    e.Handled = true;
                }
            }
            else if (e.Key == Avalonia.Input.Key.Down)
            {
                if (CompletionWindow.IsVisible)
                {
                    _ = CompletionWindow.UpdateSelectedIndex(1);
                    e.Handled = true;
                }
                else if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.SelectedIndex = Math.Min(MethodOverloadList.SelectedIndex + 1, MethodOverloadList.Items.Count - 1);
                    await MethodOverloadList.RenderDescription(MethodOverloadList.Items[MethodOverloadList.SelectedIndex]);
                    e.Handled = true;
                }
            }
            if (e.Key == Avalonia.Input.Key.PageUp)
            {
                if (CompletionWindow.IsVisible)
                {
                    if (CompletionWindow.SelectedIndex >= 0)
                    {
                        _ = CompletionWindow.UpdateSelectedIndex(-(int)(CompletionWindow.Height - 33) / 20);
                    }

                    e.Handled = true;
                }
                else if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.SelectedIndex = Math.Max(MethodOverloadList.SelectedIndex - 1, 0);
                    await MethodOverloadList.RenderDescription(MethodOverloadList.Items[MethodOverloadList.SelectedIndex]);
                    e.Handled = true;
                }
            }
            else if (e.Key == Avalonia.Input.Key.PageDown)
            {
                if (CompletionWindow.IsVisible)
                {
                    _ = CompletionWindow.UpdateSelectedIndex((int)(CompletionWindow.Height - 33) / 20);
                    e.Handled = true;
                }
                else if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.SelectedIndex = Math.Min(MethodOverloadList.SelectedIndex + 1, MethodOverloadList.Items.Count - 1);
                    await MethodOverloadList.RenderDescription(MethodOverloadList.Items[MethodOverloadList.SelectedIndex]);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Tab)
            {
                if (CompletionWindow.IsVisible)
                {
                    await CompletionWindow.Commit();
                    CompletionWindow.IsVisible = false;
                    e.Handled = true;
                }
                else if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.IsVisible = false;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (CompletionWindow.IsVisible)
                {
                    CompletionWindow.IsVisible = false;
                    CompletionCurrentlyDisabled = true;
                    e.Handled = true;
                }
                else if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.IsVisible = false;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Delete)
            {
                if (CompletionWindow.IsVisible)
                {
                    CompletionWindow.IsVisible = false;
                    CompletionCurrentlyDisabled = true;
                }

                if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.IsVisible = false;
                }
            }
            else if ((e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End))
            {
                if (CompletionWindow.IsVisible)
                {
                    CompletionWindow.IsVisible = false;
                }

                if (MethodOverloadList.IsVisible)
                {
                    MethodOverloadList.IsVisible = false;
                }
            }
            else if (e.Key == Key.S && e.KeyModifiers == Utils.ControlCmdModifier)
            {
                this.OwnerEditor.Save();
            }
            else if (e.Key == Key.J && e.KeyModifiers == Utils.ControlCmdModifier)
            {
                e.Handled = true;

                if (!EditorControl.IsReadOnly)
                {
                    await OpenCompletion(true);
                }
            }
            else if (e.Key == Key.K && e.KeyModifiers == Utils.ControlCmdModifier)
            {
                e.Handled = true;
                if (EditorControl.CaretOffset > 0)
                {
                    char inserted = EditorControl.Text.ToString()[EditorControl.CaretOffset - 1];
                    await OpenMethodOverloads(inserted);
                }
            }
            else if (e.Key == Key.L && e.KeyModifiers == Utils.ControlCmdModifier)
            {
                e.Handled = true;
                await OwnerEditor.CompilationErrorChecker.CheckCompilation();
            }
            else if ((e.Key == Key.E || e.Key == Key.D) && e.KeyModifiers == Utils.ControlCmdModifier)
            {
                if (!EditorControl.IsReadOnly)
                {
                    await OwnerEditor.FormatText();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.F9)
            {
                TextLine line = EditorControl.Text.Lines.GetLineFromPosition(EditorControl.CaretOffset);
                Editor.BreakpointToggleResult result = await OwnerEditor.TryToggleBreakpoint(line.Start, line.End);

                if (result == Editor.BreakpointToggleResult.Added)
                {
                    EditorControl.CaretOffset = EditorControl.Text.Lines[line.LineNumber].End;
                }
                else if (result == Editor.BreakpointToggleResult.Removed)
                {
                    EditorControl.CaretOffset = EditorControl.Text.Lines[line.LineNumber].Start;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.F12)
            {
                ISymbol symbol = await SymbolFinder.FindSymbolAtPositionAsync(EditorControl.Document, EditorControl.CaretOffset);

                if (symbol != null)
                {
                    SyntaxNode declaration = symbol.DeclaringSyntaxReferences.Select(el => el.GetSyntax()).FirstOrDefault();

                    if (declaration != null)
                    {
                        PropertyInfo identifierProperty = declaration.GetType().GetProperty("Identifier", typeof(SyntaxToken));

                        if (identifierProperty != null)
                        {
                            SyntaxToken identifierToken = (SyntaxToken)identifierProperty.GetValue(declaration);
                            EditorControl.SetSelection(identifierToken.Span.Start, identifierToken.Span.Length);
                        }
                        else
                        {
                            EditorControl.SetSelection(declaration.Span.Start, declaration.Span.Length);
                        }
                    }
                }
            }
            else if (e.Key == Key.F5 && OwnerEditor.BreakpointPanel.IsVisible)
            {
                OwnerEditor.BreakpointPanel.InvokeResumeClicked();
                e.Handled = true;
            }
        }

        bool CompletionCurrentlyDisabled = false;

        public async Task OnPreviewKeyUp(KeyEventArgs e)
        {
            string fullSource = OwnerEditor.FullSource;

            SourceText currentSourceText = SourceText.From(fullSource);

            if (MethodOverloadList.IsVisible)
            {
                Document newDocument = MethodOverloadsOpenDocument.WithText(currentSourceText);

                IEnumerable<TextChange> changes = await newDocument.GetTextChangesAsync(MethodOverloadsOpenDocument);

                foreach (TextChange c in changes)
                {
                    if (c.Span.Contains(MethodOverloadsOpenPosition) || (c.Span.End == MethodOverloadsOpenPosition && c.Span.Length > 0))
                    {
                        MethodOverloadList.IsVisible = false;
                    }
                }
            }

            if (CompletionWindow.IsVisible)
            {
                Document newDocument = CompletionOpenDocument.WithText(currentSourceText);
                foreach (TextChange c in await newDocument.GetTextChangesAsync(CompletionOpenDocument))
                {
                    if (c.Span.Contains(CompletionOpenPosition - 2))
                    {
                        CompletionWindow.IsVisible = false;
                    }
                }
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                IsCtrlPressed = false;
            }
            else if (e.Key == Key.Back && CompletionWindow.IsVisible)
            {
                _ = UpdateCompletion();
            }

            if (EditorControl.ShowLineChanges)
            {
                DiffResult diffResultFromLastSaved = OwnerEditor.Differ.CreateLineDiffs(OwnerEditor.PreSource + "\n" + OwnerEditor.LastSavedText + "\n" + OwnerEditor.PostSource, fullSource, false);
                IEnumerable<int> changesFromLastSaved = (from el in diffResultFromLastSaved.DiffBlocks select Enumerable.Range(el.InsertStartB - OwnerEditor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                DiffResult diffResultFromOriginal = OwnerEditor.Differ.CreateLineDiffs(OwnerEditor.PreSource + "\n" + OwnerEditor.OriginalText + "\n" + OwnerEditor.PostSource, fullSource, false);
                IEnumerable<int> changesFromOriginal = (from el in diffResultFromOriginal.DiffBlocks select Enumerable.Range(el.InsertStartB - OwnerEditor.PreSourceText.Lines.Count, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                OwnerEditor.SetLineDiff(changesFromLastSaved, changesFromOriginal);
            }
        }


        private async Task UpdateCompletion()
        {
            int position = EditorControl.CaretOffset;

            try
            {
                string fullSource = OwnerEditor.FullSource;

                CompletionWindow.Document = Document.WithText(SourceText.From(fullSource));

                CompletionList completion = await CompletionService.GetCompletionsAsync(CompletionWindow.Document, position + OwnerEditor.PreSource.Length + 1);

                if (completion != null && completion.ItemsList != null && completion.ItemsList.Count > 0)
                {
                    string filter = fullSource.Substring(completion.Span.Start, completion.Span.Length);
                    await CompletionWindow.SetCompletionList(completion);
                    await CompletionWindow.SetFilterText(filter);

                    if (CompletionWindow.VisibleItems > 0)
                    {
                        CompletionWindow.IsVisible = true;
                    }
                }
                else
                {
                    CompletionWindow.IsVisible = false;
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while updating completion: {0}", ex.Message);
            }
        }

        private async Task OpenCompletion(bool wasDot)
        {
            try
            {
                int position = EditorControl.CaretOffset;

                string fullSource = OwnerEditor.FullSource;

                lock (OwnerEditor.ReferencesLock)
                {
                    CompletionWindow.References = OwnerEditor.References;
                    //Document = Document.Project.WithMetadataReferences(OwnerEditor.References).Documents.First();
                }

                CompletionWindow.Document = Document.WithText(SourceText.From(fullSource));

                CompletionOpenPosition = position + OwnerEditor.PreSource.Length + 1 + (wasDot ? 1 : 0);

                CompletionOpenDocument = CompletionWindow.Document;

                CompletionList completion = await CompletionService.GetCompletionsAsync(CompletionWindow.Document, position + OwnerEditor.PreSource.Length + 1);

                if (completion != null && completion.ItemsList != null && completion.ItemsList.Count > 0)
                {
                    string filter = fullSource.Substring(completion.Span.Start, completion.Span.Length);

                    Rect rect = EditorControl.GetCaretRectangle();

                    rect = new Rect(rect.X - OwnerEditor.CharacterWidth + 5 - (!wasDot ? OwnerEditor.CharacterWidth : 0), rect.Y, rect.Width, rect.Height);

                    double yTop = rect.Y + rect.Height + 10 - EditorControl.VerticalOffset;
                    double yBottom = rect.Y + 10 - EditorControl.VerticalOffset;

                    if (MethodOverloadList.IsVisible)
                    {
                        if (!MethodOverloadList.IsOnTop)
                        {
                            yTop += MethodOverloadList.Height;
                        }
                        else
                        {
                            yBottom -= MethodOverloadList.Height;
                        }
                    }

                    double availableBottom = EditorControl.Parent.Bounds.Height - yTop - 10;
                    double availableTop = yBottom - 10;

                    double requestedHeight = Math.Min(9, completion.ItemsList.Count) * 20 + 33;

                    if (requestedHeight <= availableBottom)
                    {
                        CompletionWindow.MaxWindowHeight = 213;
                        CompletionWindow.AnchorOnTop = false;
                        CompletionWindow.RenderTransform = new TranslateTransform(Math.Round(rect.X + rect.Width + 10), Math.Round(yTop));
                    }
                    else if (requestedHeight <= availableTop)
                    {
                        CompletionWindow.MaxWindowHeight = 213;
                        CompletionWindow.AnchorOnTop = true;
                        CompletionWindow.TopAnchor = Math.Round(yBottom);
                        CompletionWindow.RenderTransform = new TranslateTransform(Math.Round(rect.X + rect.Width + 10), Math.Round(yBottom - requestedHeight));
                    }
                    else if (availableBottom >= availableTop)
                    {
                        CompletionWindow.MaxWindowHeight = (int)Math.Round(availableBottom);
                        CompletionWindow.AnchorOnTop = false;
                        CompletionWindow.RenderTransform = new TranslateTransform(Math.Round(rect.X + rect.Width + 10), Math.Round(yTop));
                    }
                    else
                    {
                        CompletionWindow.MaxWindowHeight = (int)Math.Round(availableTop);
                        CompletionWindow.AnchorOnTop = true;
                        CompletionWindow.TopAnchor = Math.Round(yBottom);
                        CompletionWindow.RenderTransform = new TranslateTransform(Math.Round(rect.X + rect.Width + 10), Math.Round(yBottom - CompletionWindow.MaxWindowHeight));
                    }

                    await CompletionWindow.SetCompletionList(completion);
                    await CompletionWindow.SetSelectedIndex(0);
                    await CompletionWindow.SetFilterText(filter);

                    if (CompletionWindow.VisibleItems > 0)
                    {
                        CompletionWindow.IsVisible = true;
                    }
                }
                else
                {
                    CompletionWindow.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while opening completion: {0}", ex.Message);
            }
        }

        private async Task OpenMethodOverloads(char inserted)
        {
            int position = EditorControl.CaretOffset;


            MethodOverloadsOpenPosition = position + OwnerEditor.PreSource.Length + 1;

            string fullSource = OwnerEditor.FullSource;

            MethodOverloadsOpenDocument = Document.WithText(SourceText.From(fullSource));

            char textAtPos = EditorControl.Text.ToString(new TextSpan(position - 1, 1))[0];

            while (position > 0 && (textAtPos == inserted || char.IsWhiteSpace(textAtPos)))
            {
                position--;
                textAtPos = EditorControl.Text.ToString(new TextSpan(position - 1, 1))[0];
            }

            bool genericType = inserted == '<';

            Rect rect = EditorControl.GetCaretRectangle();

            double offsetX = rect.X - OwnerEditor.CharacterWidth * (EditorControl.CaretOffset - position) + 10 + 5;
            double yTop = rect.Y + rect.Height + 10 - EditorControl.VerticalOffset;

            lock (OwnerEditor.ReferencesLock)
            {
                MethodOverloadList.References = OwnerEditor.References;
                MethodOverloadList.Document = Document.Project.WithMetadataReferences(OwnerEditor.References).Documents.First();
            }

            await MethodOverloadList.SetContent(fullSource, position + OwnerEditor.PreSource.Length + 1, offsetX, yTop, OwnerEditor.CharacterWidth, rect.Height, genericType);

            //MethodOverloadList.IsVisible = true;

            //double yBottom = rect.Y + 10 - AvaloniaEditor.VerticalOffset;

            //MethodOverloadList.RenderTransform = new TranslateTransform(Math.Round(offsetX), Math.Round(yTop));
        }


        private async Task OnTextEntered(TextInputEventArgs e)
        {
            if (e.Text.Length > 1)
            {
                CompletionCurrentlyDisabled = false;
            }
            else if (e.Text.Length == 1)
            {
                char inserted = e.Text[0];

                if (char.IsPunctuation(inserted) || char.IsWhiteSpace(inserted) || char.IsSymbol(inserted))
                {
                    CompletionCurrentlyDisabled = false;
                }

                if (!CompletionCurrentlyDisabled && char.IsLetterOrDigit(inserted) || inserted == '.')
                {
                    if (!CompletionWindow.IsVisible)
                    {
                        if (OwnerEditor.AutoOpenSuggestions && !EditorControl.Text.ToString(EditorControl.Text.Lines.GetLineFromPosition(EditorControl.SelectionEnd).Span).TrimStart().StartsWith("//"))
                        {
                            _ = OpenCompletion(inserted == '.');
                        }
                    }
                    else
                    {
                        _ = UpdateCompletion();
                    }
                }

                if (inserted == '(' || inserted == '<')
                {
                    if (OwnerEditor.AutoOpenParameters)
                    {
                        await OpenMethodOverloads(inserted);
                    }
                }

                if (inserted == ')' || inserted == '>')
                {
                    if (MethodOverloadList.IsVisible)
                    {
                        MethodOverloadList.IsVisible = false;
                    }
                }
            }

            if (e.Text == "}" || e.Text == ":" || e.Text == ";")
            {
                if (OwnerEditor.AutoFormat)
                {
                    await OwnerEditor.FormatText();
                }
            }
        }

        private async Task OnTextEntering(TextInputEventArgs e)
        {
            if (e.Text.Length == 1 || e.Text == Environment.NewLine)
            {
                char inserting = e.Text[0];

                if (CompletionWindow.IsVisible)
                {
                    try
                    {
                        e.Handled = await CompletionWindow.CheckAndCommit(inserting);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error while checking completion commit: {0}", ex.Message);
                    }
                }
            }
        }
    }

}
