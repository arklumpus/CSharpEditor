﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CSharpEditor.DiagnosticIcons
{
    internal class CoreReferenceIcon : UserControl
    {
        public CoreReferenceIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
