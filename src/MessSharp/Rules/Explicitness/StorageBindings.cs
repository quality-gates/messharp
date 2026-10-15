using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal abstract record BindingSource
{
    internal sealed record Value(ExpressionSyntax Expression) : BindingSource;
    internal sealed record Element(ExpressionSyntax Expression) : BindingSource;
    internal sealed record Unknown : BindingSource;
    internal sealed record Private : BindingSource;
}

internal sealed record StorageBinding(
    string Name, SyntaxNode Node, int Position, BindingSource Source,
    SyntaxNode? Scope = null, bool Joins = false);

internal static class StorageBindings
{
    public static IEnumerable<StorageBinding> Find(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax declarator => [Declared(declarator)],
        AssignmentExpressionSyntax assignment => Assigned(assignment),
        ForEachStatementSyntax loop =>
            [new(loop.Identifier.Text, loop, loop.Expression.Span.End, new BindingSource.Element(loop.Expression), loop)],
        SingleVariableDesignationSyntax designation => [Designated(designation)],
        ArgumentSyntax { Expression: IdentifierNameSyntax name } argument when !argument.RefKindKeyword.IsKind(SyntaxKind.None) =>
            ArgumentBindings.Find(argument, name),
        CatchDeclarationSyntax { Identifier.Text: { Length: > 0 } name } declaration =>
            [new(name, declaration, declaration.Span.End, new BindingSource.Unknown(), Scope: declaration.Parent)],
        _ => QueryBindings.Find(node),
    };

    private static StorageBinding Declared(VariableDeclaratorSyntax declarator) => declarator.Initializer is { } initializer
        ? new(declarator.Identifier.Text, declarator, declarator.Span.End, new BindingSource.Value(initializer.Value))
        : new(declarator.Identifier.Text, declarator, declarator.Span.End, new BindingSource.Private());

    private static IEnumerable<StorageBinding> Assigned(AssignmentExpressionSyntax assignment) => assignment switch
    {
        { Parent: InitializerExpressionSyntax } => [],
        { Left: IdentifierNameSyntax target } when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) =>
            [new(target.Identifier.Text, assignment, assignment.Span.End, new BindingSource.Value(assignment.Right))],
        { Left: IdentifierNameSyntax target } when assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression) =>
            [new(target.Identifier.Text, assignment, assignment.Span.End, new BindingSource.Value(assignment.Right), Joins: true)],
        { Left: TupleExpressionSyntax tuple } => DeconstructionBindings.Assigned(assignment, tuple),
        _ => [],
    };

    private static StorageBinding Designated(SingleVariableDesignationSyntax designation)
    {
        var name = designation.Identifier.Text;
        if (designation.Ancestors().OfType<ForEachVariableStatementSyntax>().FirstOrDefault() is { } loop
            && loop.Variable.Span.Contains(designation.Span))
            return new(name, designation, loop.Expression.Span.End, new BindingSource.Element(loop.Expression), loop);
        return designation.Parent switch
        {
            DeclarationPatternSyntax { Parent: IsPatternExpressionSyntax tested } =>
                new(name, designation, tested.Span.End, new BindingSource.Value(tested.Expression)),
            DeclarationExpressionSyntax { Parent: ArgumentSyntax argument } when ArgumentBindings.TriedValueSource(argument) is { } source =>
                new(name, designation, argument.Span.End, new BindingSource.Element(source)),
            _ when DeconstructionBindings.Deconstructing(designation) is { } assignment =>
                new(name, designation, assignment.Span.End, DeconstructionBindings.Source(assignment, designation)),
            _ => new(name, designation, designation.Span.End, new BindingSource.Unknown()),
        };
    }
}

internal static class ArgumentBindings
{
    public static ExpressionSyntax? TriedValueSource(ArgumentSyntax argument) =>
        argument is { Parent: ArgumentListSyntax { Arguments.Count: 2 } list }
        && list.Arguments[1] == argument
        && list.Parent is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "TryGetValue" } member
        }
            ? member.Expression : null;

    public static IEnumerable<StorageBinding> Find(ArgumentSyntax argument, IdentifierNameSyntax name)
    {
        if (argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword)) return [];
        if (TriedValueSource(argument) is { } source)
            return [new(name.Identifier.Text, argument, argument.Span.End, new BindingSource.Element(source))];
        var joins = argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword);
        return [new(name.Identifier.Text, argument, argument.Span.End, new BindingSource.Unknown(), Joins: joins)];
    }
}

internal static class QueryBindings
{
    public static IEnumerable<StorageBinding> Find(SyntaxNode node) => node switch
    {
        FromClauseSyntax from =>
            [new(from.Identifier.Text, from, from.Expression.Span.End, new BindingSource.Element(from.Expression), Query(from))],
        LetClauseSyntax let => [Untracked(let.Identifier, let)],
        JoinClauseSyntax join => [Untracked(join.Identifier, join)],
        JoinIntoClauseSyntax into => [Untracked(into.Identifier, into)],
        QueryContinuationSyntax continuation => [Untracked(continuation.Identifier, continuation)],
        _ => [],
    };

    private static StorageBinding Untracked(SyntaxToken identifier, SyntaxNode node) =>
        new(identifier.Text, node, node.SpanStart, new BindingSource.Unknown(), Scope: Query(node));

    private static SyntaxNode? Query(SyntaxNode node) => node.Ancestors().OfType<QueryExpressionSyntax>().LastOrDefault();
}
