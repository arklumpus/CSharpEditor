using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Reflection;

namespace CSharpEditorIPCDemoServer
{
    public partial class MainWindow : Window
    {
        CSharpEditor.Editor Editor;

        public MainWindow()
        {
            InitializeComponent();

            // Initialise the debugger processs. The path passed to the constructor should be the path to the client debugger executable.
            // If the client process dies unexpectedly, the debugger server will respawn it automatically.
            InterprocessDebuggerServer server = new InterprocessDebuggerServer(@"../../../../CSharpEditorIPCDemoClient/bin/Debug/net7.0/CSharpEditorIPCDemoClient.exe");

            this.Opened += async (s, e) =>
            {
                // Initial source code
                string sourceText = "";
                using (Stream stream = this.GetType().Assembly.GetManifestResourceStream("CSharpEditorIPCDemoServer.HelloWorld.cs"))
                using (StreamReader reader = new StreamReader(stream))
                {
                    sourceText = reader.ReadToEnd();
                }

                // Minimal set of references for a console application - double check these with your target framework version - sometimes they change.
                string systemRuntime = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll");
                CSharpEditor.CachedMetadataReference[] minimalReferences = new CSharpEditor.CachedMetadataReference[]
                {
                    CSharpEditor.CachedMetadataReference.CreateFromFile(systemRuntime),                                           // System.Runtime.dll
                    CSharpEditor.CachedMetadataReference.CreateFromFile(typeof(object).Assembly.Location),                        // System.Private.CoreLib.dll
                    CSharpEditor.CachedMetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location)                 // System.Console.dll
                };

                Editor = await CSharpEditor.Editor.Create(sourceText, references: minimalReferences, compilationOptions: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

                Grid.SetRow(Editor, 1);
                this.FindControl<Grid>("MainGrid").Children.Add(Editor);
            };

            this.FindControl<Button>("RunButton").Click += async (s, e) =>
            {
                // We use the SynchronousBreak and AsynchronousBreak from the debugger server, rather than the editor.
                Assembly assembly = (await Editor.Compile(server.SynchronousBreak(Editor), server.AsynchronousBreak(Editor))).Assembly;

                if (assembly != null)
                {
                    // Note how the code is being executed on the UI thread.
                    assembly.EntryPoint.Invoke(null, new object[assembly.EntryPoint.GetParameters().Length]);
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
