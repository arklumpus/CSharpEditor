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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CSharpEditor
{
    /// <summary>
    /// A class to hold information about breakpoints.
    /// </summary>
    public class BreakpointInfo
    {
        /// <summary>
        /// The location in the source code of the breakpoint, including any prepended or appended source code.
        /// </summary>
        public TextSpan BreakpointSpan { get; }

        /// <summary>
        /// A dictionary containing the names and values of the local variables in scope at the breakpoint.
        /// </summary>
        public Dictionary<string, object> LocalVariables { get; }

        internal Dictionary<string, TaggedText[]> LocalVariableDisplayParts { get; }

        internal BreakpointInfo(int breakpointStart, string[] localVariableNames, string[] localVariablesDisplayJson, object[] localVariableValues)
        {
            this.BreakpointSpan = new TextSpan(breakpointStart, Utils.BreakpointMarker.Length);
            this.LocalVariables = new Dictionary<string, object>();
            this.LocalVariableDisplayParts = new Dictionary<string, TaggedText[]>();

            for (int i = 0; i < localVariableNames.Length; i++)
            {
                this.LocalVariables.Add(localVariableNames[i], localVariableValues[i]);

                TaggedText[] displayParts = (from el in JsonSerializer.Deserialize<ReadWriteTaggedText[]>(localVariablesDisplayJson[i]) select (TaggedText)el).ToArray();

                this.LocalVariableDisplayParts.Add(localVariableNames[i], displayParts);
            }
        }

        internal static Action<int, string[], string[], object[]> GetBreakpointFunction(Func<BreakpointInfo, bool> callback)
        {
            Dictionary<int, bool> suppressedBreakPoints = new Dictionary<int, bool>();

            return (spanStart, variableNames, variableDisplay, variableValues) =>
            {
                bool suppressed = suppressedBreakPoints.TryGetValue(spanStart, out bool suppressedInDictionary) && suppressedInDictionary;

                if (!suppressed)
                {
                    BreakpointInfo info = new BreakpointInfo(spanStart, variableNames, variableDisplay, variableValues);
                    suppressedBreakPoints[spanStart] = callback?.Invoke(info) == true;
                }
            };
        }

        internal static Func<int, string[], string[], object[], Task> GetBreakpointAsyncFunction(Func<BreakpointInfo, Task<bool>> callback)
        {
            Dictionary<int, bool> suppressedBreakPoints = new Dictionary<int, bool>();

            return async (spanStart, variableNames, variableDisplay, variableValues) =>
            {
                bool suppressed = suppressedBreakPoints.TryGetValue(spanStart, out bool suppressedInDictionary) && suppressedInDictionary;

                if (!suppressed)
                {
                    BreakpointInfo info = new BreakpointInfo(spanStart, variableNames, variableDisplay, variableValues);
                    suppressedBreakPoints[spanStart] = (await callback?.Invoke(info)) == true;
                }
            };
        }

        internal static string GetBreakpointSource(TextSpan breakpointSpan, ILocalSymbol[] localVariables, SemanticModel model, string debuggerGuid, bool async)
        {
            List<string> displayJson = new List<string>();

            for (int i = 0; i < localVariables.Length; i++)
            {
                ImmutableArray<SymbolDisplayPart> displayParts = localVariables[i].ToMinimalDisplayParts(model, breakpointSpan.Start, new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance | SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions: SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeExplicitInterface,
                delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
                extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeOptionalBrackets,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                localOptions: SymbolDisplayLocalOptions.IncludeType | SymbolDisplayLocalOptions.IncludeConstantValue,
                kindOptions: SymbolDisplayKindOptions.IncludeNamespaceKeyword | SymbolDisplayKindOptions.IncludeTypeKeyword | SymbolDisplayKindOptions.IncludeMemberKeyword,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            ));

                List<TaggedText> taggedText = new List<TaggedText>();

                foreach (SymbolDisplayPart part in displayParts)
                {
                    taggedText.Add(part.ToTaggedText());
                }

                displayJson.Add(System.Text.Json.JsonSerializer.Serialize(System.Text.Json.JsonSerializer.Serialize(taggedText)));
            }


            string guid = "_" + Guid.NewGuid().ToString("N");

            StringBuilder sourceBuilder = new StringBuilder();

            sourceBuilder.AppendLine("{");
            sourceBuilder.Append("int start");
            sourceBuilder.Append(guid);
            sourceBuilder.Append(" = ");
            sourceBuilder.Append(breakpointSpan.Start.ToString());
            sourceBuilder.AppendLine(";");
            sourceBuilder.Append("string[] localVariableNames");
            sourceBuilder.Append(guid);
            sourceBuilder.Append(" = new string[] { ");

            for (int i = 0; i < localVariables.Length; i++)
            {
                sourceBuilder.Append("\"");
                sourceBuilder.Append(localVariables[i].Name);
                sourceBuilder.Append("\", ");
            }

            sourceBuilder.AppendLine(" };");

            sourceBuilder.Append("string[] localVariableDisplay");
            sourceBuilder.Append(guid);
            sourceBuilder.Append(" = new string[] { ");

            for (int i = 0; i < localVariables.Length; i++)
            {
                sourceBuilder.Append(displayJson[i]);
                sourceBuilder.Append(", ");
            }

            sourceBuilder.AppendLine(" };");

            sourceBuilder.Append("object[] localVariableValues");
            sourceBuilder.Append(guid);
            sourceBuilder.Append(" = new object[] { ");
            for (int i = 0; i < localVariables.Length; i++)
            {
                sourceBuilder.Append(localVariables[i].Name);
                sourceBuilder.Append(", ");
            }
            sourceBuilder.AppendLine(" };");

            if (!async)
            {
                sourceBuilder.Append(debuggerGuid);
                sourceBuilder.Append(".Debugger.Breakpoint(start");
                sourceBuilder.Append(guid);
                sourceBuilder.Append(", localVariableNames");
                sourceBuilder.Append(guid);
                sourceBuilder.Append(", localVariableDisplay");
                sourceBuilder.Append(guid);
                sourceBuilder.Append(", localVariableValues");
                sourceBuilder.Append(guid);
                sourceBuilder.AppendLine(");");
            }
            else
            {
                sourceBuilder.Append("await ");
                sourceBuilder.Append(debuggerGuid);
                sourceBuilder.Append(".Debugger.BreakpointAsync(start");
                sourceBuilder.Append(guid);
                sourceBuilder.Append(", localVariableNames");
                sourceBuilder.Append(guid);
                sourceBuilder.Append(", localVariableDisplay");
                sourceBuilder.Append(guid);
                sourceBuilder.Append(", localVariableValues");
                sourceBuilder.Append(guid);
                sourceBuilder.AppendLine(");");
            }

            sourceBuilder.AppendLine("}");

            return sourceBuilder.ToString();
        }

        internal static SyntaxTree GetDebuggerSyntaxTree(string debuggerGuid)
        {
            StringBuilder debuggerSourceBuilder = new StringBuilder();
            debuggerSourceBuilder.Append("namespace ");
            debuggerSourceBuilder.AppendLine(debuggerGuid);
            debuggerSourceBuilder.AppendLine("{");
            debuggerSourceBuilder.AppendLine("\t public static class Debugger");
            debuggerSourceBuilder.AppendLine("\t{");
            debuggerSourceBuilder.AppendLine("\t\tpublic static System.Action<int, string[], string[], object[]> Breakpoint;");
            debuggerSourceBuilder.AppendLine("\t\tpublic static System.Func<int, string[], string[], object[], System.Threading.Tasks.Task> BreakpointAsync;");
            debuggerSourceBuilder.AppendLine("\t}");
            debuggerSourceBuilder.AppendLine("}");

            SourceText debuggerSourceText = SourceText.From(debuggerSourceBuilder.ToString());

            SyntaxTree tree = CSharpSyntaxTree.ParseText(debuggerSourceText);

            return tree;
        }
    }



    internal class RemoteBreakpointInfo
    {
        /// <summary>
        /// The location in the source code of the breakpoint, including any prepended or appended source code.
        /// </summary>
        public TextSpan BreakpointSpan { get; }

        /// <summary>
        /// A dictionary containing the names and ids of the local variables in scope at the breakpoint.
        /// </summary>
        public Dictionary<string, (string, VariableTypes, object)> LocalVariables { get; }

        public Dictionary<string, TaggedText[]> LocalVariableDisplayParts { get; }

        internal PropertyOrFieldGetter PropertyOrFieldGetter { get; }
        internal ItemsGetter ItemsGetter { get; }

        public RemoteBreakpointInfo(int breakpointStart, Dictionary<string, (string, VariableTypes, object)> localVariables, Dictionary<string, TaggedText[]> localVariablesDisplayParts, PropertyOrFieldGetter propertyOrFieldGetter, ItemsGetter itemsGetter)
        {
            this.BreakpointSpan = new TextSpan(breakpointStart, Utils.BreakpointMarker.Length);
            this.PropertyOrFieldGetter = propertyOrFieldGetter;
            this.ItemsGetter = itemsGetter;
            this.LocalVariables = localVariables;
            this.LocalVariableDisplayParts = localVariablesDisplayParts;
        }
    }
}
