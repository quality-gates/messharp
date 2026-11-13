using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>The syntax shape that mutates a target.</summary>
public enum MutationForm
{
    /// <summary>A simple assignment or deconstruction: <c>x = v</c>, <c>(x, y) = v</c>.</summary>
    Assignment,
    /// <summary>A compound assignment: <c>x += v</c>.</summary>
    CompoundAssignment,
    /// <summary>A member assignment inside an object initializer: <c>new T { X = v }</c>.</summary>
    InitializerAssignment,
    /// <summary>A prefix or postfix <c>++</c> or <c>--</c>.</summary>
    IncrementOrDecrement,
    /// <summary>An <c>out</c> argument: the callee assigns it and never sees the old value.</summary>
    OutArgument,
    /// <summary>A <c>ref</c> argument: the callee may read and write it.</summary>
    RefArgument,
}

/// <summary>One target a node mutates. A pure write replaces the value without reading the previous one.</summary>
public readonly record struct Mutation(ExpressionSyntax Target, MutationForm Form)
{
    public bool IsPureWrite => Form is MutationForm.Assignment or MutationForm.InitializerAssignment or MutationForm.OutArgument;
}

/// <summary>
/// Syntax-only classification of the C# forms that write a variable or member: assignments, deconstruction,
/// increments and decrements, and <c>out</c>/<c>ref</c> arguments. Rules decide which forms matter to them.
/// </summary>
public static class MutationClassifier
{
    /// <summary>The targets <paramref name="node"/> mutates itself, or none when it is not a mutating form.</summary>
    public static IEnumerable<Mutation> Classify(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax assignment => FlattenTargets(assignment.Left).Select(target => new Mutation(target, Form(assignment))),
        PrefixUnaryExpressionSyntax prefix when IsIncrementOrDecrement(prefix.Kind()) =>
            [new Mutation(prefix.Operand, MutationForm.IncrementOrDecrement)],
        PostfixUnaryExpressionSyntax postfix when IsIncrementOrDecrement(postfix.Kind()) =>
            [new Mutation(postfix.Operand, MutationForm.IncrementOrDecrement)],
        ArgumentSyntax { Expression: not DeclarationExpressionSyntax } argument when argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) =>
            [new Mutation(argument.Expression, MutationForm.OutArgument)],
        ArgumentSyntax { Expression: not DeclarationExpressionSyntax } argument when argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) =>
            [new Mutation(argument.Expression, MutationForm.RefArgument)],
        _ => [],
    };

    /// <summary>The targets of an assignment's left side, unrolling nested tuple deconstruction.</summary>
    public static IEnumerable<ExpressionSyntax> FlattenTargets(ExpressionSyntax left) =>
        left is TupleExpressionSyntax tuple ? tuple.Arguments.SelectMany(argument => FlattenTargets(argument.Expression)) : [left];

    /// <summary>Every name written by a bare-identifier target anywhere in <paramref name="body"/>.</summary>
    public static HashSet<string> CollectWrittenNames(SyntaxNode body, bool pureWritesOnly = false) =>
        Identifiers(body, pureWritesOnly).Select(identifier => identifier.Identifier.Text).ToHashSet(StringComparer.Ordinal);

    /// <summary>The identifier nodes that are the target of a mutation in <paramref name="body"/>.</summary>
    public static IEnumerable<IdentifierNameSyntax> Identifiers(SyntaxNode body, bool pureWritesOnly = false) =>
        body.DescendantNodesAndSelf().SelectMany(Classify)
            .Where(mutation => !pureWritesOnly || mutation.IsPureWrite)
            .Select(mutation => mutation.Target).OfType<IdentifierNameSyntax>();

    /// <summary>Whether <paramref name="target"/> is itself written by an enclosing pure-write form, outside object initializers.</summary>
    public static bool IsPureWriteTarget(SyntaxNode target) => Owner(target) is { } owner
        && Classify(owner).Any(mutation => mutation.IsPureWrite && mutation.Form != MutationForm.InitializerAssignment
            && ReferenceEquals(mutation.Target, target));

    /// <summary>The assignment or argument whose form would mutate <paramref name="target"/>.</summary>
    private static SyntaxNode? Owner(SyntaxNode target)
    {
        var top = target;
        while (top.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax tuple })
            top = tuple;
        return top.Parent;
    }

    private static MutationForm Form(AssignmentExpressionSyntax assignment) =>
        !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ? MutationForm.CompoundAssignment
        : assignment.Parent is InitializerExpressionSyntax ? MutationForm.InitializerAssignment : MutationForm.Assignment;

    private static bool IsIncrementOrDecrement(SyntaxKind kind) =>
        kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
            or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression;
}
