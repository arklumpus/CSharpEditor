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

using Avalonia.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CSharpEditor
{
    internal static class Utils
    {
        public static KeyModifiers ControlCmdModifier { get; } = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? KeyModifiers.Meta : KeyModifiers.Control;

        internal const string BreakpointMarker = "/* Breakpoint */";
        internal const string Tab = "    ";

        private static ImmutableDictionary<string, string> _referenceDocumentation;

        public static async Task<ImmutableDictionary<string, string>> GetReferenceDocumentation()
        {
            if (_referenceDocumentation == null)
            {
                _referenceDocumentation = await System.Text.Json.JsonSerializer.DeserializeAsync<ImmutableDictionary<string, string>>(typeof(Utils).Assembly.GetManifestResourceStream("CSharpEditor.xmldocs.json"));
            }

            return _referenceDocumentation;
        }

        public static ImmutableList<string> CoreReferences { get; }

        static Utils()
        {
            using (StreamReader reader = new StreamReader(typeof(Utils).Assembly.GetManifestResourceStream("CSharpEditor.xmldocs.dll.list")))
            {
                List<string> lines = new List<string>();

                while (!reader.EndOfStream)
                {
                    lines.Add(reader.ReadLine());
                }

                CoreReferences = ImmutableList.Create(lines.ToArray());
            }
        }

        public static List<TextSpan> Join(this IEnumerable<TextSpan> spans)
        {
            List<TextSpan> tbr = new List<TextSpan>();
            foreach (TextSpan span in spans)
            {
                bool found = false;

                for (int i = 0; i < tbr.Count; i++)
                {
                    if (tbr[i].IntersectsWith(span) || tbr[i].End + 1 == span.Start || tbr[i].Start == span.End + 1)
                    {
                        int start = Math.Min(tbr[i].Start, span.Start);
                        int end = Math.Max(tbr[i].End, span.End);

                        tbr[i] = new TextSpan(start, end - start);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tbr.Add(span);
                }
            }

            return tbr;
        }

        public static List<(TextSpan, List<T>)> Join<T>(this IEnumerable<(TextSpan, T)> spans)
        {
            List<(TextSpan, List<T>)> tbr = new List<(TextSpan, List<T>)>();
            foreach ((TextSpan span, T diag) in spans)
            {
                bool found = false;

                for (int i = 0; i < tbr.Count; i++)
                {
                    if (tbr[i].Item1.IntersectsWith(span))
                    {
                        int start = Math.Min(tbr[i].Item1.Start, span.Start);
                        int end = Math.Max(tbr[i].Item1.End, span.End);

                        tbr[i].Item2.Add(diag);

                        tbr[i] = (new TextSpan(start, end - start), tbr[i].Item2);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tbr.Add((span, new List<T>() { diag }));
                }
            }

            return tbr;
        }

        //Adapted from https://github.com/dotnet/roslyn/blob/79400d2390e14235f9345c6cc8b05e035a338219/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Extensions/ISymbolExtensions.cs
        /// <returns>
        /// Returns true if symbol is a local variable and its declaring syntax node is 
        /// after the current position, false otherwise (including for non-local symbols)
        /// </returns>
        public static bool IsInaccessibleLocal(this ISymbol symbol, SemanticModel model, int position, SyntaxNode nodeAtPosition)
        {
            if (symbol.Kind != SymbolKind.Local)
            {
                return false;
            }

            // Implicitly declared locals (with Option Explicit Off in VB) are scoped to the entire
            // method and should always be considered accessible from within the same method.
            if (symbol.IsImplicitlyDeclared)
            {
                return false;
            }

            var declarationSyntax = symbol.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).FirstOrDefault();

            if (declarationSyntax != null && position < declarationSyntax.SpanStart)
            {
                return true;
            }
            else
            {
                SyntaxNode firstStatement = declarationSyntax;
                SyntaxNode lastStatement = nodeAtPosition;

                while (firstStatement.Parent != null && !firstStatement.Kind().IsStatement())
                {
                    firstStatement = firstStatement.Parent;
                }

                while (lastStatement.Parent != null && !lastStatement.Kind().IsStatement())
                {
                    lastStatement = lastStatement.Parent;
                }

                if (!lastStatement.Kind().IsStatement() || !firstStatement.Kind().IsStatement())
                {
                    return true;
                }
                else
                {
                    try
                    {
                        DataFlowAnalysis analysis = model.AnalyzeDataFlow(firstStatement, lastStatement);
                        return !analysis.DefinitelyAssignedOnExit.Contains(symbol);
                    }
                    catch
                    {
                        return true;
                    }
                }
            }
        }


        //Adapted from https://stackoverflow.com/questions/2641326/finding-all-positions-of-substring-in-a-larger-string-in-c-sharp
        public static IEnumerable<int> AllIndicesOf(this string text, string pattern, bool caseInsensitive = false)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentNullException(nameof(pattern));
            }
            return Kmp(text, pattern, caseInsensitive);
        }

        private static IEnumerable<int> Kmp(string text, string pattern, bool caseInsensitive)
        {
            int M = pattern.Length;
            int N = text.Length;

            int[] lps = LongestPrefixSuffix(pattern, caseInsensitive);
            int i = 0, j = 0;

            while (i < N)
            {
                if (pattern[j].IsEqual(text[i], caseInsensitive))
                {
                    j++;
                    i++;
                }
                if (j == M)
                {
                    yield return i - j;
                    j = lps[j - 1];
                }

                else if (i < N && !pattern[j].IsEqual(text[i], caseInsensitive))
                {
                    if (j != 0)
                    {
                        j = lps[j - 1];
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        private static int[] LongestPrefixSuffix(string pattern, bool caseInsensitive)
        {
            int[] lps = new int[pattern.Length];
            int length = 0;
            int i = 1;

            while (i < pattern.Length)
            {
                if (pattern[i].IsEqual(pattern[length], caseInsensitive))
                {
                    length++;
                    lps[i] = length;
                    i++;
                }
                else
                {
                    if (length != 0)
                    {
                        length = lps[length - 1];
                    }
                    else
                    {
                        lps[i] = length;
                        i++;
                    }
                }
            }
            return lps;
        }

        private static bool IsEqual(this char char1, char char2, bool caseInsensitive)
        {
            return char1 == char2 || (caseInsensitive && char.ToUpperInvariant(char1) == char.ToUpperInvariant(char2));
        }
    }

    internal static class Extensions
    {
        public static T? FirstOrNull<T>(this IEnumerable<T> list) where T : struct
        {
            foreach (T item in list)
            {
                return item;
            }
            return null;
        }

        public static T? LastOrNull<T>(this IEnumerable<T> list) where T : struct
        {
            if (list.Any())
            {
                return list.Last();
            }
            else
            {
                return null;
            }
        }

        public static bool IsNavigation(this Key key)
        {
            switch (key)
            {
                case Key.Up:
                case Key.Down:
                case Key.Left:
                case Key.Right:
                case Key.PageUp:
                case Key.PageDown:
                case Key.Home:
                case Key.End:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsStatement(this SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.LocalDeclarationStatement:
                case SyntaxKind.ExpressionStatement:
                case SyntaxKind.EmptyStatement:
                case SyntaxKind.LabeledStatement:
                case SyntaxKind.GotoStatement:
                case SyntaxKind.GotoCaseStatement:
                case SyntaxKind.GotoDefaultStatement:
                case SyntaxKind.BreakStatement:
                case SyntaxKind.ContinueStatement:
                case SyntaxKind.ReturnStatement:
                case SyntaxKind.YieldReturnStatement:
                case SyntaxKind.YieldBreakStatement:
                case SyntaxKind.ThrowStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.UsingStatement:
                case SyntaxKind.FixedStatement:
                case SyntaxKind.CheckedStatement:
                case SyntaxKind.UncheckedStatement:
                case SyntaxKind.UnsafeStatement:
                case SyntaxKind.LockStatement:
                case SyntaxKind.IfStatement:
                case SyntaxKind.SwitchStatement:
                case SyntaxKind.TryStatement:
                case SyntaxKind.LocalFunctionStatement:
                case SyntaxKind.GlobalStatement:
                case SyntaxKind.ForEachVariableStatement:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsAssignment(this SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.SimpleAssignmentExpression:
                case SyntaxKind.AddAssignmentExpression:
                case SyntaxKind.SubtractAssignmentExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                case SyntaxKind.DivideAssignmentExpression:
                case SyntaxKind.ModuloAssignmentExpression:
                case SyntaxKind.AndAssignmentExpression:
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                case SyntaxKind.OrAssignmentExpression:
                case SyntaxKind.LeftShiftAssignmentExpression:
                case SyntaxKind.RightShiftAssignmentExpression:
                case SyntaxKind.CoalesceAssignmentExpression:
                    return true;

                default:
                    return false;
            }
        }

        public static TaggedText ToTaggedText(this SymbolDisplayPart part)
        {
            return new TaggedText(part.Kind.ToTextTag(), part.ToString());
        }

        public static string ToTextTag(this SymbolDisplayPartKind kind)
        {
            switch (kind)
            {
                case SymbolDisplayPartKind.AliasName:
                    return TextTags.Alias;
                case SymbolDisplayPartKind.AssemblyName:
                    return TextTags.Assembly;
                case SymbolDisplayPartKind.ClassName:
                    return TextTags.Class;
                case SymbolDisplayPartKind.DelegateName:
                    return TextTags.Delegate;
                case SymbolDisplayPartKind.EnumName:
                    return TextTags.Enum;
                case SymbolDisplayPartKind.ErrorTypeName:
                    return TextTags.ErrorType;
                case SymbolDisplayPartKind.EventName:
                    return TextTags.Event;
                case SymbolDisplayPartKind.FieldName:
                    return TextTags.Field;
                case SymbolDisplayPartKind.InterfaceName:
                    return TextTags.Interface;
                case SymbolDisplayPartKind.Keyword:
                    return TextTags.Keyword;
                case SymbolDisplayPartKind.LabelName:
                    return TextTags.Label;
                case SymbolDisplayPartKind.LineBreak:
                    return TextTags.LineBreak;
                case SymbolDisplayPartKind.NumericLiteral:
                    return TextTags.NumericLiteral;
                case SymbolDisplayPartKind.StringLiteral:
                    return TextTags.StringLiteral;
                case SymbolDisplayPartKind.LocalName:
                    return TextTags.Local;
                case SymbolDisplayPartKind.MethodName:
                    return TextTags.Method;
                case SymbolDisplayPartKind.ModuleName:
                    return TextTags.Module;
                case SymbolDisplayPartKind.NamespaceName:
                    return TextTags.Namespace;
                case SymbolDisplayPartKind.Operator:
                    return TextTags.Operator;
                case SymbolDisplayPartKind.ParameterName:
                    return TextTags.Parameter;
                case SymbolDisplayPartKind.PropertyName:
                    return TextTags.Property;
                case SymbolDisplayPartKind.Punctuation:
                    return TextTags.Punctuation;
                case SymbolDisplayPartKind.Space:
                    return TextTags.Space;
                case SymbolDisplayPartKind.StructName:
                    return TextTags.Struct;
                case SymbolDisplayPartKind.AnonymousTypeIndicator:
                    return TextTags.AnonymousTypeIndicator;
                case SymbolDisplayPartKind.Text:
                    return TextTags.Text;
                case SymbolDisplayPartKind.TypeParameterName:
                    return TextTags.TypeParameter;
                case SymbolDisplayPartKind.RangeVariableName:
                    return TextTags.RangeVariable;
                case SymbolDisplayPartKind.EnumMemberName:
                    return TextTags.EnumMember;
                case SymbolDisplayPartKind.ExtensionMethodName:
                    return TextTags.ExtensionMethod;
                case SymbolDisplayPartKind.ConstantName:
                    return TextTags.Constant;
                default:
                    return TextTags.ErrorType;
            }
        }

        //From https://stackoverflow.com/questions/323640/can-i-convert-a-c-sharp-string-value-to-an-escaped-string-literal
        internal static string ToLiteral(this string input)
        {
            using (var writer = new StringWriter())
            using (var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                return writer.ToString();
            }
        }

        //From https://stackoverflow.com/questions/323640/can-i-convert-a-c-sharp-string-value-to-an-escaped-string-literal
        internal static string ToLiteral(this char input)
        {
            using (var writer = new StringWriter())
            using (var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                return writer.ToString();
            }
        }

        /*public static IEnumerable<LinePositionSpan> ToLinePositionSpans(this IEnumerable<TextSpan> spans, SourceText text)
        {
            if (!spans.Any())
            {
                yield break;
            }

            foreach (TextSpan span in spans)
            {
                foreach (LinePositionSpan lineSpan in span.ToLinePositionSpans(text))
                {
                    yield return lineSpan;
                }
            }
        }*/

        public static IEnumerable<LinePositionSpan> ToLinePositionSpans(this TextSpan span, SourceText text)
        {
            LinePositionSpan lineSpan = text.Lines.GetLinePositionSpan(span);

            while (lineSpan.Start.Line != lineSpan.End.Line)
            {
                yield return new LinePositionSpan(lineSpan.Start, new LinePosition(lineSpan.Start.Line, Math.Min(text.Lines[lineSpan.Start.Line].Span.Length + 1, text.Lines[lineSpan.Start.Line].SpanIncludingLineBreak.Length)));
                lineSpan = new LinePositionSpan(new LinePosition(lineSpan.Start.Line + 1, 0), lineSpan.End);
            }

            yield return lineSpan;
        }

        public static TextSpan? ApplyChanges(this TextSpan span, IEnumerable<TextChange> changes)
        {
            int start = span.Start;
            int end = span.End;

            foreach (TextChange change in changes)
            {
                start = ApplyChange(start, change);
                end = ApplyChange(end, change);
            }

            if (start >= 0 && end >= 0 && end >= start)
            {
                return new TextSpan(start, end - start);
            }
            else
            {
                return null;
            }
        }

        public static int ApplyChange(int position, TextChange change)
        {
            if (position <= change.Span.Start)
            {
                return position;
            }
            else if (position >= change.Span.End)
            {
                return position + change.Span.Length - change.NewText.Length;
            }
            else
            {
                return -1;
            }
        }
    }

    internal class ReadWriteTaggedText
    {
        public string Tag { get; set; }
        public string Text { get; set; }

        public static implicit operator TaggedText(ReadWriteTaggedText rwTT)
        {
            return new TaggedText(rwTT.Tag, rwTT.Text);
        }

        public static implicit operator ReadWriteTaggedText(TaggedText tt)
        {
            return new ReadWriteTaggedText() { Tag = tt.Tag, Text = tt.Text };
        }
    }


}
