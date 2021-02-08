using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Reflection;
using System.Threading;

namespace CSharpEditorDemo
{
    public class MainWindow : Window
    {
        CSharpEditor.Editor Editor;

        public MainWindow()
        {
            InitializeComponent();

            this.Opened += async (s, e) =>
            {
                // Initial source code
                string sourceText = "";
                using (Stream stream = this.GetType().Assembly.GetManifestResourceStream("CSharpEditorDemo.HelloWorld.cs"))
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
                Assembly assembly = (await Editor.Compile(Editor.SynchronousBreak, Editor.AsynchronousBreak)).Assembly;

                if (assembly != null)
                {
                    // Run on a separate thread, in order to enable breakpoints in synchronous functions.
                    // No need to use a separate thread if the entry point is async.
                    new Thread(() =>
                    {
                        assembly.EntryPoint.Invoke(null, new object[assembly.EntryPoint.GetParameters().Length]);
                    }).Start();
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}