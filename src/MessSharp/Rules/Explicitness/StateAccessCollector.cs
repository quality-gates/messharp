using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>How a method body touches a named piece of state.</summary>
internal enum AccessKind
{
    /// <summary>The value flows into the method.</summary>
    Read,

    /// <summary>The state itself gets a new value.</summary>
    Write,

    /// <summary>The object that the state holds is changed.</summary>
    Change,
}

internal readonly record struct StateAccess(string Name, AccessKind Kind, SyntaxNode Site);

/// <summary>
/// Finds the reads, writes and changes of named state in a method body.
/// The resolver gives the state name that an expression refers to, or null.
/// </summary>
internal sealed class StateAccessCollector(Func<ExpressionSyntax, string?> resolve) : IBodyVisitor
{
    private readonly List<StateAccess> _mutations = [];
    private readonly List<StateAccess> _reads = [];
    private readonly HashSet<SyntaxNode> _pureWrites = [];

    /// <summary>The writes and changes in source order, then the reads in source order.</summary>
    public List<StateAccess> Accesses() => [.. _mutations, .. _reads];

    /// <summary>
    /// Records what one node does to the state. A node comes before its
    /// children, so a pure write is known before its target is seen as a read.
    /// </summary>
    public void Visit(SyntaxNode node)
    {
        foreach (var (target, isPureWrite) in MutationTargets(node))
            AddMutation(target, isPureWrite);
        if (DiscardedCallReceiver(node) is { } receiver && ResolveChain(receiver) is { } changed)
            _mutations.Add(new StateAccess(changed, AccessKind.Change, node));
        if (node is ExpressionSyntax expr && !_pureWrites.Contains(expr) && resolve(expr) is { } name)
            _reads.Add(new StateAccess(name, AccessKind.Read, expr));
    }

    private void AddMutation(ExpressionSyntax target, bool isPureWrite)
    {
        if (resolve(target) is { } written)
        {
            _mutations.Add(new StateAccess(written, AccessKind.Write, target));
            if (isPureWrite) _pureWrites.Add(target);
            return;
        }

        if (ReceiverOf(target) is { } receiver && ResolveChain(receiver) is { } changed)
            _mutations.Add(new StateAccess(changed, AccessKind.Change, target));
    }

    /// <summary>
    /// Expressions that a node assigns to. A pure write replaces the value
    /// without reading it first (simple assignment, out argument).
    /// </summary>
    private static IEnumerable<(ExpressionSyntax Target, bool IsPureWrite)> MutationTargets(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } => [],
        AssignmentExpressionSyntax a => AssignedExpressions(a.Left)
            .Select(t => (t, a.IsKind(SyntaxKind.SimpleAssignmentExpression))),
        PrefixUnaryExpressionSyntax p when IsIncrementOrDecrement(p.Kind()) => [(p.Operand, false)],
        PostfixUnaryExpressionSyntax p when IsIncrementOrDecrement(p.Kind()) => [(p.Operand, false)],
        ArgumentSyntax arg when arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) => [(arg.Expression, true)],
        ArgumentSyntax arg when arg.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) => [(arg.Expression, false)],
        _ => [],
    };

    /// <summary>The targets of an assignment, with nested tuple deconstruction flattened.</summary>
    private static IEnumerable<ExpressionSyntax> AssignedExpressions(ExpressionSyntax left) =>
        left is TupleExpressionSyntax tuple ? tuple.Arguments.SelectMany(a => AssignedExpressions(a.Expression)) : [left];

    private static bool IsIncrementOrDecrement(SyntaxKind kind) =>
        kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
             or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression;

    /// <summary>
    /// The receiver of a call whose result is not used. Such a call is made
    /// for its effect, so it changes the object it is called on.
    /// </summary>
    private static ExpressionSyntax? DiscardedCallReceiver(SyntaxNode node)
    {
        if (DiscardedExpression(node) is not { } discarded) return null;
        var expr = discarded is AwaitExpressionSyntax awaited ? awaited.Expression : discarded;
        return expr switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } => ma.Expression,
            ConditionalAccessExpressionSyntax { WhenNotNull: InvocationExpressionSyntax } ca => ca.Expression,
            _ => null,
        };
    }

    /// <summary>An expression statement, or the expression body of a member that returns nothing.</summary>
    private static ExpressionSyntax? DiscardedExpression(SyntaxNode node) => node switch
    {
        ExpressionStatementSyntax statement => statement.Expression,
        ExpressionSyntax { Parent: ArrowExpressionClauseSyntax arrow } body when ExpressionBodies.DiscardsValue(arrow) => body,
        _ => null,
    };

    /// <summary>
    /// Walks from an expression down through member and element access to
    /// the first part that names state, e.g. <c>items</c> in <c>items[0].Name</c>.
    /// </summary>
    private string? ResolveChain(ExpressionSyntax start)
    {
        for (ExpressionSyntax? expr = start; expr != null; expr = ReceiverOf(expr))
        {
            if (resolve(expr) is { } name) return name;
        }
        return null;
    }

    private static ExpressionSyntax? ReceiverOf(ExpressionSyntax expr) => expr switch
    {
        MemberAccessExpressionSyntax ma => ma.Expression,
        ElementAccessExpressionSyntax ea => ea.Expression,
        ConditionalAccessExpressionSyntax ca => ca.Expression,
        ParenthesizedExpressionSyntax p => p.Expression,
        _ => null,
    };
}
