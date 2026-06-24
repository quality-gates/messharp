using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

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
        var source = System.IO.File.ReadAllText(path);
        return Parse(path, source);
    }

    private static string ExtractNamespace(Microsoft.CodeAnalysis.SyntaxNode root)
    {
        var ns = root.DescendantNodesAndSelf()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        return ns?.Name.ToString() ?? "";
    }

    private static void BuildArtifacts(SourceFile file, Microsoft.CodeAnalysis.SyntaxNode root)
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

    private static bool IsNested(Microsoft.CodeAnalysis.SyntaxNode node)
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
        var baseTypes = ModelBuilderHelpers.CollectBaseTypes(node.BaseList);
        ModelBuilderHelpers.CollectFields(node, fields, constants);

        var cls = new ClassModel
        {
            Name = node.Identifier.Text,
            NodeType = nodeType,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
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
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
            IsPrivate = ModelBuilderHelpers.IsPrivate(node.Modifiers),
            Parameters = ModelBuilderHelpers.BuildParameters(node.ParameterList),
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
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
            IsPrivate = ModelBuilderHelpers.IsPrivate(node.Modifiers),
            Parameters = ModelBuilderHelpers.BuildParameters(node.ParameterList),
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
                    Exported = ModelBuilderHelpers.IsExported(m.Modifiers),
                    Parameters = ModelBuilderHelpers.BuildParameters(m.ParameterList),
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
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
            Namespace = ns,
            BaseTypes = ModelBuilderHelpers.CollectBaseTypes(node.BaseList),
            Methods = methods,
            Node = node,
            File = file,
        };
    }
}
