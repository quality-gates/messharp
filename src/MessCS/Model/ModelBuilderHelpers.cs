using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Model;

/// <summary>
/// Low-level Roslyn helpers used by ModelBuilder: field, parameter, and
/// auto-property extraction. Extracted to reduce ModelBuilder's coupling count.
/// </summary>
internal static class ModelBuilderHelpers
{
    internal static void CollectFields(TypeDeclarationSyntax node,
        List<FieldModel> fields, List<FieldModel> constants)
    {
        foreach (var member in node.Members)
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
                Exported = IsExported(field.Modifiers),
                IsPrivate = IsPrivate(field.Modifiers),
                IsStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                IsReadonly = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
                Node = v,
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
    // accessibility modifier at all (the C# class-member default).
    internal static bool IsPrivate(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)) ||
        !modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword) ||
                            m.IsKind(SyntaxKind.InternalKeyword) ||
                            m.IsKind(SyntaxKind.ProtectedKeyword));
}
