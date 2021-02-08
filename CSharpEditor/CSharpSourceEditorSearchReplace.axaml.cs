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

namespace CSharpEditor
{
    internal class CSharpSourceEditorSearchReplace : UserControl
    {
        public static readonly DirectProperty<CSharpSourceEditorSearchReplace, bool> HasFocusProperty =
        AvaloniaProperty.RegisterDirect<CSharpSourceEditorSearchReplace, bool>(nameof(HasFocus), o => o.HasFocus);

        private bool _hasFocus = false;
        public bool HasFocus
        {
            get { return _hasFocus; }
            private set { SetAndRaise(HasFocusProperty, ref _hasFocus, value); }
        }

        public TextBox SearchBox { get => this.FindControl<TextBox>("SearchBox"); }
        public TextBox ReplaceBox { get => this.FindControl<TextBox>("ReplaceBox"); }
        public ToggleButton ReplaceToggle { get => this.FindControl<ToggleButton>("ReplaceToggle"); }

        private CSharpSourceEditor Editor { get; }

        public CSharpSourceEditorSearchReplace(CSharpSourceEditor editor) : this()
        {
            this.Editor = editor;

            this.FindControl<Button>("CloseButton").Click += (s, e) =>
            {
                this.IsVisible = false;
                this.Editor.Focus();
            };

            this.PropertyChanged += (s, e) =>
            {
                if (e.Property == IsVisibleProperty)
                {
                    this.Editor.SelectionLayer.InvalidateVisual();
                }
            };

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Escape)
                {
                    this.IsVisible = false;
                    this.Editor.Focus();
                    e.Handled = true;
                }
                else if (e.Key == Avalonia.Input.Key.Enter)
                {
                    this.Editor.FindNext();
                    e.Handled = true;
                }
            };

            this.FindControl<TextBox>("SearchBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    this.Editor.SearchText = (string)e.NewValue;
                }
            };

            this.FindControl<TextBox>("ReplaceBox").PropertyChanged += (s, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    this.Editor.ReplaceText = (string)e.NewValue;
                }
            };

            this.FindControl<ToggleButton>("CaseSensitiveButton").PropertyChanged += (s, e) =>
            {
                if (e.Property == ToggleButton.IsCheckedProperty)
                {
                    this.Editor.IsSearchCaseSensitive = (bool)e.NewValue;
                }
            };

            this.FindControl<ToggleButton>("RegexButton").PropertyChanged += (s, e) =>
            {
                if (e.Property == ToggleButton.IsCheckedProperty)
                {
                    this.Editor.IsSearchRegex = (bool)e.NewValue;
                }
            };

            this.FindControl<Button>("NextButton").Click += (s, e) =>
            {
                this.Editor.FindNext();
            };

            this.FindControl<Button>("PreviousButton").Click += (s, e) =>
            {
                this.Editor.FindPrevious();
            };

            this.FindControl<Button>("ReplaceNextButton").Click += async (s, e) =>
            {
                await this.Editor.ReplaceNext();
            };


            this.FindControl<Button>("ReplaceAllButton").Click += async (s, e) =>
            {
                await this.Editor.ReplaceAll();
            };
        }

        public CSharpSourceEditorSearchReplace()
        {
            this.InitializeComponent();

            this.FindControl<TextBox>("SearchBox").GotFocus += (s, e) =>
            {
                HasFocus = true;
            };

            this.FindControl<TextBox>("ReplaceBox").GotFocus += (s, e) =>
            {
                HasFocus = true;
            };

            this.FindControl<TextBox>("SearchBox").LostFocus += (s, e) =>
            {
                HasFocus = false;
            };

            this.FindControl<TextBox>("ReplaceBox").LostFocus += (s, e) =>
            {
                HasFocus = false;
            };

            this.PointerPressed += (s, e) =>
            {
                e.Handled = true;
            };

            this.PointerReleased += (s, e) =>
            {
                e.Handled = true;
            };

            this.PointerMoved += (s, e) =>
            {
                e.Handled = true;
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
