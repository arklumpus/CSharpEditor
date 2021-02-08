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
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CSharpEditor
{
    internal class ReferencesContainer : UserControl
    {
        private ImmutableList<MetadataReference> References;

        public ReferencesContainer()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<Button>("AddReferenceButton").Click += AddReferenceClicked;
            this.FindControl<Button>("DialogOKButton").Click += DialogOKClicked;
        }

        internal void RecreateList(ImmutableList<MetadataReference> references)
        {
            References = references;

            this.FindControl<StackPanel>("ReferencesContainer").Children.Clear();

            ToggleButton coreReferencesButton = this.FindControl<ToggleButton>("CoreReferencesButton");
            ToggleButton additionalReferencesButton = this.FindControl<ToggleButton>("AdditionalReferencesButton");

            foreach (MetadataReference reference in References)
            {
                AddReferenceLine(reference, coreReferencesButton, additionalReferencesButton);
            }
        }

        private void AddReferenceLine(MetadataReference reference, ToggleButton coreReferencesButton, ToggleButton additionalReferencesButton)
        {
            bool isCore = IsCoreReference(reference.Display);

            Grid referenceGrid = new Grid();

            referenceGrid.ColumnDefinitions.Add(new ColumnDefinition(32, GridUnitType.Pixel));
            referenceGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
            referenceGrid.ColumnDefinitions.Add(new ColumnDefinition(32, GridUnitType.Pixel));
            referenceGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
            referenceGrid.ColumnDefinitions.Add(new ColumnDefinition(32, GridUnitType.Pixel));
            referenceGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
            referenceGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            Button removeButton = new Button() { Content = new DiagnosticIcons.MinusIcon(), Width = 20, Height = 20, Margin = new Thickness(6, 0, 6, 0) };
            removeButton.Classes.Add("AddRemove");
            referenceGrid.Children.Add(removeButton);

            Control icon;

            if (isCore)
            {
                icon = new DiagnosticIcons.CoreReferenceIcon();
                referenceGrid.Bind<bool>(Grid.IsVisibleProperty, coreReferencesButton.GetBindingObservable(ToggleButton.IsCheckedProperty).Select(x => x.Value.Value));
            }
            else
            {
                icon = new DiagnosticIcons.AssemblyReferenceIcon();
                referenceGrid.Bind<bool>(Grid.IsVisibleProperty, additionalReferencesButton.GetBindingObservable(ToggleButton.IsCheckedProperty).Select(x => x.Value.Value));
            }

            icon.Margin = new Thickness(6, 0, 6, 0);

            Grid.SetColumn(icon, 2);
            referenceGrid.Children.Add(icon);

            Canvas nameBackground = new Canvas() { Margin = new Thickness(5, 2, 5, 2), Height = 20, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, ClipToBounds = true };

            TextBlock nameBlock = new TextBlock() { Text = reference.Display, ClipToBounds = true };

            nameBackground.Children.Add(nameBlock);
            Grid.SetColumn(nameBackground, 6);
            referenceGrid.Children.Add(nameBackground);

            Canvas documentationIcon = new Canvas() { Width = 16, Height = 16 };

            Button documentationStatus = new Button() { Content = documentationIcon, Width = 20, Height = 20, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            documentationStatus.Classes.Add("AddRemove");

            DocumentationProvider documentationProvider = GetDocumentationProvider(reference);

            string typeName = documentationProvider.GetType().Name;

            if (documentationProvider is XmlDocumentationProvider && typeName != "NullXmlDocumentationProvider")
            {
                documentationIcon.Children.Add(new DiagnosticIcons.TickIcon());
                ToolTip.SetTip(documentationStatus, "XML documentation available");
            }
            else if (isCore)
            {
                documentationIcon.Children.Add(new DiagnosticIcons.BlueTickIcon());
                ToolTip.SetTip(documentationStatus, "Core assembly documentation");
            }
            else
            {
                documentationIcon.Children.Add(new DiagnosticIcons.ErrorIcon());
                ToolTip.SetTip(documentationStatus, "XML documentation not available");
            }

            Grid.SetColumn(documentationStatus, 4);
            referenceGrid.Children.Add(documentationStatus);

            this.FindControl<StackPanel>("ReferencesContainer").Children.Add(referenceGrid);

            referenceGrid.Tag = reference;

            removeButton.Click += async (s, e) =>
            {
                this.FindControl<StackPanel>("ReferencesContainer").Children.Remove(referenceGrid);


                if (!(referenceGrid.Tag is MetadataReference referenceToRemove))
                {
                    referenceToRemove = (MetadataReference)(CachedMetadataReference)referenceGrid.Tag;
                }

                this.References = this.References.Remove(referenceToRemove);

                Editor editor = this.FindAncestorOfType<Editor>();

                await editor.SetReferences(References, false);
            };

            documentationStatus.Click += async (s, e) =>
            {
                await DocumentationButtonClicked(reference, documentationIcon, referenceGrid);
            };
        }

        private static DocumentationProvider GetDocumentationProvider(MetadataReference reference)
        {
            return (DocumentationProvider)reference.GetType().GetProperty("DocumentationProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(reference);
        }

        private bool IsCoreReference(string reference)
        {
            return System.IO.Path.GetFileName(reference) == "System.Private.CoreLib.dll" || Utils.CoreReferences.Contains(System.IO.Path.GetFileName(reference));
        }

        private async Task DocumentationButtonClicked(MetadataReference reference, Canvas documentationIcon, Grid referenceGrid)
        {
            OpenFileDialog dialog;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                dialog = new OpenFileDialog()
                {
                    Title = "Add documentation...",
                    AllowMultiple = false,
                    Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "XML documentation", Extensions = new List<string>() { "xml" } }, new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } }
                };
            }
            else
            {
                dialog = new OpenFileDialog()
                {
                    Title = "Add documentation...",
                    AllowMultiple = false
                };
            }

            string[] result = await dialog.ShowAsync(this.FindAncestorOfType<Window>());

            if (result != null && result.Length == 1)
            {
                try
                {
                    List<string> describedMembers = new List<string>();

                    XDocument doc = XDocument.Load(result[0]);

                    foreach (XElement element in doc.Descendants("member"))
                    {
                        string name = element.Attribute("name").Value;
                        describedMembers.Add(name);
                    }

                    string fullAssemblyPath = GetFullAssemblyPath(reference.Display);
                    int foundTypes = 0;
                    int totalTypes = 0;

                    List<string> paths = Directory.GetFiles(Environment.CurrentDirectory, "*.dll").Concat(Directory.GetFiles(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.dll")).Concat(Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")).ToList();

                    HashSet<string> dllNames = new HashSet<string>();
                    List<string> uniquePaths = new List<string>();

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (dllNames.Add(System.IO.Path.GetFileName(paths[i])))
                        {
                            uniquePaths.Add(paths[i]);
                        }
                    }

                    using (MetadataLoadContext context = new MetadataLoadContext(new PathAssemblyResolver(uniquePaths), typeof(object).Assembly.FullName))
                    {
                        Assembly ass = context.LoadFromAssemblyPath(fullAssemblyPath);
                        Type[] types = ass.GetTypes();

                        foreach (Type type in types)
                        {
                            if (type.IsPublic || type.IsPublic)
                            {
                                totalTypes++;

                                string documentationId = "T:" + type.FullName.Replace("+", ".");

                                if (describedMembers.Contains(documentationId))
                                {
                                    foundTypes++;
                                }
                            }
                        }
                    }

                    await ShowDialog("Documentation analysis", "The documentation file describes " + foundTypes.ToString() + " types out of " + totalTypes + " contained in the assembly.", DialogIcon.Info);

                    CachedMetadataReference newReference = CachedMetadataReference.CreateFromFile(reference.Display, result[0]);

                    References = References.Replace(reference, newReference);

                    referenceGrid.Tag = newReference;

                    Editor editor = this.FindAncestorOfType<Editor>();

                    await editor.SetReferences(References, false);

                    documentationIcon.Children.Clear();
                    documentationIcon.Children.Add(new DiagnosticIcons.TickIcon());
                    ToolTip.SetTip((Control)documentationIcon.Parent, "XML documentation available");
                }
                catch
                {
                    await ShowDialog("Error loading documentation", "An error occurred while loading the documentation!", DialogIcon.Warning);
                }
            }
        }

        private async void AddReferenceClicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                dialog = new OpenFileDialog()
                {
                    Title = "Add reference...",
                    AllowMultiple = false,
                    Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "Component files", Extensions = new List<string>() { "exe", "dll", "tlb", "olb", "ocx", "winmd" } }, new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } }
                };
            }
            else
            {
                dialog = new OpenFileDialog()
                {
                    Title = "Add reference...",
                    AllowMultiple = false
                };
            }

            string[] result = await dialog.ShowAsync(this.FindAncestorOfType<Window>());

            if (result != null && result.Length == 1)
            {
                string relativeToWorkingDir = System.IO.Path.GetRelativePath(Environment.CurrentDirectory, result[0]);
                string relativeToExecutable = System.IO.Path.GetRelativePath(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), result[0]);

                string path = result[0];

                if (relativeToWorkingDir.Length < path.Length)
                {
                    path = relativeToWorkingDir;
                }

                if (relativeToExecutable.Length < path.Length)
                {
                    path = relativeToExecutable;
                }

                try
                {
                    List<string> paths = Directory.GetFiles(Environment.CurrentDirectory, "*.dll").Concat(Directory.GetFiles(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.dll")).Concat(Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")).ToList();

                    HashSet<string> dllNames = new HashSet<string>();
                    List<string> uniquePaths = new List<string>();

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (dllNames.Add(System.IO.Path.GetFileName(paths[i])))
                        {
                            uniquePaths.Add(paths[i]);
                        }
                    }

                    using (MetadataLoadContext context = new MetadataLoadContext(new PathAssemblyResolver(uniquePaths), typeof(object).Assembly.FullName))
                    {
                        Assembly ass = context.LoadFromAssemblyPath(result[0]);
                    }

                    CachedMetadataReference reference = CachedMetadataReference.CreateFromFile(path);

                    AddReferenceLine(reference, this.FindControl<ToggleButton>("CoreReferencesButton"), this.FindControl<ToggleButton>("AdditionalReferencesButton"));

                    References = References.Add(reference);

                    Editor editor = this.FindAncestorOfType<Editor>();


                    await editor.SetReferences(References, false);
                }
                catch (Exception ex)
                {
                    await ShowDialog("Error loading assembly", "An error occurred while loading the assembly!\n" + ex.Message, DialogIcon.Warning);
                }
            }
        }

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0, 1);

        private enum DialogIcon { Warning, Info }

        private async Task ShowDialog(string title, string text, DialogIcon icon)
        {
            switch (icon)
            {
                case DialogIcon.Warning:
                    this.FindControl<Viewbox>("DialogWarningIcon").IsVisible = true;
                    this.FindControl<Viewbox>("DialogInfoIcon").IsVisible = false;
                    break;

                case DialogIcon.Info:
                    this.FindControl<Viewbox>("DialogWarningIcon").IsVisible = false;
                    this.FindControl<Viewbox>("DialogInfoIcon").IsVisible = true;
                    break;
            }

            this.FindControl<TextBlock>("DialogMessageTitle").Text = title;
            this.FindControl<TextBlock>("DialogMessageText").Text = text;

            this.FindControl<Grid>("DialogGrid").IsVisible = true;

            await semaphore.WaitAsync();

            this.FindControl<Grid>("DialogGrid").IsVisible = false;
        }

        private void DialogOKClicked(object sender, RoutedEventArgs e)
        {
            semaphore.Release();
        }

        private string GetFullAssemblyPath(string relativePath)
        {
            if (File.Exists(relativePath))
            {
                return System.IO.Path.GetFullPath(relativePath);
            }
            else if (File.Exists(System.IO.Path.Combine(Environment.CurrentDirectory, System.IO.Path.GetFileName(relativePath))))
            {
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(Environment.CurrentDirectory, System.IO.Path.GetFileName(relativePath)));
            }
            else if (File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), System.IO.Path.GetFileName(relativePath))))
            {
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), System.IO.Path.GetFileName(relativePath)));
            }
            else
            {
                throw new FileNotFoundException();
            }
        }
    }
}
