using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpEditor;

namespace CSharpEditorIPCDemoClient
{
    public partial class MainWindow : Window
    {
        // Do not run this program directly. Let it be started by the CSharpEditorIPCDemoServer.

        // This will be set to true when the parent process exits, to signal that this process also needs to die.
        bool terminating = false;
        public MainWindow()
        {
            InitializeComponent();

            // Set up the client debugger, passing the arguments that were used to start the program.
            InterprocessDebuggerClient client = new InterprocessDebuggerClient(Program.CallingArguments);
            this.Content = client;

            // This event is called when a breakpoint in the code is hit. We show this window, so that users can
            // interact with it.
            client.BreakpointHit += (s, e) =>
            {
                this.Show();
                this.Activate();
            };

            // This event is invoked when the user has clicked on the button to resume code execution after the
            // breakpoint.
            client.BreakpointResumed += (s, e) =>
            {
                this.Hide();
            };

            // This event is called when the parent process that started this program exits.
            client.ParentProcessExited += (s, e) =>
            {
                // Signals that the window needs to be closed.
                terminating = true;
                this.Close();
            };

            // Prevent this window from actually closing, unless the parent process has already exited.
            this.Closing += (s, e) =>
            {
                if (!terminating)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }

        // This will be set to true after the first time this window is shown.
        bool initialized = false;
        public override void Show()
        {
            if (!initialized)
            {
                initialized = true;

                // Hide the window just after it is shown. We can't just not show the window, otherwise the first time it is shown it will be below the window of the server process.
                // Still, this is not 100% fail-proof, as sometimes the Activate method does not work properly.
                base.Show();
                this.Hide();
            }
            else
            {
                base.Show();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
