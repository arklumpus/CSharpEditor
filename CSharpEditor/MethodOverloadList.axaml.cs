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
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal class MethodOverloadList : UserControl
    {
        public Document Document { get; set; }
        public IReadOnlyList<MetadataReference> References;

        private const int ItemNumberWidth = 95;

        public List<ISymbol> Items { get; set; } = new List<ISymbol>();

        private SemanticModel SemanticModel;
        private int Position;

        private int _selectedIndex = -1;

        public int SelectedIndex
        {
            get
            {
                return _selectedIndex;
            }

            set
            {
                _selectedIndex = value;

                this.FindControl<TextBlock>("CountText").Text = (_selectedIndex + 1).ToString() + " of " + Items.Count.ToString();
            }
        }
        public Point RenderPosition { get; set; }
        public double CharacterHeight { get; set; }

        public bool ShowsTypeParameters { get; set; }

        public bool IsOnTop { get; private set; }

        public async Task RenderDescription(ISymbol symbol)
        {
            VectSharp.Font labelFont = new VectSharp.Font(Editor.OpenSansRegular, this.FontSize);
            VectSharp.Font codeFont = new VectSharp.Font(Editor.RobotoMonoRegular, this.FontSize);
            VectSharp.Font parameterNameFont = new VectSharp.Font(Editor.OpenSansBoldItalic, this.FontSize);
            VectSharp.Font parameterDescriptionFont = new VectSharp.Font(Editor.OpenSansItalic, this.FontSize);

            double availableWidth = this.Parent.Bounds.Width - 22 - (this.Items.Count > 1 ? ItemNumberWidth : 0) - 14 - 5;

            double availableHeightBottom = this.Parent.Bounds.Height - RenderPosition.Y - 22 - 4;
            double availableHeightTop = RenderPosition.Y - CharacterHeight - 4;

            ImmutableArray<SymbolDisplayPart> parts = symbol.ToMinimalDisplayParts(SemanticModel, Position);

            string documentationId = symbol.GetDocumentationCommentId();
            string documentationXml = symbol.GetDocumentationCommentXml();

            if (string.IsNullOrEmpty(documentationXml) && !string.IsNullOrEmpty(documentationId))
            {
                (await Utils.GetReferenceDocumentation()).TryGetValue(documentationId, out documentationXml);
            }

            FormattedText description = FormattedText.FormatDescription(from el in parts select el.ToTaggedText(), documentationXml, labelFont, codeFont);

            Canvas content = description.Render(availableWidth, false);
            content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

            double desiredWidth = content.Width;

            FormattedText parameters = new FormattedText();

            if (ShowsTypeParameters)
            {
                FormattedText.FormatTypeParameterList(parameters, documentationXml, parameterNameFont, parameterDescriptionFont, codeFont);
            }
            else
            {
                FormattedText.FormatTypeParameterList(parameters, documentationXml, parameterNameFont, parameterDescriptionFont, codeFont);
                FormattedText.FormatParameterList(parameters, documentationXml, parameterNameFont, parameterDescriptionFont, codeFont);
            }

            Canvas parameterContents = null;
            double parameterContentsHeight = 0;

            if (parameters.Paragraphs.Count > 0)
            {
                parameterContents = parameters.Render(availableWidth, false);
                parameterContents.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                parameterContents.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                parameterContentsHeight = Math.Min(parameterContents.Height, this.FontSize * (3 * 1.8 + 0.4));
                desiredWidth = Math.Max(desiredWidth, parameterContents.Width);
                this.FindControl<ScrollViewer>("MethodParametersContainer").IsVisible = true;

                if (parameterContents.Height > parameterContentsHeight)
                {
                    this.FindControl<ScrollViewer>("MethodParametersContainer").VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;
                    desiredWidth = Math.Max(desiredWidth, parameterContents.Width + 20);
                }
                else
                {
                    this.FindControl<ScrollViewer>("MethodParametersContainer").VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
                }
            }
            else
            {
                this.FindControl<ScrollViewer>("MethodParametersContainer").IsVisible = false;
            }

            double desiredHeight = content.Height + parameterContentsHeight;

            if (desiredWidth <= availableWidth && desiredHeight <= availableHeightBottom)
            {
                this.Width = Math.Min(availableWidth, desiredWidth) + (Items.Count > 1 ? ItemNumberWidth : 0) + 10 + 4;
                this.Height = Math.Max(desiredHeight, 25) + 4;

                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Content = content;
                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = content.Height;

                this.FindControl<ScrollViewer>("MethodParametersContainer").Content = parameterContents;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = parameterContentsHeight;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y };
                this.IsOnTop = false;

                this.IsVisible = true;

            }
            else if (desiredWidth <= availableWidth && desiredHeight <= availableHeightTop)
            {
                this.Width = Math.Min(availableWidth, desiredWidth) + (Items.Count > 1 ? ItemNumberWidth : 0) + 10 + 4;
                this.Height = Math.Max(desiredHeight, 25) + 4;

                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Content = content;
                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = content.Height;

                this.FindControl<ScrollViewer>("MethodParametersContainer").Content = parameterContents;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = parameterContentsHeight;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y - CharacterHeight - this.Height };
                this.IsOnTop = true;

                this.IsVisible = true;
            }
            else if (content.Width <= availableWidth && content.Height <= availableHeightBottom)
            {
                this.Width = Math.Min(availableWidth, content.Width) + (Items.Count > 1 ? ItemNumberWidth : 0) + 10 + 4;
                this.Height = Math.Max(content.Height, 25) + 4;

                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Content = content;
                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Width = Math.Min(availableWidth, content.Width) + 10;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = content.Height;

                this.FindControl<ScrollViewer>("MethodParametersContainer").Content = null;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = 0;
                this.FindControl<ScrollViewer>("MethodParametersContainer").IsVisible = false;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y };
                this.IsOnTop = false;

                this.IsVisible = true;
            }
            else if (content.Width <= availableWidth && content.Height <= availableHeightTop)
            {
                this.Width = Math.Min(availableWidth, content.Width) + (Items.Count > 1 ? ItemNumberWidth : 0) + 10 + 4;
                this.Height = Math.Max(content.Height, 25) + 4;

                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Content = content;
                this.FindControl<ScrollViewer>("MethodDescriptionContainer").Width = Math.Min(availableWidth, content.Width) + 10;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = content.Height;

                this.FindControl<ScrollViewer>("MethodParametersContainer").Content = null;
                this.FindControl<ScrollViewer>("MethodParametersContainer").Height = 0;
                this.FindControl<ScrollViewer>("MethodParametersContainer").IsVisible = false;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y - CharacterHeight - this.Height };
                this.IsOnTop = true;

                this.IsVisible = true;
            }
            else
            {
                this.IsVisible = false;
            }
        }

        public async Task SetContent(string text, int caretPosition, double offsetX, double offsetY, double characterWidth, double characterHeight, bool searchGenerics)
        {
            SourceText source = SourceText.From(text);

            SyntaxTree tree = await Document.WithText(source).GetSyntaxTreeAsync();

            CSharpCompilation comp = CSharpCompilation.Create("documentation", new[] { tree }, References);
            SemanticModel model = comp.GetSemanticModel(tree);

            TextSpan targetSpan = new TextSpan(caretPosition - 1, 1);

            SyntaxNode node = tree.GetRoot().FindNode(targetSpan);

            if (node.IsKind(SyntaxKind.TypeArgumentList))
            {
                node = node.Parent;
            }

            if (node != null)
            {
                SymbolInfo info = model.GetSymbolInfo(node);

                List<ISymbol> originalSymbols = new List<ISymbol>();

                if (info.Symbol != null)
                {
                    originalSymbols.Add(info.Symbol);
                }

                if (info.CandidateSymbols != null && info.CandidateSymbols.Length > 0)
                {
                    originalSymbols.AddRange(info.CandidateSymbols);
                }

                List<ISymbol> symbols = new List<ISymbol>();

                if (!searchGenerics)
                {
                    AddSymbols<IMethodSymbol, IMethodSymbol>(originalSymbols, symbols, SymbolKind.Method, SymbolKind.Method, symbol => ((IMethodSymbol)symbol).ContainingType.GetMembers(), symbol => true, (symbol1, symbol2) => symbol1.Name == symbol2.Name);

                    AddSymbols<INamedTypeSymbol, IMethodSymbol>(originalSymbols, symbols, SymbolKind.NamedType, SymbolKind.Method, symbol => symbol.Constructors, symbol => false, (symbol1, symbol2) => true);

                    this.ShowsTypeParameters = false;
                }
                else
                {
                    AddSymbols<IMethodSymbol, IMethodSymbol>(originalSymbols, symbols, SymbolKind.Method, SymbolKind.Method, symbol => symbol.ContainingType.GetMembers(), symbol => symbol.Arity > 0, (symbol1, symbol2) => symbol1.Name == symbol2.Name && symbol2.Arity > 0);
                    AddSymbols<INamedTypeSymbol, INamedTypeSymbol>(originalSymbols, symbols, SymbolKind.NamedType, SymbolKind.NamedType, symbol =>
                    {
                        if (symbol.ContainingSymbol.Kind == SymbolKind.Namespace)
                        {
                            return symbol.ContainingNamespace.GetTypeMembers();
                        }
                        else if (symbol.ContainingSymbol.Kind == SymbolKind.NamedType)
                        {
                            return symbol.ContainingType.GetMembers();
                        }
                        else
                        {
                            return new ISymbol[0];
                        }

                    }, symbol => symbol.Arity > 0, (symbol1, symbol2) => symbol1.Name == symbol2.Name && symbol2.Arity > 0);
                    this.ShowsTypeParameters = true;
                }



                this.SemanticModel = model;
                this.Position = caretPosition;

                this.Items = symbols;

                if ((!searchGenerics && symbols.Count > 0) || symbols.Count > 1)
                {
                    if (symbols.Count == 1)
                    {
                        this.FindControl<Grid>("GridContainer").ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Pixel);
                        this.FindControl<Grid>("CountGrid").IsVisible = false;
                    }
                    else
                    {
                        this.FindControl<Grid>("GridContainer").ColumnDefinitions[0].Width = new GridLength(ItemNumberWidth, GridUnitType.Pixel);
                        this.FindControl<Grid>("CountGrid").IsVisible = true;
                        this.FindControl<TextBlock>("CountText").Text = "1 of " + symbols.Count.ToString();
                    }

                    this.SelectedIndex = 0;

                    SyntaxNode fullNode = node;

                    while (fullNode.Parent != null && !fullNode.Kind().IsStatement())
                    {
                        fullNode = fullNode.Parent;
                    }

                    LinePosition mappedCaretPosition = tree.GetMappedLineSpan(targetSpan).StartLinePosition;
                    LinePosition nodeStartPosition = tree.GetMappedLineSpan(fullNode.Span).StartLinePosition;

                    this.CharacterHeight = characterHeight;
                    this.RenderPosition = new Point((nodeStartPosition.Character - mappedCaretPosition.Character - 1) * characterWidth + offsetX, offsetY);

                    await RenderDescription(symbols[0]);
                }
                else
                {
                    this.IsVisible = false;
                }

                /*ISymbol symbol = info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);

                if (symbol != null)
                {
                    string documentationId = symbol.GetDocumentationCommentId();
                    documentationXml = symbol.GetDocumentationCommentXml();

                    if (string.IsNullOrEmpty(documentationXml) && !string.IsNullOrEmpty(documentationId))
                    {
                        (await GetReferenceDocumentation()).TryGetValue(documentationId, out documentationXml);
                    }
                }*/
            }
            else
            {
                this.IsVisible = false;
            }
        }

        private void AddSymbols<TSymbol1, TSymbol2>(IEnumerable<ISymbol> originalSymbols, List<ISymbol> symbols, SymbolKind kind, SymbolKind kind2, Func<TSymbol1, IEnumerable<ISymbol>> GetSiblings, Func<TSymbol1, bool> IsSuitable1, Func<TSymbol1, TSymbol2, bool> IsSuitable2)
        {
            foreach (ISymbol symbol in from el in originalSymbols where el.Kind == kind select el)
            {
                TSymbol1 method = (TSymbol1)symbol;

                if (!symbols.Contains(symbol) && IsSuitable1(method))
                {
                    symbols.Add(symbol);
                }

                foreach (ISymbol symbol2 in from el in GetSiblings(method) where el.Kind == kind2 && IsSuitable2(method, (TSymbol2)el) select el)
                {
                    if (!symbols.Contains(symbol2))
                    {
                        symbols.Add(symbol2);
                    }
                }
            }
        }

        public MethodOverloadList()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<Path>("UpPath").PointerPressed += async (s, e) =>
            {
                if (SelectedIndex > 0)
                {
                    SelectedIndex--;
                    await RenderDescription(Items[SelectedIndex]);
                }
            };
            this.FindControl<Path>("DownPath").PointerPressed += async (s, e) =>
            {
                if (SelectedIndex < Items.Count - 1)
                {
                    SelectedIndex++;
                    await RenderDescription(Items[SelectedIndex]);
                }
            };
        }
    }
}
