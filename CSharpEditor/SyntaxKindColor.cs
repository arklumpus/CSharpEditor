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


using Avalonia.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpEditor
{
    internal static class SyntaxKindColor
    {
        public static Color CommentColor = Color.FromRgb(0, 128, 0);
        public static Color? GetColor(this SyntaxToken token)
        {
            bool isIdentifier = false;

            switch (token.Kind())
            {
                case SyntaxKind.BoolKeyword:
                case SyntaxKind.ByteKeyword:
                case SyntaxKind.SByteKeyword:
                case SyntaxKind.ShortKeyword:
                case SyntaxKind.UShortKeyword:
                case SyntaxKind.IntKeyword:
                case SyntaxKind.UIntKeyword:
                case SyntaxKind.LongKeyword:
                case SyntaxKind.ULongKeyword:
                case SyntaxKind.DoubleKeyword:
                case SyntaxKind.FloatKeyword:
                case SyntaxKind.DecimalKeyword:
                case SyntaxKind.StringKeyword:
                case SyntaxKind.CharKeyword:
                case SyntaxKind.VoidKeyword:
                case SyntaxKind.ObjectKeyword:
                case SyntaxKind.TypeOfKeyword:
                case SyntaxKind.SizeOfKeyword:
                case SyntaxKind.NullKeyword:
                case SyntaxKind.TrueKeyword:
                case SyntaxKind.FalseKeyword:
                case SyntaxKind.DefaultKeyword:
                case SyntaxKind.LockKeyword:
                case SyntaxKind.PublicKeyword:
                case SyntaxKind.PrivateKeyword:
                case SyntaxKind.InternalKeyword:
                case SyntaxKind.ProtectedKeyword:
                case SyntaxKind.StaticKeyword:
                case SyntaxKind.ReadOnlyKeyword:
                case SyntaxKind.SealedKeyword:
                case SyntaxKind.ConstKeyword:
                case SyntaxKind.FixedKeyword:
                case SyntaxKind.StackAllocKeyword:
                case SyntaxKind.VolatileKeyword:
                case SyntaxKind.NewKeyword:
                case SyntaxKind.OverrideKeyword:
                case SyntaxKind.AbstractKeyword:
                case SyntaxKind.VirtualKeyword:
                case SyntaxKind.EventKeyword:
                case SyntaxKind.ExternKeyword:
                case SyntaxKind.RefKeyword:
                case SyntaxKind.OutKeyword:
                case SyntaxKind.InKeyword:
                case SyntaxKind.IsKeyword:
                case SyntaxKind.AsKeyword:
                case SyntaxKind.ParamsKeyword:
                case SyntaxKind.ArgListKeyword:
                case SyntaxKind.MakeRefKeyword:
                case SyntaxKind.RefTypeKeyword:
                case SyntaxKind.RefValueKeyword:
                case SyntaxKind.ThisKeyword:
                case SyntaxKind.BaseKeyword:
                case SyntaxKind.NamespaceKeyword:
                case SyntaxKind.UsingKeyword:
                case SyntaxKind.ClassKeyword:
                case SyntaxKind.StructKeyword:
                case SyntaxKind.InterfaceKeyword:
                case SyntaxKind.EnumKeyword:
                case SyntaxKind.DelegateKeyword:
                case SyntaxKind.CheckedKeyword:
                case SyntaxKind.UncheckedKeyword:
                case SyntaxKind.UnsafeKeyword:
                case SyntaxKind.OperatorKeyword:
                case SyntaxKind.ExplicitKeyword:
                case SyntaxKind.ImplicitKeyword:
                case SyntaxKind.YieldKeyword:
                case SyntaxKind.PartialKeyword:
                case SyntaxKind.AliasKeyword:
                case SyntaxKind.GlobalKeyword:
                case SyntaxKind.AssemblyKeyword:
                case SyntaxKind.ModuleKeyword:
                case SyntaxKind.TypeKeyword:
                case SyntaxKind.FieldKeyword:
                case SyntaxKind.MethodKeyword:
                case SyntaxKind.ParamKeyword:
                case SyntaxKind.PropertyKeyword:
                case SyntaxKind.TypeVarKeyword:
                case SyntaxKind.GetKeyword:
                case SyntaxKind.SetKeyword:
                case SyntaxKind.AddKeyword:
                case SyntaxKind.RemoveKeyword:
                case SyntaxKind.WhereKeyword:
                case SyntaxKind.FromKeyword:
                case SyntaxKind.GroupKeyword:
                case SyntaxKind.JoinKeyword:
                case SyntaxKind.IntoKeyword:
                case SyntaxKind.LetKeyword:
                case SyntaxKind.ByKeyword:
                case SyntaxKind.SelectKeyword:
                case SyntaxKind.OrderByKeyword:
                case SyntaxKind.OnKeyword:
                case SyntaxKind.EqualsKeyword:
                case SyntaxKind.AscendingKeyword:
                case SyntaxKind.DescendingKeyword:
                case SyntaxKind.NameOfKeyword:
                case SyntaxKind.AsyncKeyword:
                case SyntaxKind.AwaitKeyword:
                case SyntaxKind.WhenKeyword:
                case SyntaxKind.OrKeyword:
                case SyntaxKind.AndKeyword:
                case SyntaxKind.NotKeyword:
                case SyntaxKind.WithKeyword:
                case SyntaxKind.InitKeyword:
                case SyntaxKind.RecordKeyword:
                case SyntaxKind.ManagedKeyword:
                case SyntaxKind.UnmanagedKeyword:
                case SyntaxKind.ElifKeyword:
                case SyntaxKind.EndIfKeyword:
                case SyntaxKind.RegionKeyword:
                case SyntaxKind.EndRegionKeyword:
                case SyntaxKind.DefineKeyword:
                case SyntaxKind.UndefKeyword:
                case SyntaxKind.WarningKeyword:
                case SyntaxKind.ErrorKeyword:
                case SyntaxKind.LineKeyword:
                case SyntaxKind.PragmaKeyword:
                case SyntaxKind.HiddenKeyword:
                case SyntaxKind.ChecksumKeyword:
                case SyntaxKind.DisableKeyword:
                case SyntaxKind.RestoreKeyword:
                case SyntaxKind.ReferenceKeyword:
                case SyntaxKind.LoadKeyword:
                case SyntaxKind.NullableKeyword:
                case SyntaxKind.EnableKeyword:
                case SyntaxKind.WarningsKeyword:
                case SyntaxKind.AnnotationsKeyword:
                case SyntaxKind.VarKeyword:
                    return Color.FromRgb(0, 0, 255);

                case SyntaxKind.IfKeyword:
                case SyntaxKind.ElseKeyword:
                case SyntaxKind.WhileKeyword:
                case SyntaxKind.ForKeyword:
                case SyntaxKind.ForEachKeyword:
                case SyntaxKind.DoKeyword:
                case SyntaxKind.SwitchKeyword:
                case SyntaxKind.CaseKeyword:
                case SyntaxKind.TryKeyword:
                case SyntaxKind.CatchKeyword:
                case SyntaxKind.FinallyKeyword:
                case SyntaxKind.GotoKeyword:
                case SyntaxKind.BreakKeyword:
                case SyntaxKind.ContinueKeyword:
                case SyntaxKind.ReturnKeyword:
                case SyntaxKind.ThrowKeyword:
                    return Color.FromRgb(143, 8, 96);

                case SyntaxKind.CharacterLiteralToken:
                case SyntaxKind.StringLiteralToken:
                    return Color.FromRgb(163, 21, 21);

                case SyntaxKind.NumericLiteralToken:
                    return Color.FromRgb(0, 0, 0);

                case SyntaxKind.IdentifierToken:
                    isIdentifier = true;
                    break;
            }

            if (isIdentifier && token.GetNextToken().IsKind(SyntaxKind.OpenParenToken))
            {
                switch (token.Parent.Kind())
                {
                    case SyntaxKind.DelegateDeclaration:
                        if (((DelegateDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.ConstructorDeclaration:
                        if (((ConstructorDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.DestructorDeclaration:
                        if (((DestructorDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    default:
                        if (token.Parent.Parent.IsKind(SyntaxKind.ObjectCreationExpression) && ((ObjectCreationExpressionSyntax)token.Parent.Parent).Type == token.Parent)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        else
                        {
                            return Color.FromRgb(116, 83, 31);
                        }
                }

            }
            else if (isIdentifier)
            {
                switch (token.Parent.Kind())
                {
                    case SyntaxKind.ClassDeclaration:
                        if (((ClassDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.EnumDeclaration:
                        if (((EnumDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.StructDeclaration:
                        if (((StructDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.InterfaceDeclaration:
                        if (((InterfaceDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.DelegateDeclaration:
                        if (((DelegateDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.ConstructorDeclaration:
                        if (((ConstructorDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.DestructorDeclaration:
                        if (((DestructorDeclarationSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                    case SyntaxKind.TypeParameter:
                        if (((TypeParameterSyntax)token.Parent).Identifier == token)
                        {
                            return Color.FromRgb(43, 145, 175);
                        }
                        break;
                }
            }

            return null;
        }

        public static Color GetIdentifierColor(this SyntaxToken token, SemanticModel model)
        {
            SymbolInfo parentInfo = model.GetSymbolInfo(token.Parent);

            if (parentInfo.Symbol != null)
            {
                switch (parentInfo.Symbol.Kind)
                {
                    case SymbolKind.NamedType:
                    case SymbolKind.DynamicType:
                    case SymbolKind.ArrayType:
                    case SymbolKind.PointerType:
                    case SymbolKind.TypeParameter:
                        return Color.FromRgb(43, 145, 175);
                }
            }

            return Color.FromRgb(0, 0, 0);
        }
    }
}
