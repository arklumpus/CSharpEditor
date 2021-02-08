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
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal class SymbolToolTip : UserControl
    {
        internal Document Document;
        internal IReadOnlyList<MetadataReference> References;

        public Point RenderPosition { get; set; }

        public SymbolToolTip()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async Task SetContent(Document document, int caretPosition, double offsetX, double offsetY, double lineHeight)
        {
            this.Document = document;

            ISymbol symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, caretPosition);

            if (symbol != null)
            {
                this.RenderPosition = new Point(offsetX, offsetY);
                await RenderDescription(symbol, lineHeight);
            }
            else
            {
                this.IsVisible = false;
            }
        }

        public void SetContent(List<Diagnostic> diagnostics, double offsetX, double offsetY, double lineHeight)
        {
            if (diagnostics != null && diagnostics.Count > 0)
            {
                this.RenderPosition = new Point(offsetX, offsetY);
                RenderDescription(diagnostics, lineHeight);
            }
            else
            {
                this.IsVisible = false;
            }
        }


        public async Task RenderDescription(ISymbol symbol, double lineHeight)
        {
            VectSharp.Font labelFont = new VectSharp.Font(Editor.OpenSansRegular, this.FontSize);
            VectSharp.Font codeFont = new VectSharp.Font(Editor.RobotoMonoRegular, this.FontSize);
            VectSharp.Font parameterNameFont = new VectSharp.Font(Editor.OpenSansBoldItalic, this.FontSize);
            VectSharp.Font parameterDescriptionFont = new VectSharp.Font(Editor.OpenSansItalic, this.FontSize);

            double availableWidth = this.Parent.Bounds.Width - 22 - 14 - 5;

            double availableHeightBottom = this.Parent.Bounds.Height - RenderPosition.Y - 22 - 4;
            double availableHeightTop = RenderPosition.Y - lineHeight;

            ImmutableArray<SymbolDisplayPart> parts = symbol.ToDisplayParts(/*SemanticModel, Position, */new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
                memberOptions: SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeExplicitInterface,
                delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
                extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeOptionalBrackets,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                localOptions: SymbolDisplayLocalOptions.IncludeType | SymbolDisplayLocalOptions.IncludeConstantValue,
                kindOptions: SymbolDisplayKindOptions.IncludeNamespaceKeyword | SymbolDisplayKindOptions.IncludeTypeKeyword | SymbolDisplayKindOptions.IncludeMemberKeyword,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            ));

            string documentationId = symbol.GetDocumentationCommentId();
            string documentationXml = symbol.GetDocumentationCommentXml();

            if (string.IsNullOrEmpty(documentationXml) && !string.IsNullOrEmpty(documentationId))
            {
                (await Utils.GetReferenceDocumentation()).TryGetValue(documentationId, out documentationXml);
            }

            FormattedText description = FormattedText.FormatDescription(from el in parts select el.ToTaggedText(), documentationXml, labelFont, codeFont);

            if (description.Paragraphs.Count == 1)
            {
                description.Paragraphs[0].SpaceAfter = description.Paragraphs[0].Lines[0].GetAverageFontSize() * 0.4;
            }

            Canvas iconCanvas = new Canvas() { Width = 16, Height = 16 };

            switch (symbol.Kind)
            {
                case SymbolKind.Property:
                    iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.PropertyIcon(), Width = 16, Height = 16 });
                    break;
                case SymbolKind.Event:
                    iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.EventIcon(), Width = 16, Height = 16 });
                    break;
                case SymbolKind.Field:
                    iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.FieldIcon(), Width = 16, Height = 16 });
                    break;
                case SymbolKind.Method:
                    iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.MethodIcon(), Width = 16, Height = 16 });
                    break;
                case SymbolKind.NamedType:
                case SymbolKind.DynamicType:
                case SymbolKind.ArrayType:
                case SymbolKind.PointerType:
                    switch (((ITypeSymbol)symbol).TypeKind)
                    {
                        case TypeKind.Class:
                            iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.ClassIcon(), Width = 16, Height = 16 });
                            break;
                        case TypeKind.Struct:
                            iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.StructIcon(), Width = 16, Height = 16 });
                            break;
                        case TypeKind.Interface:
                            iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.InterfaceIcon(), Width = 16, Height = 16 });
                            break;
                        case TypeKind.Enum:
                            iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.EnumIcon(), Width = 16, Height = 16 });
                            break;
                        case TypeKind.Delegate:
                            iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.DelegateIcon(), Width = 16, Height = 16 });
                            break;
                        default:
                            iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.UnknownIcon(), Width = 16, Height = 16 });
                            break;
                    }
                    break;
                case SymbolKind.Namespace:
                    iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.NamespaceIcon(), Width = 16, Height = 16 });
                    break;
                case SymbolKind.Local:
                case SymbolKind.Parameter:
                    iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.LocalIcon(), Width = 16, Height = 16 });
                    break;
                default:
                    iconCanvas.Children.Add(new Viewbox() { Child = new IntellisenseIcon.UnknownIcon(), Width = 16, Height = 16 });
                    break;
            }

            Canvas content = description.Render(availableWidth, false, iconCanvas);
            content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

            double desiredWidth = content.Width;
            double desiredHeight = content.Height;

            if (desiredHeight <= availableHeightBottom)
            {
                this.Width = Math.Min(availableWidth, desiredWidth) + 10 + 4;
                this.Height = Math.Max(desiredHeight, 25) + 4;

                this.FindControl<Border>("ContainerBorder").Child = content;
                this.FindControl<Border>("ContainerBorder").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<Border>("ContainerBorder").Height = content.Height;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y };

                this.IsVisible = true;

            }
            else if (desiredHeight <= availableHeightTop)
            {
                this.Width = Math.Min(availableWidth, desiredWidth) + 10 + 4;
                this.Height = Math.Max(desiredHeight, 25) + 4;

                this.FindControl<Border>("ContainerBorder").Child = content;
                this.FindControl<Border>("ContainerBorder").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<Border>("ContainerBorder").Height = content.Height;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y - this.Height- lineHeight };

                this.IsVisible = true;
            }
            else
            {
                this.IsVisible = false;
            }
        }

        public void RenderDescription(List<Diagnostic> diagnostics, double lineHeight)
        {
            VectSharp.Font labelFont = new VectSharp.Font(Editor.OpenSansRegular, this.FontSize);
            VectSharp.Font codeFont = new VectSharp.Font(Editor.RobotoMonoRegular, this.FontSize);

            double availableWidth = this.Parent.Bounds.Width - 22 - 14 - 5;

            double availableHeightBottom = this.Parent.Bounds.Height - RenderPosition.Y - 22 - 4;
            double availableHeightTop = RenderPosition.Y - lineHeight;

            StackPanel contentPanel = new StackPanel();

            double desiredWidth = 0;
            double desiredHeight = 0;

            foreach (Diagnostic diagnostic in diagnostics)
            {
                FormattedText description = FormattedText.FormatDescription(FormattedText.TokenizeText(diagnostic.Id + ": " + diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)), null, labelFont, codeFont);

                if (description.Paragraphs.Count == 1)
                {
                    description.Paragraphs[0].SpaceAfter = description.Paragraphs[0].Lines[0].GetAverageFontSize() * 0.4;
                }

                Canvas iconCanvas = new Canvas() { Width = 16, Height = 16 };

                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        iconCanvas.Children.Add(new Viewbox() { Child = new DiagnosticIcons.ErrorIcon(), Width = 16, Height = 16 });
                        break;
                    case DiagnosticSeverity.Warning:
                        iconCanvas.Children.Add(new Viewbox() { Child = new DiagnosticIcons.WarningIcon(), Width = 16, Height = 16 });
                        break;
                }

                Canvas content = description.Render(availableWidth, false, iconCanvas, true);
                content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

                contentPanel.Children.Add(content);
                desiredWidth = Math.Max(desiredWidth, content.Width);
                desiredHeight += content.Height;
            }

            if (desiredHeight <= availableHeightBottom)
            {
                this.Width = Math.Min(availableWidth, desiredWidth) + 10 + 4;
                this.Height = Math.Max(desiredHeight, 25) + 4;

                this.FindControl<Border>("ContainerBorder").Child = contentPanel;
                this.FindControl<Border>("ContainerBorder").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<Border>("ContainerBorder").Height = desiredHeight;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y };

                this.IsVisible = true;

            }
            else if (desiredHeight <= availableHeightTop)
            {
                this.Width = Math.Min(availableWidth, desiredWidth) + 10 + 4;
                this.Height = Math.Max(desiredHeight, 25) + 4;

                this.FindControl<Border>("ContainerBorder").Child = contentPanel;
                this.FindControl<Border>("ContainerBorder").Width = Math.Min(availableWidth, desiredWidth) + 10;
                this.FindControl<Border>("ContainerBorder").Height = desiredHeight;

                this.RenderTransform = new TranslateTransform() { X = Math.Min(RenderPosition.X, this.Parent.Bounds.Width - 22 - this.Width), Y = RenderPosition.Y - this.Height - lineHeight };

                this.IsVisible = true;
            }
            else
            {
                this.IsVisible = false;
            }
        }
    }
}
