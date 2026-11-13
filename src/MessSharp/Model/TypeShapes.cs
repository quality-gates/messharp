using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

/// <summary>
/// Syntax-only questions about the shape of a declared C# type. Rules ask these
/// instead of re-parsing or string-matching the formatted type text; no
/// compiler binding is involved, so aliases from <c>using</c> directives are
/// not resolved.
/// </summary>
public static class TypeShapes
{
    /// <summary>
    /// <c>bool</c>, <c>Boolean</c>, <c>System.Boolean</c> or <c>global::System.Boolean</c>,
    /// optionally wrapped as <c>T?</c> or <c>[System.]Nullable&lt;T&gt;</c>.
    /// </summary>
    public static bool IsBoolean(TypeSyntax? type) => type != null && IsBooleanName(UnwrapNullable(type));

    /// <summary>
    /// The declared return type syntax of <paramref name="method"/>, or null for
    /// constructors, destructors, setters and other members without one.
    /// </summary>
    public static TypeSyntax? ReturnTypeOf(MethodModel method) => method.Node switch
    {
        MethodDeclarationSyntax declaration => declaration.ReturnType,
        OperatorDeclarationSyntax declaration => declaration.ReturnType,
        ConversionOperatorDeclarationSyntax declaration => declaration.Type,
        AccessorDeclarationSyntax { Parent.Parent: PropertyDeclarationSyntax property } accessor
            when accessor.Keyword.IsKind(SyntaxKind.GetKeyword) => property.Type,
        _ => null,
    };

    /// <summary><c>void</c>.</summary>
    public static bool IsVoid(TypeSyntax? type) =>
        type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

    /// <summary>A non-generic <c>Task</c> or <c>ValueTask</c>, unqualified or qualified.</summary>
    public static bool IsTaskWithoutResult(TypeSyntax? type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text is "Task" or "ValueTask",
        QualifiedNameSyntax q => IsTaskWithoutResult(q.Right),
        _ => false,
    };

    /// <summary>
    /// The simple name of every named type in <paramref name="type"/>, including
    /// generic type arguments and tuple, array and nullable element types.
    /// Namespace and alias qualifiers are skipped, so
    /// <c>System.Threading.Tasks.Task&lt;Customer&gt;</c> yields Task and Customer.
    /// </summary>
    public static IEnumerable<string> NamedTypes(TypeSyntax? type) => type == null
        ? []
        : type.DescendantNodesAndSelf()
            .OfType<SimpleNameSyntax>()
            .Where(name => !IsQualifier(name))
            .Select(name => name.Identifier.ValueText)
            .Where(name => name.Length > 0);

    private static TypeSyntax UnwrapNullable(TypeSyntax type) => type switch
    {
        NullableTypeSyntax nullable => nullable.ElementType,
        GenericNameSyntax { Identifier.Text: "Nullable", TypeArgumentList.Arguments: [var inner] } => inner,
        QualifiedNameSyntax { Left: IdentifierNameSyntax { Identifier.Text: "System" }, Right: GenericNameSyntax { Identifier.Text: "Nullable", TypeArgumentList.Arguments: [var inner] } } => inner,
        _ => type,
    };

    private static bool IsBooleanName(TypeSyntax type) => type switch
    {
        PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.BoolKeyword),
        IdentifierNameSyntax id => id.Identifier.Text == "Boolean",
        QualifiedNameSyntax { Left: var left, Right.Identifier.Text: "Boolean" } => IsSystemNamespace(left),
        _ => false,
    };

    private static bool IsSystemNamespace(NameSyntax name) => name switch
    {
        IdentifierNameSyntax id => id.Identifier.Text == "System",
        AliasQualifiedNameSyntax { Alias.Identifier.Text: "global", Name.Identifier.Text: "System" } => true,
        _ => false,
    };

    private static bool IsQualifier(SimpleNameSyntax name)
    {
        SyntaxNode current = name;
        while (true)
        {
            switch (current.Parent)
            {
                case AliasQualifiedNameSyntax alias when alias.Alias == current:
                    return true;
                case AliasQualifiedNameSyntax alias:
                    current = alias;
                    break;
                case QualifiedNameSyntax qualified when qualified.Left == current:
                    return true;
                case QualifiedNameSyntax qualified:
                    current = qualified;
                    break;
                default:
                    return false;
            }
        }
    }
}
