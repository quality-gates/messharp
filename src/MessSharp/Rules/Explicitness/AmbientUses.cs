using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

internal sealed class AmbientUses(CallableOwner owner) : IBodyVisitor
{
    private readonly List<Finding> _sources = [];
    private readonly List<Finding> _randoms = [];
    private readonly List<Finding> _sinks = [];

    public List<Finding> Inputs() => [.. _sources, .. _randoms];

    public List<Finding> Outputs() => [.. _sinks];

    public void Visit(SyntaxNode node)
    {
        if (!UsesStandardReceiver(node)) return;
        if (AmbientApi.Inputs.UseAt(node) is { } source)
            _sources.Add(new Finding(node, "uses " + source));
        if (AmbientApi.Outputs.UseAt(node) is { } sink)
            _sinks.Add(new Finding(node, "uses " + sink));
        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax call
            && KnownParseOperations.ReadsCulture(call))
            _sources.Add(new Finding(node, "uses the current culture"));
        if (AmbientApi.IsUnseededRandom(node))
            _randoms.Add(new Finding(node, "uses new Random()"));
    }

    private bool UsesStandardReceiver(SyntaxNode node) =>
        node is not Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax access
        || KnownTypeChecks.StandardReceiver(owner, access.Expression);
}
