using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// A deconstructing assignment binds each target to the matching element of a tuple literal on its right. The right
/// side is evaluated before any target is written, so a target whose element is not spelled out, or reads a target
/// written before it, holds an unknown value.
/// </summary>
internal static class DeconstructionBindings
{
    public static IEnumerable<StorageBinding> Assigned(AssignmentExpressionSyntax assignment, TupleExpressionSyntax tuple) =>
        Targets(tuple).Select(target =>
            new StorageBinding(target.Identifier.Text, assignment, assignment.Span.End, Source(assignment, target)));

    public static IEnumerable<IdentifierNameSyntax> Targets(TupleExpressionSyntax tuple) =>
        tuple.Arguments.SelectMany(argument => argument.Expression switch
        {
            TupleExpressionSyntax nested => Targets(nested),
            IdentifierNameSyntax { Identifier.Text: not "_" } name => [name],
            _ => [],
        });

    /// <summary>The deconstructing assignment whose left side declares or names <paramref name="target"/>.</summary>
    public static AssignmentExpressionSyntax? Deconstructing(SyntaxNode target) => target.Ancestors()
        .TakeWhile(node => node is ArgumentSyntax or TupleExpressionSyntax or DeclarationExpressionSyntax
            or VariableDesignationSyntax or AssignmentExpressionSyntax)
        .OfType<AssignmentExpressionSyntax>().FirstOrDefault(assignment => assignment.Left.Span.Contains(target.Span));

    public static BindingSource Source(AssignmentExpressionSyntax assignment, SyntaxNode target)
    {
        var value = Element(assignment.Right, Path(target, assignment.Left));
        return value is null || ReadsEarlierTarget(value, assignment.Left, target)
            ? new BindingSource.Unknown() : new BindingSource.Value(value);
    }

    private static List<int> Path(SyntaxNode target, SyntaxNode left)
    {
        var path = new List<int>();
        for (var node = target; node != left && node.Parent is { } parent; node = parent)
        {
            if (parent is TupleExpressionSyntax tuple && node is ArgumentSyntax argument)
                path.Insert(0, tuple.Arguments.IndexOf(argument));
            else if (parent is ParenthesizedVariableDesignationSyntax group && node is VariableDesignationSyntax variable)
                path.Insert(0, group.Variables.IndexOf(variable));
        }
        return path;
    }

    private static ExpressionSyntax? Element(ExpressionSyntax value, IEnumerable<int> path)
    {
        ExpressionSyntax? element = value;
        foreach (var index in path)
            element = element is not null && CallableSyntax.Unwrap(element) is TupleExpressionSyntax tuple
                ? tuple.Arguments.ElementAtOrDefault(index)?.Expression : null;
        return element;
    }

    private static bool ReadsEarlierTarget(ExpressionSyntax value, SyntaxNode left, SyntaxNode target)
    {
        var earlier = left.DescendantNodes()
            .Where(node => node.SpanStart < target.SpanStart && node is IdentifierNameSyntax { Parent: ArgumentSyntax }
                or SingleVariableDesignationSyntax)
            .Select(Name).ToHashSet(StringComparer.Ordinal);
        return value.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(name => earlier.Contains(name.Identifier.Text));
    }

    private static string Name(SyntaxNode target) => target switch
    {
        SingleVariableDesignationSyntax designation => designation.Identifier.Text,
        _ => ((IdentifierNameSyntax)target).Identifier.Text,
    };
}
