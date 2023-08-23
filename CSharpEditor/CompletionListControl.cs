﻿/*
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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis.Completion;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace CSharpEditor
{
    internal class CompletionListControl : Control
    {
        public enum IconTypes
        {
            Property = 0,
            Event,
            Field,
            Method,
            Class,
            Delegate,
            Enum,
            Struct,
            Interface,
            Namespace,
            Local,
            Keyword,
            Unknown,
            EnumMember
        }

        /*static CompletionListControl()
        {
            WriteableBitmap bmp = new WriteableBitmap(new PixelSize(16, 16), new Vector(72, 72), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);

            
        }*/

        private ImmutableList<(IconTypes icon, CompletionItem item)> _items;
        public ImmutableList<(IconTypes icon, CompletionItem item)> Items
        {
            get
            {
                return _items;
            }
            set
            {
                _items = value;
                Hidden = new bool[value.Count];
                ComputeMaxItemTextWidth();
            }
        }
        public bool[] Hidden { get; private set; }
        public string Filter { get; set; }

        public int SelectedIndex { get; set; } = -1;

        public double MaxItemTextWidth { get; private set; } = 0;

        private Bitmap[] Icons;

        private FontFamily FontFamily;
        private double FontSize;

        private CompletionWindow CompletionWindow;

        public CompletionListControl(CompletionWindow completionWindow)
        {
            this.Items = ImmutableList<(IconTypes icon, CompletionItem item)>.Empty;

            this.FontFamily = FontFamily.Parse("resm:CSharpEditor.Fonts.?assembly=CSharpEditor#Open Sans");
            this.FontSize = 14;

            this.CompletionWindow = completionWindow;

            this.DoubleTapped += async (s, e) =>
            {
                await this.CompletionWindow.Commit();
            };

            InitializeIcons();
        }

        protected override async void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            double y = e.GetPosition(this).Y;

            int index = (int)Math.Floor(y / 20);

            int actualIndex = -1;

            int found = -1;

            for (int i = 0; i < Hidden.Length; i++)
            {
                if (!Hidden[i])
                {
                    found++;

                    if (found == index)
                    {
                        actualIndex = i;
                        break;
                    }
                }
            }

            await this.CompletionWindow.SetSelectedIndex(actualIndex);
        }


        private void InitializeIcons()
        {
            this.Icons = new Bitmap[14];

            this.Icons[0] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.PropertyIcon.png"));
            this.Icons[1] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.EventIcon.png"));
            this.Icons[2] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.FieldIcon.png"));
            this.Icons[3] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.MethodIcon.png"));
            this.Icons[4] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.ClassIcon.png"));
            this.Icons[5] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.DelegateIcon.png"));
            this.Icons[6] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.EnumIcon.png"));
            this.Icons[7] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.StructIcon.png"));
            this.Icons[8] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.InterfaceIcon.png"));
            this.Icons[9] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.NamespaceIcon.png"));
            this.Icons[10] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.LocalIcon.png"));
            this.Icons[11] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.KeywordIcon.png"));
            this.Icons[12] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.UnknownIcon.png"));
            this.Icons[13] = new Bitmap(this.GetType().Assembly.GetManifestResourceStream("CSharpEditor.IntellisenseIconsPNG.EnumMemberIcon.png"));
        }

        private void ComputeMaxItemTextWidth()
        {
            Typeface face = new Typeface(this.FontFamily);

            double maxWidth = 0;

            for (int i = 0; i < Items.Count; i++)
            {
                Avalonia.Media.TextFormatting.TextLayout lay = new Avalonia.Media.TextFormatting.TextLayout(Items[i].item.DisplayTextPrefix + Items[i].item.DisplayText + Items[i].item.DisplayTextSuffix, face, this.FontSize, Brushes.Black);
                maxWidth = Math.Max(maxWidth, lay.Width);
            }

            this.MaxItemTextWidth = maxWidth;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(MaxItemTextWidth, Hidden.Where(a => !a).Count() * 20);
        }

        SolidColorBrush SelectionBrush = new SolidColorBrush(Color.FromRgb(196, 213, 255));
        SolidColorBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(247, 249, 254));
        public override void Render(DrawingContext context)
        {
            context.FillRectangle(BackgroundBrush, new Rect(0, 0, this.Bounds.Width, this.Bounds.Height));

            Typeface face = new Typeface(this.FontFamily);
            Typeface boldFace = new Typeface(this.FontFamily, weight: FontWeight.Bold);

            int index = 0;

            for (int i = 0; i < Items.Count; i++)
            {
                if (!Hidden[i])
                {
                    if (i == SelectedIndex)
                    {
                        context.FillRectangle(SelectionBrush, new Rect(20, index * 20, this.MaxItemTextWidth + 2 + 5, 20));
                    }
                    context.DrawImage(this.Icons[(int)Items[i].icon], new Rect(2, index * 20 + 2, 16, 16));

                    double x = 22;

                    if (!string.IsNullOrEmpty(Items[i].item.DisplayTextPrefix))
                    {
                        Avalonia.Media.FormattedText prefix = new Avalonia.Media.FormattedText(Items[i].item.DisplayTextPrefix, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, this.FontSize, Brushes.Black);
                        context.DrawText(prefix, new Point(x, index * 20));
                        x += prefix.Width;
                    }

                    if (Filter != null && Items[i].item.DisplayText.StartsWith(Filter, StringComparison.OrdinalIgnoreCase))
                    {
                        Avalonia.Media.FormattedText filter = new Avalonia.Media.FormattedText(Items[i].item.DisplayText.Substring(0, Filter.Length), System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight, boldFace, this.FontSize, Brushes.Black);
                        context.DrawText(filter, new Point(x, index * 20));
                        x += filter.Width;

                        Avalonia.Media.FormattedText name = new Avalonia.Media.FormattedText(Items[i].item.DisplayText.Substring(Filter.Length), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, this.FontSize, Brushes.Black);
                        context.DrawText(name, new Point(x, index * 20));
                        x += name.Width;
                    }
                    else
                    {
                        Avalonia.Media.FormattedText name = new Avalonia.Media.FormattedText(Items[i].item.DisplayText, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, this.FontSize, Brushes.Black);
                        context.DrawText(name, new Point(x, index * 20));
                        x += name.Width;
                    }

                    if (!string.IsNullOrEmpty(Items[i].item.DisplayTextSuffix))
                    {
                        Avalonia.Media.FormattedText suffix = new Avalonia.Media.FormattedText(Items[i].item.DisplayTextSuffix, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, this.FontSize, Brushes.Black);
                        context.DrawText(suffix, new Point(x, index * 20));
                    }

                    index++;
                }
            }
        }

        private void DrawIcon(Control icon, double x, double y, DrawingContext context)
        {
            icon.Measure(new Size(16, 16));
            using (context.PushTransform(Matrix.CreateTranslation(x, y)))
            {
                foreach (Control ctrl in icon.GetVisualDescendants())
                {
                    ctrl.Render(context);
                }
            }
        }
    }
}
