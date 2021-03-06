﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CSharpEditor.DiagnosticIcons
{
    internal class AssemblyReferenceIcon : UserControl
    {
        public AssemblyReferenceIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
