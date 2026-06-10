using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Model;

/// <summary>
/// Builds a <see cref="SourceFile"/> from C# source text using Roslyn.
/// </summary>
public static class ModelBuilder
{
    public static SourceFile Parse(string path, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: path);
        var root = tree.GetRoot();
        var ns = ExtractNamespace(root);
        var file = new SourceFile
        {
            Path = path,
            Tree = tree,
            Root = root,
            Source = source,
            Namespace = ns,
        };
        BuildArtifacts(file, root);
        return file;
    }

    public static SourceFile ParseFile(string path)
    {
        var source = File.ReadAllText(path);
        return Parse(path, source);
    }

    private static string ExtractNamespace(SyntaxNode root)
    {
        var ns = root.DescendantNodesAndSelf()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        return ns?.Name.ToString() ?? "";
    }

    private static void BuildArtifacts(SourceFile file, SyntaxNode root)
    {
        var nsName = file.Namespace;

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case InterfaceDeclarationSyntax iface:
                    if (!IsNested(iface))
                        file.Interfaces.Add(BuildInterface(file, iface, nsName));
                    break;
                case TypeDeclarationSyntax type
                    when type is ClassDeclarationSyntax
                      || type is StructDeclarationSyntax
                      || type is RecordDeclarationSyntax:
                    if (!IsNested(type))
                        file.Classes.Add(BuildClass(file, type, nsName));
                    break;
            }
        }

        foreach (var cls in file.Classes)
            file.AllMethods.AddRange(cls.Methods);
    }

    private static bool IsNested(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is TypeDeclarationSyntax or InterfaceDeclarationSyntax)
                return true;
            parent = parent.Parent;
        }
        return false;
    }

    private static ClassModel BuildClass(SourceFile file, TypeDeclarationSyntax node, string ns)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        var nodeType = node switch
        {
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax => "record",
            _ => "class",
        };

        var fields = new List<FieldModel>();
        var constants = new List<FieldModel>();
        var baseTypes = CollectBaseTypes(node.BaseList);

        CollectFields(node, fields, constants);

        var cls = new ClassModel
        {
            Name = node.Identifier.Text,
            NodeType = nodeType,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = IsExported(node.Modifiers),
            Namespace = ns,
            Fields = fields,
            Constants = constants,
            BaseTypes = baseTypes,
            Node = node,
            File = file,
        };

        foreach (var member in node.Members)
        {
            var method = TryBuildMethod(file, cls, member);
            if (method != null)
                cls.Methods.Add(method);
        }

        return cls;
    }

    private static void CollectFields(TypeDeclarationSyntax node,
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

    private static void CollectFieldDecl(FieldDeclarationSyntax field,
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
                Node = v,
            });
        }
    }

    private static bool IsAutoProperty(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList == null) return false;
        return prop.AccessorList.Accessors.All(
            a => a.Body == null && a.ExpressionBody == null);
    }

    private static FieldModel BuildAutoPropertyField(PropertyDeclarationSyntax prop)
    {
        var span = prop.SyntaxTree.GetLineSpan(prop.Span);
        return new FieldModel
        {
            Name = prop.Identifier.Text,
            Type = prop.Type.ToString(),
            Line = span.StartLinePosition.Line + 1,
            Exported = IsExported(prop.Modifiers),
            IsAutoProperty = true,
            Node = prop,
        };
    }

    private static MethodModel? TryBuildMethod(SourceFile file, ClassModel cls, MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax m => BuildMethod(file, cls, m),
            ConstructorDeclarationSyntax c => BuildConstructor(file, cls, c),
            _ => null,
        };
    }

    private static MethodModel BuildMethod(SourceFile file, ClassModel cls, MethodDeclarationSyntax node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new MethodModel
        {
            Name = node.Identifier.Text,
            IsConstructor = false,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = IsExported(node.Modifiers),
            Parameters = BuildParameters(node.ParameterList),
            ReturnType = node.ReturnType.ToString(),
            Class = cls,
            Node = node,
            Body = node.Body,
            File = file,
        };
    }

    private static MethodModel BuildConstructor(SourceFile file, ClassModel cls, ConstructorDeclarationSyntax node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new MethodModel
        {
            Name = node.Identifier.Text,
            IsConstructor = true,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = IsExported(node.Modifiers),
            Parameters = BuildParameters(node.ParameterList),
            ReturnType = "",
            Class = cls,
            Node = node,
            Body = node.Body,
            File = file,
        };
    }

    private static InterfaceModel BuildInterface(SourceFile file, InterfaceDeclarationSyntax node, string ns)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        var methods = new List<MethodModel>();

        foreach (var member in node.Members)
        {
            if (member is MethodDeclarationSyntax m)
            {
                var mSpan = m.SyntaxTree.GetLineSpan(m.Span);
                methods.Add(new MethodModel
                {
                    Name = m.Identifier.Text,
                    IsConstructor = false,
                    Line = mSpan.StartLinePosition.Line + 1,
                    EndLine = mSpan.EndLinePosition.Line + 1,
                    Exported = IsExported(m.Modifiers),
                    Parameters = BuildParameters(m.ParameterList),
                    ReturnType = m.ReturnType.ToString(),
                    Node = m,
                    Body = m.Body,
                    File = file,
                });
            }
        }

        return new InterfaceModel
        {
            Name = node.Identifier.Text,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = IsExported(node.Modifiers),
            Namespace = ns,
            BaseTypes = CollectBaseTypes(node.BaseList),
            Methods = methods,
            Node = node,
            File = file,
        };
    }

    private static List<ParameterModel> BuildParameters(ParameterListSyntax? paramList)
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

    private static List<string> CollectBaseTypes(BaseListSyntax? baseList)
    {
        if (baseList == null) return new();
        return baseList.Types.Select(t => t.Type.ToString()).ToList();
    }

    private static bool IsExported(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
}
