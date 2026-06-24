using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

/// <summary>
/// A parsed C# source file and all artifacts discovered within it.
/// </summary>
public sealed class SourceFile
{
    public string Path { get; init; } = "";
    public SyntaxTree Tree { get; init; } = null!;
    public SyntaxNode Root { get; init; } = null!;
    public string Source { get; init; } = "";
    public string Namespace { get; init; } = "";
    public List<ClassModel> Classes { get; init; } = new();
    public List<InterfaceModel> Interfaces { get; init; } = new();
    /// <summary>All methods across all classes, in source order.</summary>
    public List<MethodModel> AllMethods { get; init; } = new();
}

public sealed class ClassModel
{
    public string Name { get; init; } = "";
    /// <summary>"class", "struct", or "record".</summary>
    public string NodeType { get; init; } = "class";
    public int Line { get; init; }
    public int EndLine { get; init; }
    public bool Exported { get; init; }
    public string Namespace { get; init; } = "";
    public List<FieldModel> Fields { get; init; } = new();
    public List<FieldModel> Constants { get; init; } = new();
    public List<string> BaseTypes { get; init; } = new();
    public List<MethodModel> Methods { get; init; } = new();
    public TypeDeclarationSyntax Node { get; init; } = null!;
    public SourceFile File { get; init; } = null!;
}

public sealed class InterfaceModel
{
    public string Name { get; init; } = "";
    public int Line { get; init; }
    public int EndLine { get; init; }
    public bool Exported { get; init; }
    public string Namespace { get; init; } = "";
    public List<string> BaseTypes { get; init; } = new();
    public List<MethodModel> Methods { get; init; } = new();
    public InterfaceDeclarationSyntax Node { get; init; } = null!;
    public SourceFile File { get; init; } = null!;
}

public sealed class MethodModel
{
    public string Name { get; init; } = "";
    public bool IsConstructor { get; init; }
    public int Line { get; init; }
    public int EndLine { get; init; }
    public bool Exported { get; init; }
    /// <summary>Declared private, including the implicit class-member default.</summary>
    public bool IsPrivate { get; init; }
    public List<ParameterModel> Parameters { get; init; } = new();
    public string ReturnType { get; init; } = "";
    /// <summary>Owning class, or null for interface methods.</summary>
    public ClassModel? Class { get; init; }
    public SyntaxNode Node { get; init; } = null!;
    public BlockSyntax? Body { get; init; }
    public SourceFile File { get; init; } = null!;
}

public sealed class FieldModel
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int Line { get; init; }
    public bool Exported { get; init; }
    /// <summary>Declared private, including the implicit class-member default.</summary>
    public bool IsPrivate { get; init; }
    public bool IsAutoProperty { get; init; }
    public bool IsStatic { get; init; }
    public bool IsReadonly { get; init; }
    public SyntaxNode Node { get; init; } = null!;
}

public sealed class ParameterModel
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int Line { get; init; }
    public ParameterSyntax Node { get; init; } = null!;
}
