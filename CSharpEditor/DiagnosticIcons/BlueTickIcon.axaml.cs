﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CSharpEditor.DiagnosticIcons
{
    internal partial class BlueTickIcon : UserControl
    {
        public BlueTickIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
