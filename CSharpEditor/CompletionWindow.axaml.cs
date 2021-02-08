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
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal class CompletionWindow : UserControl
    {
        public Document Document { get; set; }
        public CompletionService CompletionService { get; set; }

        public IReadOnlyList<MetadataReference> References { get; set; }

        public int MaxWindowHeight { get; set; } = 213;

        public bool AnchorOnTop { get; set; }

        public double TopAnchor { get; set; }

        public int SelectedIndex
        {
            get
            {
                return CompletionListControl.SelectedIndex;
            }
        }

        public async Task SetSelectedIndex(int index)
        {
            CompletionListControl.SelectedIndex = index;
            CompletionListControl.InvalidateVisual();

            if (!UpdatingItems && CompletionListControl.SelectedIndex >= 0)
            {
                if (!CompletionListControl.Hidden[CompletionListControl.SelectedIndex])
                {
                    CompletionItem item = CompletionListControl.Items[CompletionListControl.SelectedIndex].item;
                    await GetSymbolComments(item);
                }
                else
                {
                    this.FindControl<Border>("DocumentationBorder").IsVisible = false;
                    await UpdateScrollViewer();
                }
            }
        }

        private async Task UpdateScrollViewer()
        {
            int eff = EffectiveSelectedIndex;

            if (eff >= 0)
            {
                Vector newOffset = new Vector(this.FindControl<ScrollViewer>("ItemsScrollViewer").Offset.X, Math.Max(Math.Min(eff * 20, this.FindControl<ScrollViewer>("ItemsScrollViewer").Offset.Y), eff * 20 + 20 - (this.Height - 33)));

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.FindControl<ScrollViewer>("ItemsScrollViewer").Offset = newOffset;
                }, DispatcherPriority.Layout);
            }
        }

        public int VisibleItems { get; private set; }

        async Task GetSymbolComments(CompletionItem item)
        {
            await UpdateScrollViewer();

            CompletionDescription description = await CompletionService.GetDescriptionAsync(Document, item);

            SourceText currText = await Document.GetTextAsync();
            SourceText newText = currText.Replace(item.Span, item.DisplayText);

            SyntaxTree tree = await Document.WithText(newText).GetSyntaxTreeAsync();

            CSharpCompilation comp = CSharpCompilation.Create("documentation", new[] { tree }, References);
            SemanticModel model = comp.GetSemanticModel(tree);

            TextSpan targetSpan = new TextSpan(item.Span.Start, item.DisplayText.Length);

            string documentationXml = "";

            IEnumerable<SyntaxNode> nodes = tree.GetRoot().DescendantNodes(targetSpan);

            if (nodes.Any())
            {
                SyntaxNode node = nodes.FirstOrDefault(n => (targetSpan.Contains(n.Span) && !n.IsKind(SyntaxKind.IncompleteMember)));

                if (node != null)
                {
                    SymbolInfo info = model.GetSymbolInfo(node);

                    ISymbol symbol = info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);

                    if (symbol != null)
                    {
                        string documentationId = symbol.GetDocumentationCommentId();
                        documentationXml = symbol.GetDocumentationCommentXml();

                        if (string.IsNullOrEmpty(documentationXml) && !string.IsNullOrEmpty(documentationId))
                        {
                            (await Utils.GetReferenceDocumentation()).TryGetValue(documentationId, out documentationXml);
                        }
                    }
                }
            }

            ShowDocumentationComments(description, documentationXml, EffectiveSelectedIndex * 20 - this.FindControl<ScrollViewer>("ItemsScrollViewer").Offset.Y);
        }

        double shifterX = 1;
        double shifterY = 1;

        private void ShowDocumentationComments(CompletionDescription description, string documentationXml, double offset)
        {
            if (string.IsNullOrEmpty(description.Text) && string.IsNullOrEmpty(documentationXml))
            {
                this.FindControl<Border>("DocumentationBorder").IsVisible = false;
            }
            else
            {
                VectSharp.Font labelFont = new VectSharp.Font(Editor.OpenSansRegular, this.FontSize);
                VectSharp.Font codeFont = new VectSharp.Font(Editor.RobotoMonoRegular, this.FontSize);

                double thisX = 0;
                double thisY = 0;

                if (this.RenderTransform != null)
                {
                    thisY = ((TranslateTransform)this.RenderTransform).Y;
                    thisX = ((TranslateTransform)this.RenderTransform).X;
                }

                double maxHeight = this.Parent.Bounds.Height - thisY - offset - 10 - 20;

                double maxWidthRight = this.Parent.Bounds.Width - thisX - this.Width - 12;
                double maxWidthLeft = thisX - 12;

                Control unlimitedContent = FormattedText.FormatDescription(description.TaggedParts, documentationXml, labelFont, codeFont).Render(double.PositiveInfinity, false);
                unlimitedContent.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                unlimitedContent.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

                if (unlimitedContent.Width < maxWidthRight)
                {
                    this.FindControl<ScrollViewer>("DocumentationScrollViewer").Content = unlimitedContent;
                    this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

                    this.FindControl<Border>("DocumentationBorder").Height = Math.Min(maxHeight, unlimitedContent.Height + 4);

                    if (unlimitedContent.Height + 4 > maxHeight)
                    {
                        this.FindControl<Border>("DocumentationBorder").Width = unlimitedContent.Width + 12 + 20;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    }
                    else
                    {
                        this.FindControl<Border>("DocumentationBorder").Width = unlimitedContent.Width + 12;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }

                    this.FindControl<Border>("DocumentationBorder").RenderTransform = new TranslateTransform() { X = this.Width, Y = offset };
                }
                else if (unlimitedContent.Width < maxWidthLeft)
                {
                    this.FindControl<ScrollViewer>("DocumentationScrollViewer").Content = unlimitedContent;
                    this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

                    this.FindControl<Border>("DocumentationBorder").Height = Math.Min(maxHeight, unlimitedContent.Height + 4);

                    if (unlimitedContent.Height + 4 > maxHeight)
                    {
                        this.FindControl<Border>("DocumentationBorder").Width = unlimitedContent.Width + 12 + 20;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    }
                    else
                    {
                        this.FindControl<Border>("DocumentationBorder").Width = unlimitedContent.Width + 12;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }

                    this.FindControl<Border>("DocumentationBorder").RenderTransform = new TranslateTransform() { X = -this.FindControl<Border>("DocumentationBorder").Width, Y = offset };
                }
                else if (maxWidthRight >= maxWidthLeft)
                {
                    Control content = FormattedText.FormatDescription(description.TaggedParts, documentationXml, labelFont, codeFont).Render(maxWidthRight - 20 - 12, false);
                    content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                    content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

                    this.FindControl<ScrollViewer>("DocumentationScrollViewer").Content = content;

                    if (content.Width + 12 <= maxWidthRight && content.Height + 4 <= maxHeight)
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                        this.FindControl<Border>("DocumentationBorder").Width = content.Width + 12;
                        this.FindControl<Border>("DocumentationBorder").Height = content.Height + 4;
                    }
                    else if (content.Width + 12 + 20 <= maxWidthRight && content.Height + 4 > maxHeight)
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

                        this.FindControl<Border>("DocumentationBorder").Width = content.Width + 12 + 20;
                        this.FindControl<Border>("DocumentationBorder").Height = maxHeight + (shifterY *= -1);
                    }
                    else if (content.Width + 12 > maxWidthRight && content.Height + 4 + 20 <= maxHeight)
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                        this.FindControl<Border>("DocumentationBorder").Width = maxWidthRight + (shifterX *= -1);
                        this.FindControl<Border>("DocumentationBorder").Height = content.Height + 4 + 20;
                    }
                    else
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

                        this.FindControl<Border>("DocumentationBorder").Width = maxWidthRight + (shifterX *= -1);
                        this.FindControl<Border>("DocumentationBorder").Height = maxHeight + (shifterY *= -1);
                    }

                    this.FindControl<Border>("DocumentationBorder").RenderTransform = new TranslateTransform() { X = this.Width, Y = offset };
                }
                else
                {
                    Control content = FormattedText.FormatDescription(description.TaggedParts, documentationXml, labelFont, codeFont).Render(maxWidthLeft - 20 - 12, false);
                    content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                    content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

                    this.FindControl<ScrollViewer>("DocumentationScrollViewer").Content = content;

                    if (content.Width + 12 <= maxWidthLeft && content.Height + 4 <= maxHeight)
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                        this.FindControl<Border>("DocumentationBorder").Width = content.Width + 12;
                        this.FindControl<Border>("DocumentationBorder").Height = content.Height + 4;
                    }
                    else if (content.Width + 12 + 20 <= maxWidthLeft && content.Height + 4 > maxHeight)
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

                        this.FindControl<Border>("DocumentationBorder").Width = content.Width + 12 + 20;
                        this.FindControl<Border>("DocumentationBorder").Height = maxHeight + (shifterY *= -1);
                    }
                    else if (content.Width + 12 > maxWidthLeft && content.Height + 4 + 20 <= maxHeight)
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                        this.FindControl<Border>("DocumentationBorder").Width = maxWidthLeft + (shifterX *= -1);
                        this.FindControl<Border>("DocumentationBorder").Height = content.Height + 4 + 20;
                    }
                    else
                    {
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                        this.FindControl<ScrollViewer>("DocumentationScrollViewer").VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

                        this.FindControl<Border>("DocumentationBorder").Width = maxWidthLeft + (shifterX *= -1);
                        this.FindControl<Border>("DocumentationBorder").Height = maxHeight + (shifterY *= -1);
                    }

                    this.FindControl<Border>("DocumentationBorder").RenderTransform = new TranslateTransform() { X = -this.FindControl<Border>("DocumentationBorder").Width, Y = offset };
                }
                this.FindControl<Border>("DocumentationBorder").IsVisible = true;
            }
        }



        public int EffectiveSelectedIndex
        {
            get
            {
                if (CompletionListControl.SelectedIndex >= 0 && !CompletionListControl.Hidden[CompletionListControl.SelectedIndex])
                {
                    int prevItems = 0;

                    for (int i = 0; i < Math.Min(CompletionListControl.SelectedIndex + 1, TotalItems); i++)
                    {
                        if (!CompletionListControl.Hidden[i])
                        {
                            prevItems++;
                        }
                    }

                    return prevItems - 1;
                }
                else
                {
                    return -1;
                }
            }
        }

        public int TotalItems { get; private set; }

        internal event EventHandler<CompletionCommitEventArgs> Committed;

        public string FilterText { get; private set; } = null;

        public async Task SetFilterText(string text)
        {
            FilterText = text;
            CompletionListControl.Filter = text;
            await UpdateFilter();
        }

        public CompletionList CompletionList { get; private set; }

        public async Task SetCompletionList(CompletionList list)
        {
            UpdatingItems = true;
            CompletionItem previousSelectedItem = null;

            if (SelectedIndex >= 0)
            {
                previousSelectedItem = CompletionListControl.Items[SelectedIndex].item;
            }

            Vector? previousOffset = this.IsVisible ? (Vector?)this.FindControl<ScrollViewer>("ItemsScrollViewer").Offset : null;

            await SetSelectedIndex(-1);

            CompletionList = list;

            List<(CompletionListControl.IconTypes icon, CompletionItem item)> itemList = new List<(CompletionListControl.IconTypes icon, CompletionItem item)>();

            bool[] filters = new bool[FilterButtons.Count];

            int index = 0;
            int newSelectedIndex = -1;

            foreach (CompletionItem item in list.Items)
            {
                if (!string.IsNullOrEmpty(item.DisplayText))
                {
                    if (previousSelectedItem != null)
                    {
                        if (newSelectedIndex < 0 && item.DisplayText == previousSelectedItem.DisplayText && item.DisplayTextPrefix == previousSelectedItem.DisplayTextPrefix && item.DisplayTextSuffix == previousSelectedItem.DisplayTextSuffix && item.FilterText == previousSelectedItem.FilterText && item.SortText == previousSelectedItem.SortText)
                        {
                            newSelectedIndex = index;
                        }
                    }

                    CompletionListControl.IconTypes icon;

                    if (item.Properties.ContainsKey("SymbolKind"))
                    {
                        switch ((SymbolKind)(int.Parse(item.Properties["SymbolKind"])))
                        {
                            case SymbolKind.Property:
                                icon = CompletionListControl.IconTypes.Property;
                                filters[8] = true;
                                break;
                            case SymbolKind.Event:
                                icon = CompletionListControl.IconTypes.Event;
                                filters[7] = true;
                                break;
                            case SymbolKind.Field:
                                icon = CompletionListControl.IconTypes.Field;
                                filters[6] = true;
                                break;
                            case SymbolKind.Method:
                                icon = CompletionListControl.IconTypes.Method;
                                filters[9] = true;
                                break;
                            case SymbolKind.NamedType:
                            case SymbolKind.DynamicType:
                            case SymbolKind.ArrayType:
                            case SymbolKind.PointerType:
                                if (item.Tags.Contains(WellKnownTags.Class))
                                {
                                    icon = CompletionListControl.IconTypes.Class;
                                    filters[1] = true;
                                }
                                else if (item.Tags.Contains(WellKnownTags.Delegate))
                                {
                                    icon = CompletionListControl.IconTypes.Delegate;
                                    filters[5] = true;
                                }
                                else if (item.Tags.Contains(WellKnownTags.Enum))
                                {
                                    icon = CompletionListControl.IconTypes.Enum;
                                    filters[4] = true;
                                }
                                else if (item.Tags.Contains(WellKnownTags.Structure))
                                {
                                    icon = CompletionListControl.IconTypes.Struct;
                                    filters[2] = true;
                                }
                                else if (item.Tags.Contains(WellKnownTags.Interface))
                                {
                                    icon = CompletionListControl.IconTypes.Interface;
                                    filters[3] = true;
                                }
                                else
                                {
                                    icon = CompletionListControl.IconTypes.Unknown;
                                    filters[12] = true;
                                }

                                break;
                            case SymbolKind.Namespace:
                                icon = CompletionListControl.IconTypes.Namespace;
                                filters[0] = true;
                                break;
                            case SymbolKind.Local:
                            case SymbolKind.Parameter:
                                icon = CompletionListControl.IconTypes.Local;
                                filters[10] = true;
                                break;
                            default:
                                icon = CompletionListControl.IconTypes.Unknown;
                                filters[12] = true;
                                break;
                        }
                    }
                    else if (item.Tags.Contains(WellKnownTags.Keyword))
                    {
                        icon = CompletionListControl.IconTypes.Keyword;
                        filters[11] = true;
                    }
                    else if (item.Tags.Contains(WellKnownTags.Local) || item.Tags.Contains(WellKnownTags.Parameter))
                    {
                        icon = CompletionListControl.IconTypes.Local;
                        filters[10] = true;
                    }
                    else if (item.Tags.Contains(WellKnownTags.Class))
                    {
                        icon = CompletionListControl.IconTypes.Class;
                        filters[1] = true;
                    }
                    else if (item.Tags.Contains(WellKnownTags.Enum))
                    {
                        icon = CompletionListControl.IconTypes.Enum;
                        filters[4] = true;
                    }
                    else
                    {
                        icon = CompletionListControl.IconTypes.Unknown;
                        filters[12] = true;
                    }

                    itemList.Add((icon, item));

                    index++;
                }
            }

            this.CompletionListControl.Items = ImmutableList.Create(itemList.ToArray());

            for (int i = 0; i < FilterButtons.Count; i++)
            {
                FilterButtons[i].IsVisible = filters[i];

                if (!this.IsVisible)
                {
                    FilterButtons[i].IsChecked = false;
                }
            }

            this.Width = Math.Max(filters.Count(a => a) * 30 + 2, CompletionListControl.MaxItemTextWidth + 2 + 2 + 20 + 25);

            if (this.RenderTransform != null)
            {
                double currX = ((TranslateTransform)this.RenderTransform).X;
                double currY = ((TranslateTransform)this.RenderTransform).Y;

                this.RenderTransform = new TranslateTransform(Math.Min(this.Parent.Bounds.Width - this.Width, currX), currY);
            }

            UpdatingItems = false;

            await UpdateFilter();

            if (previousOffset != null)
            {
                this.FindControl<ScrollViewer>("ItemsScrollViewer").Offset = previousOffset.Value;
            }

            if (newSelectedIndex >= 0 || (SelectedIndex < 0 && index > 0))
            {
                await SetSelectedIndex(Math.Max(0, newSelectedIndex));
            }

            TotalItems = index;

            this.CompletionListControl.InvalidateMeasure();
            this.CompletionListControl.InvalidateVisual();
        }

        private readonly List<ToggleButton> FilterButtons = new List<ToggleButton>();
        private bool UpdatingItems = false;

        public CompletionWindow()
        {
            this.InitializeComponent();

            this.Focusable = false;

            foreach (Control control in this.FindControl<StackPanel>("FilterContainer").Children)
            {
                FilterButtons.Add(control as ToggleButton);
                control.PropertyChanged += async (s, e) =>
                {
                    if (!UpdatingItems && e.Property == ToggleButton.IsCheckedProperty)
                    {
                        await UpdateFilter();
                    }
                };
            }
        }

        private async Task UpdateFilter()
        {
            bool[] filters = new bool[FilterButtons.Count];

            for (int i = 0; i < FilterButtons.Count; i++)
            {
                filters[i] = FilterButtons[i].IsVisible && FilterButtons[i].IsChecked == true;
            }

            bool[] foundFilters = new bool[FilterButtons.Count];
            bool[] foundButFilteredFilters = new bool[FilterButtons.Count];

            CompletionListControl.Filter = FilterText;

            string filter = FilterText?.Trim();

            int index = 0;
            int firstVisibleIndex = -1;
            int visibleCount = 0;
            int firstExactMatch = -1;

            if (!filters.Any(a => a))
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    filters[i] = true;
                }
            }

            for (int i = 0; i < CompletionListControl.Items.Count; i++)
            {
                CompletionItem item = CompletionListControl.Items[i].item;

                int foundIndex = -1;

                if (item.Properties.ContainsKey("SymbolKind"))
                {
                    switch ((SymbolKind)(int.Parse(item.Properties["SymbolKind"])))
                    {
                        case SymbolKind.Property:
                            CompletionListControl.Hidden[i] = !filters[8];
                            foundIndex = 8;
                            break;
                        case SymbolKind.Event:
                            CompletionListControl.Hidden[i] = !filters[7];
                            foundIndex = 7;
                            break;
                        case SymbolKind.Field:
                            CompletionListControl.Hidden[i] = !filters[6];
                            foundIndex = 6;
                            break;
                        case SymbolKind.Method:
                            CompletionListControl.Hidden[i] = !filters[9];
                            foundIndex = 9;
                            break;
                        case SymbolKind.NamedType:
                        case SymbolKind.DynamicType:
                        case SymbolKind.ArrayType:
                        case SymbolKind.PointerType:
                            if (item.Tags.Contains(WellKnownTags.Class))
                            {
                                CompletionListControl.Hidden[i] = !filters[1];
                                foundIndex = 1;
                            }
                            else if (item.Tags.Contains(WellKnownTags.Delegate))
                            {
                                CompletionListControl.Hidden[i] = !filters[5];
                                foundIndex = 5;
                            }
                            else if (item.Tags.Contains(WellKnownTags.Enum))
                            {
                                CompletionListControl.Hidden[i] = !filters[4];
                                foundIndex = 4;
                            }
                            else if (item.Tags.Contains(WellKnownTags.Structure))
                            {
                                CompletionListControl.Hidden[i] = !filters[2];
                                foundIndex = 2;
                            }
                            else if (item.Tags.Contains(WellKnownTags.Interface))
                            {
                                CompletionListControl.Hidden[i] = !filters[3];
                                foundIndex = 3;
                            }
                            else
                            {
                                CompletionListControl.Hidden[i] = !filters[12];
                                foundIndex = 12;
                            }
                            break;
                        case SymbolKind.Namespace:
                            CompletionListControl.Hidden[i] = !filters[0];
                            foundIndex = 0;
                            break;
                        case SymbolKind.Local:
                        case SymbolKind.Parameter:
                            CompletionListControl.Hidden[i] = !filters[10];
                            foundIndex = 10;
                            break;
                        default:
                            CompletionListControl.Hidden[i] = !filters[12];
                            foundIndex = 12;
                            break;
                    }
                }
                else if (item.Tags.Contains(WellKnownTags.Keyword))
                {
                    CompletionListControl.Hidden[i] = !filters[11];
                    foundIndex = 11;
                }
                else if (item.Tags.Contains(WellKnownTags.Local) || item.Tags.Contains(WellKnownTags.Parameter))
                {
                    CompletionListControl.Hidden[i] = !filters[10];
                    foundIndex = 10;
                }
                else if (item.Tags.Contains(WellKnownTags.Class))
                {
                    CompletionListControl.Hidden[i] = !filters[1];
                    foundIndex = 1;
                }
                else if (item.Tags.Contains(WellKnownTags.Enum))
                {
                    CompletionListControl.Hidden[i] = !filters[4];
                    foundIndex = 4;
                }
                else
                {
                    CompletionListControl.Hidden[i] = !filters[12];
                    foundIndex = 12;
                }

                if (string.IsNullOrEmpty(filter))
                {
                    foundButFilteredFilters[foundIndex] = true;
                    //CompletionListControl.Hidden[i] |= false;
                }
                else if (item.FilterText.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                {
                    foundButFilteredFilters[foundIndex] = true;
                    //CompletionListControl.Hidden[i] |= false;
                }
                else
                {
                    CompletionListControl.Hidden[i] = true;
                }

                if (!CompletionListControl.Hidden[i])
                {
                    foundFilters[foundIndex] = true;
                }


                if (firstVisibleIndex < 0 && !CompletionListControl.Hidden[i])
                {
                    firstVisibleIndex = index;
                }

                if (!CompletionListControl.Hidden[i])
                {
                    visibleCount++;

                    if (!string.IsNullOrEmpty(filter) && item.FilterText.StartsWith(filter) && firstExactMatch < 0)
                    {
                        firstExactMatch = index;
                    }
                }

                index++;
            }

            for (int i = 0; i < foundFilters.Length; i++)
            {
                ((Control)this.FindControl<StackPanel>("FilterContainer").Children[i]).IsEnabled = foundFilters[i] || foundButFilteredFilters[i];

                if (((Control)this.FindControl<StackPanel>("FilterContainer").Children[i]).IsEnabled)
                {
                    ((Control)this.FindControl<StackPanel>("FilterContainer").Children[i]).Opacity = 1;
                }
                else
                {
                    ((Control)this.FindControl<StackPanel>("FilterContainer").Children[i]).Opacity = 0.25;
                }
            }


            if ((SelectedIndex < 0 || (filter != null && !CompletionListControl.Items[CompletionListControl.SelectedIndex].item.FilterText.StartsWith(filter))) && firstExactMatch > 0)
            {
                await SetSelectedIndex(firstExactMatch);
            }
            else if ((SelectedIndex < 0 || CompletionListControl.Hidden[CompletionListControl.SelectedIndex]) && firstVisibleIndex >= 0)
            {
                await SetSelectedIndex(firstVisibleIndex);
            }

            if (!AnchorOnTop)
            {
                this.Height = Math.Min(this.MaxWindowHeight, visibleCount * 20 + 33);
            }
            else
            {
                this.Height = Math.Min(this.MaxWindowHeight, visibleCount * 20 + 33);
                double prevX = ((TranslateTransform)this.RenderTransform).X;

                this.RenderTransform = new TranslateTransform(prevX, this.TopAnchor - this.Height);
            }

            VisibleItems = visibleCount;

            await UpdateScrollViewer();

            if (visibleCount == 0)
            {
                this.IsVisible = false;
            }

            CompletionListControl.InvalidateMeasure();

            await SetSelectedIndex(SelectedIndex);
        }

        public async Task UpdateSelectedIndex(int delta)
        {
            try
            {
                int prevSelectedIndex = Math.Max(CompletionListControl.SelectedIndex, 0);

                int index = prevSelectedIndex;

                if (delta > 0)
                {
                    for (int i = prevSelectedIndex + 1; i < TotalItems; i++)
                    {
                        if (delta == 0)
                        {
                            break;
                        }

                        if (!CompletionListControl.Hidden[i])
                        {
                            delta--;
                            index = i;
                        }
                    }
                }
                else
                {
                    for (int i = prevSelectedIndex - 1; i >= 0; i--)
                    {
                        if (delta == 0)
                        {
                            break;
                        }

                        if (!CompletionListControl.Hidden[i])
                        {
                            delta++;
                            index = i;
                        }
                    }
                }

                await SetSelectedIndex(index);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while updating selection: {0}", ex.Message);
            }
        }

        public async Task Commit(CompletionItem item = null)
        {
            if (item == null)
            {
                item = CompletionListControl.Items[CompletionListControl.SelectedIndex].item;
            }

            Committed?.Invoke(this, new CompletionCommitEventArgs(item));

            await SetSelectedIndex(-1);
        }

        public async Task<bool> CheckAndCommit(char insertedChar)
        {
            CompletionItem item = CompletionListControl.Items[CompletionListControl.SelectedIndex].item;

            List<char> commitCharacters = new List<char>(CompletionList.Rules.DefaultCommitCharacters);

            foreach (CharacterSetModificationRule rule in item.Rules.CommitCharacterRules)
            {
                if (rule.Kind == CharacterSetModificationKind.Add)
                {
                    commitCharacters.AddRange(rule.Characters);
                }
                else if (rule.Kind == CharacterSetModificationKind.Remove)
                {
                    foreach (char c in rule.Characters)
                    {
                        commitCharacters.Remove(c);
                    }
                }
                else if (rule.Kind == CharacterSetModificationKind.Replace)
                {
                    commitCharacters = new List<char>(rule.Characters);
                }
            }

            if (commitCharacters.Contains(insertedChar) || insertedChar == '\n' || insertedChar == '\r')
            {
                await Commit(item);
            }

            bool tbr = false;

            if (insertedChar == '\n' || insertedChar == '\r')
            {
                EnterKeyRule enterRule = item.Rules.EnterKeyRule;
                if (enterRule == EnterKeyRule.Default)
                {
                    enterRule = CompletionList.Rules.DefaultEnterKeyRule;
                }

                switch (enterRule)
                {
                    case EnterKeyRule.Always:
                        tbr = false;
                        break;
                    case EnterKeyRule.Never:
                        tbr = true;
                        break;
                    case EnterKeyRule.AfterFullyTypedWord:
                        tbr = FilterText.Equals(item.DisplayText, StringComparison.OrdinalIgnoreCase);
                        break;
                }

            }

            return tbr;
        }

        private CompletionListControl CompletionListControl;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            CompletionListControl = new CompletionListControl(this);

            this.FindControl<ScrollViewer>("ItemsScrollViewer").Content = CompletionListControl;
        }
    }

    internal class CompletionCommitEventArgs : EventArgs
    {
        public CompletionItem Item { get; }

        public CompletionCommitEventArgs(CompletionItem item) : base()
        {
            Item = item;
        }
    }
}
