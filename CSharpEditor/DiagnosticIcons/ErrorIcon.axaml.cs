﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CSharpEditor.DiagnosticIcons
{
    internal class ErrorIcon : UserControl
    {
        public ErrorIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
