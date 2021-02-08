using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace CSharpEditorIPCDemoClient
{
    class Program
    {
        // Used to store the arguments with which the program was called, so that the MainWindow can
        // access them easily.
        public static string[] CallingArguments;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            // Do not run this program directly. Let it be started by the CSharpEditorIPCDemoServer.
            CallingArguments = args;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
