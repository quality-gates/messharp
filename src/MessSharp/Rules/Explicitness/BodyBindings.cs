using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class BodyBindings
{
    private static readonly IReadOnlySet<SyntaxKind> ConditionalKinds = new HashSet<SyntaxKind>
    {
        SyntaxKind.IfStatement, SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement,
        SyntaxKind.WhileStatement, SyntaxKind.DoStatement, SyntaxKind.SwitchStatement, SyntaxKind.TryStatement,
        SyntaxKind.CatchClause, SyntaxKind.ConditionalExpression, SyntaxKind.ConditionalAccessExpression,
        SyntaxKind.SwitchExpression, SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression, SyntaxKind.CoalesceExpression,
    };
    private readonly SyntaxNode _body;
    private readonly bool _jumps;

    public BodyBindings(SyntaxNode body)
    {
        _body = body;
        var nodes = BodyWalk.Nodes(body).ToList();
        _jumps = nodes.Any(node => node is GotoStatementSyntax or LabeledStatementSyntax);
        All = nodes.SelectMany(StorageBindings.Find).OrderBy(binding => binding.Position).ToList();
    }

    public IReadOnlyList<StorageBinding> All { get; }

    public bool Reaches(StorageBinding binding, SyntaxNode site)
    {
        if (!InScope(binding, site)) return false;
        return binding.Position <= site.SpanStart
            || RepetitionRegion(binding.Node) is { } region && region.Span.Contains(site.Span);
    }

    public bool Reaches(StorageStep step, SyntaxNode site) => step is StorageStep.Bound bound
        ? Reaches(bound.Binding, site)
        : step.Position <= site.SpanStart
            || RepetitionRegion(step.Node) is { } region && region.Span.Contains(site.Span);

    public SyntaxNode? RepetitionRegion(SyntaxNode node)
    {
        if (_jumps) return _body;
        return node.Ancestors().TakeWhile(parent => parent != _body).LastOrDefault(parent => parent is
            ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax
            or WhileStatementSyntax or DoStatementSyntax);
    }

    public bool MayBeSkipped(StorageBinding binding) =>
        binding.Joins || MayBeSkipped(binding.Node);

    public bool MayBeSkipped(SyntaxNode node) => node.Ancestors().TakeWhile(parent => parent != _body).Any(IsConditional);

    private static bool IsConditional(SyntaxNode node) => ConditionalKinds.Contains(node.Kind());

    private static bool InScope(StorageBinding binding, SyntaxNode site)
    {
        if (binding.Scope is { } scope) return scope.Span.Contains(site.Span);
        if (binding.Node is AssignmentExpressionSyntax or ArgumentSyntax) return true;
        return binding.Node.Ancestors().OfType<BlockSyntax>().FirstOrDefault() is not { } block
            || block.Span.Contains(site.Span);
    }

}
