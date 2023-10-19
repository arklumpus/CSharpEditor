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

using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace CSharpEditor
{
    /// <summary>
    /// A class used to analyse breakpoints on a separate process (to avoid deadlocks with breakpoints in synchronous code).
    /// </summary>
    public class InterprocessDebuggerServer : IDisposable
    {
        private Process ClientProcess;
        private string ClientExePath;
        private List<string> InitialArguments;
        NamedPipeServerStream PipeServerOut;
        NamedPipeServerStream PipeServerIn;
        StreamWriter PipeServerOutWriter;
        StreamReader PipeServerInReader;
        private Func<int, int> GetClientPid;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new <see cref="InterprocessDebuggerServer"/>, starting the client process and establishing pipes to communicate with it.
        /// </summary>
        /// <param name="clientExePath">The path to the executable of the client process.</param>
        public InterprocessDebuggerServer(string clientExePath)
        {
            this.InitializeServer(clientExePath, new List<string>(), null);
        }

        /// <summary>
        /// Initializes a new <see cref="InterprocessDebuggerServer"/>, starting the client process and establishing pipes to communicate with it.
        /// </summary>
        /// <param name="clientExePath">The path to the executable of the client process.</param>
        /// <param name="initialArguments">The arguments that will be used to start the client process. The additional arguments specific to the <see cref="InterprocessDebuggerServer"/> will be appended after these.</param>
        public InterprocessDebuggerServer(string clientExePath, IEnumerable<string> initialArguments)
        {
            this.InitializeServer(clientExePath, initialArguments.ToList(), null);
        }

        /// <summary>
        /// Initializes a new <see cref="InterprocessDebuggerServer"/>, starting the client process and establishing pipes to communicate with it.
        /// </summary>
        /// <param name="clientExePath">The path to the executable of the client process.</param>
        /// <param name="initialArguments">The arguments that will be used to start the client process. The additional arguments specific to the <see cref="InterprocessDebuggerServer"/> will be appended after these.</param>
        /// <param name="getClientPid">A method that returns the process identifier (PID) of the client debugger process. The argument of this method is the PID of the process that has been started by the server. If this is <see langword="null"/>, it is assumed that the process started by the server is the client debugger process.</param>
        public InterprocessDebuggerServer(string clientExePath, IEnumerable<string> initialArguments, Func<int, int> getClientPid)
        {
            this.InitializeServer(clientExePath, initialArguments.ToList(), getClientPid);
        }

        private void InitializeServer(string clientExePath, List<string> initialArguments, Func<int, int> getClientPid)
        {
            ClientExePath = clientExePath;

            ClientProcess = new Process();
            ClientProcess.StartInfo.FileName = clientExePath;

            InitialArguments = initialArguments;

            string pipeNameOut = Guid.NewGuid().ToString("N");
            string pipeNameIn = Guid.NewGuid().ToString("N");

            PipeServerOut?.Dispose();
            PipeServerIn?.Dispose();

            PipeServerOut = new NamedPipeServerStream(pipeNameOut, PipeDirection.Out);
            PipeServerIn = new NamedPipeServerStream(pipeNameIn, PipeDirection.In);

            PipeServerOutWriter = new StreamWriter(PipeServerOut);
            PipeServerInReader = new StreamReader(PipeServerIn);

            /*foreach (string arg in initialArguments)
            {
                ClientProcess.StartInfo.ArgumentList.Add(arg);
            }

            ClientProcess.StartInfo.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
            ClientProcess.StartInfo.ArgumentList.Add(pipeNameOut);
            ClientProcess.StartInfo.ArgumentList.Add(pipeNameIn);*/

            System.Text.StringBuilder argumentBuilder = new System.Text.StringBuilder();

            foreach (string arg in initialArguments)
            {
                argumentBuilder.Append(arg);
                argumentBuilder.Append(" ");
            }

            argumentBuilder.Append(Process.GetCurrentProcess().Id.ToString());
            argumentBuilder.Append(" ");
            argumentBuilder.Append(pipeNameOut);
            argumentBuilder.Append(" ");
            argumentBuilder.Append(pipeNameIn);

            ClientProcess.StartInfo.Arguments = argumentBuilder.ToString();

            ClientProcess.StartInfo.UseShellExecute = false;
            ClientProcess.Start();

            PipeServerOut.WaitForConnection();
            PipeServerIn.WaitForConnection();

            this.GetClientPid = getClientPid;

            if (GetClientPid != null)
            {
                int newPid = GetClientPid(ClientProcess.Id);

                ClientProcess = Process.GetProcessById(newPid);
            }

            string guid = System.Guid.NewGuid().ToString("N");
            PipeServerOutWriter.WriteLine(guid);
            PipeServerOutWriter.Flush();            

            string input = PipeServerInReader.ReadLine();

            if (input != guid)
            {
                throw new ApplicationException("The client debugger process answered incorrectly!\n" + input);
            }
        }

        /// <summary>
        /// Returns a function to handle breakpoints in synchronous methods by transferring the breakpoint information to the client process. Pass the output of this method as an argument to <see cref="Editor.Compile(Func{BreakpointInfo, bool}, Func{BreakpointInfo, Task{bool}})"/>. The function will lock until the client process signals that execution can resume.
        /// </summary>
        /// <param name="editor">The <see cref="Editor"/> whose code will be debugged. Note that no reference to this object is kept after this method returns.</param>
        /// <returns>A function to handle breakpoints in synchronous methods by transferring the breakpoint information to the client process. If the client process is not executing when a breakpoint occurs, it is started again.</returns>
        public Func<BreakpointInfo, bool> SynchronousBreak(Editor editor)
        {
            IEnumerable<MetadataReference> editorReferences = editor.References;
            string text = editor.Text;
            string preSource = editor.PreSource;
            string postSource = editor.PostSource;

            return (info) =>
            {
                if (ClientProcess.HasExited)
                {
                    InitializeServer(ClientExePath, InitialArguments, GetClientPid);
                }

                PipeServerOutWriter.WriteLine("Init");

                Dictionary<string, object> objectCache = new Dictionary<string, object>();
                Dictionary<string, (string, VariableTypes, string)> localVariables = new Dictionary<string, (string, VariableTypes, string)>();
                Dictionary<string, TaggedText[]> localVariablesDisplayParts = new Dictionary<string, TaggedText[]>();

                foreach (KeyValuePair<string, object> variable in info.LocalVariables)
                {
                    string guid = System.Guid.NewGuid().ToString("N");
                    objectCache.Add(guid, variable.Value);
                    localVariablesDisplayParts.Add(variable.Key, info.LocalVariableDisplayParts[variable.Key]);

                    (VariableTypes variableType, string valueJSON) = GetVariableTypeAndJSONValue(variable.Value);

                    localVariables.Add(variable.Key, (guid, variableType, valueJSON));
                }

                string localVariablesDisplayPartsJson = JsonSerializer.Serialize((from el in localVariablesDisplayParts select new string[] { el.Key, JsonSerializer.Serialize((from el2 in el.Value select (ReadWriteTaggedText)el2).ToArray()) }).ToArray());
                string localVariablesJson = JsonSerializer.Serialize((from el in localVariables select new string[] { el.Key, el.Value.Item1, JsonSerializer.Serialize(el.Value.Item2), el.Value.Item3 }).ToArray());
                List<string> references = new List<string>();
                foreach (MetadataReference reference in editorReferences)
                {
                    if (reference is PortableExecutableReference port)
                    {
                        references.Add(port.FilePath);
                    }
                }

                string message = JsonSerializer.Serialize(new string[] { localVariablesDisplayPartsJson, localVariablesJson, text, info.BreakpointSpan.Start.ToString(), preSource, postSource, JsonSerializer.Serialize(references) });

                PipeServerOutWriter.WriteLine(message);
                PipeServerOutWriter.Flush();

                while (!ClientProcess.HasExited)
                {
                    string line = PipeServerInReader.ReadLine();

                    if (!ClientProcess.HasExited && !string.IsNullOrEmpty(line))
                    {
                        string[] inputMessage = JsonSerializer.Deserialize<string[]>(line);

                        if (inputMessage[0] == "GetItems")
                        {
                            IEnumerable enumerable = (IEnumerable)objectCache[inputMessage[1]];

                            List<string[]> items = new List<string[]>();

                            foreach (object obj in enumerable)
                            {
                                string guid = System.Guid.NewGuid().ToString("N");
                                objectCache.Add(guid, obj);

                                (VariableTypes variableType, string valueJSON) = GetVariableTypeAndJSONValue(obj);

                                items.Add(new string[] { guid, JsonSerializer.Serialize(variableType), valueJSON });
                            }


                            PipeServerOutWriter.WriteLine(JsonSerializer.Serialize(items));
                            PipeServerOutWriter.Flush();
                        }
                        else if (inputMessage[0] == "GetProperty")
                        {
                            bool isProperty = Convert.ToBoolean(inputMessage[3]);

                            object owner = objectCache[inputMessage[1]];

                            object obj;

                            try
                            {
                                if (isProperty)
                                {
                                    obj = owner.GetType().InvokeMember(inputMessage[2], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetProperty, null, owner, null);
                                }
                                else
                                {
                                    obj = owner.GetType().InvokeMember(inputMessage[2], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField, null, owner, null);
                                }
                            }
                            catch (Exception ex)
                            {
                                obj = "An error occurred while accessing this member:\n" + ex.Message;
                            }


                            string guid = System.Guid.NewGuid().ToString("N");
                            objectCache.Add(guid, obj);

                            (VariableTypes variableType, string valueJSON) = GetVariableTypeAndJSONValue(obj);

                            PipeServerOutWriter.WriteLine(JsonSerializer.Serialize(new string[] { guid, JsonSerializer.Serialize(variableType), valueJSON }));
                            PipeServerOutWriter.Flush();
                        }
                        else if (inputMessage[0] == "Resume")
                        {
                            return Convert.ToBoolean(inputMessage[1]);
                        }
                    }
                }

                return false;
            };
        }

        /// <summary>
        /// Returns a function to handle breakpoints in asynchronous methods by transferring the breakpoint information to the client process. Pass the output of this method as an argument to <see cref="Editor.Compile(Func{BreakpointInfo, bool}, Func{BreakpointInfo, Task{bool}})"/>. This function will actually execute synchronously and lock until the client process signals that execution can resume.
        /// </summary>
        /// <param name="editor">The <see cref="Editor"/> whose code will be debugged. Note that no reference to this object is kept after this method returns.</param>
        /// <returns>A function to handle breakpoints in asynchronous methods by transferring the breakpoint information to the client process. If the client process is not executing when a breakpoint occurs, it is started again.</returns>
        public Func<BreakpointInfo, Task<bool>> AsynchronousBreak(Editor editor)
        {
            Func<BreakpointInfo, bool> syncBreak = SynchronousBreak(editor);

            return (info) =>
            {
                bool tbr = syncBreak(info);
                return Task<bool>.FromResult(tbr);
            };
        }

        private static (VariableTypes variableType, string valueJSON) GetVariableTypeAndJSONValue(object variableValue)
        {
            VariableTypes variableType = VariableTypes.Other;
            string valueJSON = null;

            if (variableValue is string str)
            {
                variableType = VariableTypes.String;
                valueJSON = JsonSerializer.Serialize(str);
            }
            else if (variableValue is char chr)
            {
                variableType = VariableTypes.Char;
                valueJSON = JsonSerializer.Serialize(chr.ToString());
            }
            else if (variableValue is long || variableValue is int || variableValue is double || variableValue is decimal || variableValue is ulong || variableValue is uint || variableValue is short || variableValue is ushort || variableValue is byte || variableValue is sbyte || variableValue is float)
            {
                variableType = VariableTypes.Number;
                valueJSON = JsonSerializer.Serialize(Convert.ToString(variableValue, System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (variableValue is bool bol)
            {
                variableType = VariableTypes.Bool;
                valueJSON = JsonSerializer.Serialize(bol.ToString().ToLower());
            }
            else if (variableValue is null)
            {
                variableType = VariableTypes.Null;
                valueJSON = JsonSerializer.Serialize("");
            }
            else if (variableValue is IEnumerable)
            {
                string enumerableType = "";

                int count = -1;

                foreach (Type interf in ((Type)variableValue.GetType()).GetInterfaces())
                {
                    if (interf.IsGenericType && interf.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        foreach (Type tp in interf.GetGenericArguments())
                        {
                            enumerableType += tp.Name + ", ";
                        }
                    }
                }

                if (enumerableType.Length > 2)
                {
                    enumerableType = enumerableType.Substring(0, enumerableType.Length - 2);
                }

                if (variableValue is ICollection collection)
                {
                    count = collection.Count;
                }

                variableType = VariableTypes.IEnumerable;
                valueJSON = JsonSerializer.Serialize(new string[] { enumerableType, count.ToString() });
            }
            else if (variableValue is Enum)
            {
                variableType = VariableTypes.Enum;
                valueJSON = JsonSerializer.Serialize(new string[] { variableValue.GetType().Name, variableValue.ToString() });
            }
            else
            {
                if (variableValue.GetType().IsClass)
                {
                    if (variableValue is Delegate)
                    {
                        variableType = VariableTypes.Delegate;
                    }
                    else
                    {
                        variableType = VariableTypes.Class;
                    }
                }
                else if (variableValue.GetType().IsInterface)
                {
                    variableType = VariableTypes.Interface;
                }
                else
                {
                    variableType = VariableTypes.Other;
                }

                List<System.Reflection.MemberInfo> propertyList = new List<System.Reflection.MemberInfo>();

                foreach (System.Reflection.MemberInfo prop in ((Type)variableValue.GetType()).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (prop.MemberType == System.Reflection.MemberTypes.Property || prop.MemberType == System.Reflection.MemberTypes.Field)
                    {
                        propertyList.Add(prop);
                    }
                }

                propertyList = new List<MemberInfo>(from el in propertyList where (el.MemberType == MemberTypes.Field && !((FieldInfo)el).IsStatic) || (el.MemberType == MemberTypes.Property && !((((PropertyInfo)el).GetMethod != null && ((PropertyInfo)el).GetMethod.IsStatic) || (((PropertyInfo)el).SetMethod != null && ((PropertyInfo)el).SetMethod.IsStatic))) orderby el.Name ascending select el);

                List<string[]> properties = new List<string[]>();

                if (propertyList.Count > 0)
                {
                    foreach (MemberInfo property in propertyList)
                    {
                        if (property.MemberType == MemberTypes.Field)
                        {
                            try
                            {
                                properties.Add(new string[] { property.Name, bool.FalseString, (!((FieldInfo)property).IsPublic).ToString() });
                            }
                            catch { }
                        }
                        else if (property.MemberType == MemberTypes.Property)
                        {
                            try
                            {
                                properties.Add(new string[] { property.Name, bool.TrueString, (!(((PropertyInfo)property).GetMethod?.IsPublic == true || ((PropertyInfo)property).SetMethod?.IsPublic == true)).ToString() });
                            }
                            catch { }
                        }
                    }
                }

                valueJSON = JsonSerializer.Serialize(properties);
            }

            return (variableType, valueJSON);
        }

        /// <summary>
        /// Kills the debugger client process and frees the pipe resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PipeServerOutWriter.WriteLine("Abort");
                    PipeServerOutWriter.Flush();
                    PipeServerOutWriter.Dispose();
                    PipeServerInReader.Dispose();
                    PipeServerOut.Dispose();
                    PipeServerIn.Dispose();
                }

                ClientProcess.Kill();
                disposedValue = true;
            }
        }

        /// <summary>
        /// Destructor for the debugger server, which also kills the client process.
        /// </summary>
        ~InterprocessDebuggerServer()
        {
            Dispose(disposing: false);
        }

        /// <summary>
        /// Kills the debugger client process and frees the pipe resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }


    /// <summary>
    /// A control that shows breakpoint information for breakpoints reached on a server process. This control contains a read-only <see cref="CSharpEditor.Editor"/> to display the code, which is reused as much as possible to reduce the initialization time.
    /// </summary>
    public class InterprocessDebuggerClient : UserControl, IDisposable
    {
        readonly NamedPipeClientStream PipeClientIn;
        readonly NamedPipeClientStream PipeClientOut;
        readonly StreamReader PipeClientInReader;
        readonly StreamWriter PipeClientOutWriter;
        readonly Process ParentProcess;

        private bool ParentProcessExitedRaised = false;

        /// <summary>
        /// Invoked when the server process that started this client has been closed or has signaled that all client activity should cease.
        /// </summary>
        public event EventHandler<EventArgs> ParentProcessExited;

        /// <summary>
        /// Invoked when the server process signals that a breakpoint has been reached.
        /// </summary>
        public event EventHandler<EventArgs> BreakpointHit;

        /// <summary>
        /// Invoked when the user signals that code execution can resume.
        /// </summary>
        public event EventHandler<EventArgs> BreakpointResumed;

        private Editor Editor;
        private bool disposedValue;

        /// <summary>
        /// Creates a new <see cref="InterprocessDebuggerClient"/>, using the information provided by the <see cref="InterprocessDebuggerServer"/> to open the pipes to communicate with it.
        /// </summary>
        /// <param name="args">The arguments with which the <see cref="InterprocessDebuggerServer"/> started the client process.</param>
        public InterprocessDebuggerClient(string[] args)
        {
            ParentProcess = Process.GetProcessById(int.Parse(args[0]));
            if (ParentProcess != null)
            {
                ParentProcess.EnableRaisingEvents = true;

                ParentProcess.Exited += (s, e) =>
                {
                    if (!ParentProcessExitedRaised)
                    {
                        ParentProcessExitedRaised = true;
                        ParentProcessExited?.Invoke(this, new EventArgs());
                    }
                };
            }

            PipeClientIn = new NamedPipeClientStream(".", args[1], PipeDirection.In);
            
            PipeClientOut = new NamedPipeClientStream(".", args[2], PipeDirection.Out);

            PipeClientIn.Connect();
            PipeClientOut.Connect();

            PipeClientOutWriter = new StreamWriter(PipeClientOut);
            PipeClientInReader = new StreamReader(PipeClientIn);
            
            
            string message = PipeClientInReader.ReadLine();

            PipeClientOutWriter.WriteLine(message);
            PipeClientOutWriter.Flush();
        }

        /// <summary>
        /// Start the loop that waits for breakpoint signals from the server.
        /// </summary>
        /// <param name="e">The event args.</param>
        protected override async void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);

            if (Editor == null)
            {
                Editor = await Editor.Create();
                Editor.AccessType = Editor.AccessTypes.ReadOnly;
                this.Content = Editor;
            }

            System.Threading.Thread thr = new System.Threading.Thread(async () =>
            {
                while (!ParentProcess.HasExited)
                {
                    string message = await PipeClientInReader.ReadLineAsync();

                    if (message == "Abort")
                    {
                        if (!ParentProcessExitedRaised)
                        {
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                ParentProcessExitedRaised = true;
                                ParentProcessExited?.Invoke(this, new EventArgs());
                            });
                        }
                        break;
                    }

                    if (!ParentProcess.HasExited && message == "Init")
                    {
                        message = await PipeClientInReader.ReadLineAsync();

                        if (message == "Abort")
                        {
                            if (!ParentProcessExitedRaised)
                            {
                                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    ParentProcessExitedRaised = true;
                                    ParentProcessExited?.Invoke(this, new EventArgs());
                                });
                            }
                            break;
                        }

                        if (!ParentProcess.HasExited && !string.IsNullOrEmpty(message))
                        {
                            string[] messageParts = JsonSerializer.Deserialize<string[]>(message);

                            string[][] localVariablesDisplayPartsJson = JsonSerializer.Deserialize<string[][]>(messageParts[0]);
                            string[][] localVariablesJson = JsonSerializer.Deserialize<string[][]>(messageParts[1]);
                            string sourceCode = messageParts[2];
                            int breakpointStart = int.Parse(messageParts[3]);
                            string preSource = messageParts[4];
                            string postSource = messageParts[5];

                            IEnumerable<CachedMetadataReference> references = from el in JsonSerializer.Deserialize<string[]>(messageParts[6]) select CachedMetadataReference.CreateFromFile(el);

                            Dictionary<string, TaggedText[]> localVariablesDisplayParts = new Dictionary<string, TaggedText[]>();

                            foreach (string[] item in localVariablesDisplayPartsJson)
                            {
                                localVariablesDisplayParts.Add(item[0], (from el in JsonSerializer.Deserialize<ReadWriteTaggedText[]>(item[1]) select (TaggedText)el).ToArray());
                            }

                            Dictionary<string, (string, VariableTypes, object)> localVariables = new Dictionary<string, (string, VariableTypes, object)>();

                            foreach (string[] item in localVariablesJson)
                            {
                                VariableTypes variableType = JsonSerializer.Deserialize<VariableTypes>(item[2]);

                                object variableValue = ParseVariableValue(variableType, item[3]);

                                localVariables.Add(item[0], (item[1], variableType, variableValue));
                            }

                            (string propertyId, VariableTypes propertyType, object propertyValue) propertyOrFieldGetter(string variableId, string propertyName, bool isProperty)
                            {
                                PipeClientOutWriter.WriteLine(JsonSerializer.Serialize(new string[] { "GetProperty", variableId, propertyName, isProperty.ToString() }));
                                PipeClientOutWriter.Flush();

                                string message2 = PipeClientInReader.ReadLine();

                                if (message2 == "Abort")
                                {
                                    if (!ParentProcessExitedRaised)
                                    {
                                        ParentProcessExitedRaised = true;
                                        ParentProcessExited?.Invoke(this, new EventArgs());
                                    }
                                    return ("", VariableTypes.Null, "");
                                }

                                string[] output = JsonSerializer.Deserialize<string[]>(message2);

                                VariableTypes variableType = JsonSerializer.Deserialize<VariableTypes>(output[1]);

                                return (output[0], variableType, ParseVariableValue(variableType, output[2]));
                            }

                            (string itemId, VariableTypes itemType, object itemValue)[] itemsGetter(string variableId)
                            {
                                PipeClientOutWriter.WriteLine(JsonSerializer.Serialize(new string[] { "GetItems", variableId }));
                                PipeClientOutWriter.Flush();

                                string message2 = PipeClientInReader.ReadLine();

                                if (message2 == "Abort")
                                {
                                    if (!ParentProcessExitedRaised)
                                    {
                                        ParentProcessExitedRaised = true;
                                        ParentProcessExited?.Invoke(this, new EventArgs());
                                    }
                                    return new (string, VariableTypes, object)[] { ("", VariableTypes.Null, "") };
                                }

                                string[][] output = JsonSerializer.Deserialize<string[][]>(message2);

                                return (from el in output let variableType = JsonSerializer.Deserialize<VariableTypes>(el[1]) select (el[0], variableType, ParseVariableValue(variableType, el[2]))).ToArray();
                            }

                            RemoteBreakpointInfo info = new RemoteBreakpointInfo(breakpointStart, localVariables, localVariablesDisplayParts, propertyOrFieldGetter, itemsGetter);

                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                if (Editor == null || Editor.PreSource != preSource || Editor.PostSource != postSource)
                                {
                                    Editor = await Editor.Create(sourceCode, preSource, postSource, references);
                                    Editor.AccessType = Editor.AccessTypes.ReadOnly;
                                    this.Content = Editor;
                                }
                                else
                                {
                                    await Editor.SetText(sourceCode);
                                    await Editor.SetReferences(ImmutableList.Create((from el in references select (MetadataReference)el).ToArray()));
                                }

                                BreakpointHit?.Invoke(this, new EventArgs());
                            });
                            
                            bool shouldSuppress = await Editor.AsynchronousBreak(info);

                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                PipeClientOutWriter.WriteLine(JsonSerializer.Serialize(new string[] { "Resume", shouldSuppress.ToString() }));
                                PipeClientOutWriter.Flush();

                                BreakpointResumed?.Invoke(this, new EventArgs());
                            });
                        }
                    }
                }
            });

            thr.Start();
        }

        private object ParseVariableValue(VariableTypes variableType, string item)
        {
            object variableValue = null;

            switch (variableType)
            {
                case VariableTypes.String:
                case VariableTypes.Char:
                case VariableTypes.Number:
                case VariableTypes.Bool:
                case VariableTypes.Null:
                    variableValue = JsonSerializer.Deserialize<string>(item);
                    break;

                case VariableTypes.IEnumerable:
                    {
                        string[] value = JsonSerializer.Deserialize<string[]>(item);
                        variableValue = (value[0], int.Parse(value[1]));
                    }
                    break;

                case VariableTypes.Enum:
                    {
                        string[] value = JsonSerializer.Deserialize<string[]>(item);
                        variableValue = (value[0], value[1]);
                    }
                    break;

                case VariableTypes.Class:
                case VariableTypes.Delegate:
                case VariableTypes.Interface:
                case VariableTypes.Other:
                    {
                        string[][] value = JsonSerializer.Deserialize<string[][]>(item);
                        variableValue = (from el in value select (el[0], Convert.ToBoolean(el[1]), Convert.ToBoolean(el[2]))).ToArray();
                    }
                    break;
            }

            return variableValue;
        }

        /// <summary>
        /// Closes the pipes used by this instance.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PipeClientInReader.Dispose();
                    PipeClientOutWriter.Dispose();
                    PipeClientIn.Dispose();
                    PipeClientOut.Dispose();
                }

                disposedValue = true;
            }
        }


        /// <summary>
        /// Closes the pipes uses by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
