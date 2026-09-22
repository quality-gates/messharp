using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

/// <summary>
/// Low-level Roslyn helpers used by ModelBuilder: field, parameter, and
/// auto-property extraction. Extracted to reduce ModelBuilder's coupling count.
/// </summary>
internal static class ModelBuilderHelpers
{
    internal static IEnumerable<MemberDeclarationSyntax> MembersOf(BaseTypeDeclarationSyntax node) =>
        node switch
        {
            TypeDeclarationSyntax type => type.Members,
            EnumDeclarationSyntax enumNode => enumNode.Members,
            _ => Enumerable.Empty<MemberDeclarationSyntax>(),
        };

    internal static void CollectFields(BaseTypeDeclarationSyntax node,
        List<FieldModel> fields, List<FieldModel> constants)
    {
        if (node is EnumDeclarationSyntax enumNode)
        {
            foreach (var member in enumNode.Members)
            {
                var span = member.SyntaxTree.GetLineSpan(member.Identifier.Span);
                var enumMember = new FieldModel
                {
                    Name = member.Identifier.Text,
                    Type = enumNode.Identifier.Text,
                    Line = span.StartLinePosition.Line + 1,
                    EndLine = span.EndLinePosition.Line + 1,
                    Exported = true,
                    IsStatic = true,
                    IsReadonly = true,
                    Node = member,
                };
                fields.Add(enumMember);
                constants.Add(enumMember);
            }
            return;
        }

        foreach (var member in MembersOf(node))
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    CollectFieldDecl(field, fields, constants);
                    break;
                case PropertyDeclarationSyntax prop when IsAutoProperty(prop):
                    fields.Add(BuildAutoPropertyField(prop));
                    break;
            }
        }
    }

    internal static void CollectFieldDecl(FieldDeclarationSyntax field,
        List<FieldModel> fields, List<FieldModel> constants)
    {
        var typeStr = field.Declaration.Type.ToString();
        var isConst = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));
        var target = isConst ? constants : fields;

        foreach (var v in field.Declaration.Variables)
        {
            var vSpan = field.SyntaxTree.GetLineSpan(v.Span);
            target.Add(new FieldModel
            {
                Name = v.Identifier.Text,
                Type = typeStr,
                Line = vSpan.StartLinePosition.Line + 1,
                EndLine = vSpan.EndLinePosition.Line + 1,
                Exported = IsExported(field.Modifiers),
                IsPrivate = IsPrivate(field.Modifiers),
                IsStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                IsReadonly = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
                Node = field,
            });
        }
    }

    internal static bool IsAutoProperty(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList == null) return false;
        return prop.AccessorList.Accessors.All(
            a => a.Body == null && a.ExpressionBody == null);
    }

    internal static FieldModel BuildAutoPropertyField(PropertyDeclarationSyntax prop)
    {
        var span = prop.SyntaxTree.GetLineSpan(prop.Span);
        return new FieldModel
        {
            Name = prop.Identifier.Text,
            Type = prop.Type.ToString(),
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = IsExported(prop.Modifiers),
            IsPrivate = IsPrivate(prop.Modifiers),
            IsAutoProperty = true,
            Node = prop,
        };
    }

    internal static List<ParameterModel> BuildParameters(ParameterListSyntax? paramList)
    {
        if (paramList == null) return new();
        var result = new List<ParameterModel>();
        foreach (var p in paramList.Parameters)
        {
            var span = p.SyntaxTree.GetLineSpan(p.Span);
            result.Add(new ParameterModel
            {
                Name = p.Identifier.Text,
                Type = p.Type?.ToString() ?? "",
                Line = span.StartLinePosition.Line + 1,
                IsOut = p.Modifiers.Any(SyntaxKind.OutKeyword),
                Node = p,
            });
        }
        return result;
    }

    internal static List<string> CollectBaseTypes(BaseListSyntax? baseList)
    {
        if (baseList == null) return new();
        return baseList.Types.Select(t => t.Type.ToString()).ToList();
    }

    internal static bool IsExported(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));

    // A class member is private when declared so, or when it carries no
    // accessibility modifier at all (the C# class-member default). Any
    // ProtectedKeyword rules out private, even combined with PrivateKeyword
    // (private protected escapes the containing file to derived types).
    internal static bool IsPrivate(SyntaxTokenList modifiers) =>
        !modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword) ||
                            m.IsKind(SyntaxKind.InternalKeyword) ||
                            m.IsKind(SyntaxKind.ProtectedKeyword));

    internal static bool IsInterfaceMethodExported(SyntaxTokenList modifiers, bool interfaceExported) =>
        IsExported(modifiers) ||
        (interfaceExported && !modifiers.Any(m =>
            m.IsKind(SyntaxKind.PrivateKeyword) ||
            m.IsKind(SyntaxKind.InternalKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword)));

    internal static bool IsInterfaceMethodPrivate(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));

    internal static string EnclosingNamespace(SyntaxNode node)
    {
        var names = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(ns => ns.Name.ToString())
            .Reverse();
        return string.Join(".", names);
    }
}
