using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace CSharpEditor.DiagnosticIcons
{
    internal class KeyIcon : UserControl
    {
        public static readonly StyledProperty<string> KeyTextProperty = AvaloniaProperty.Register<KeyIcon, string>(nameof(KeyText), "Key");

        public string KeyText
        {
            get { return GetValue(KeyTextProperty); }
            set
            {
                string newVal = value;
                if (newVal == "Ctrl" && System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    newVal = "Cmd";
                }

                SetValue(KeyTextProperty, newVal);
            }
        }


        public KeyIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
