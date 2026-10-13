using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StateRootResolver
{
    private readonly StateNames _parameters;
    private readonly StateNames _statics;
    private readonly StateNames _instance;

    public StateRootResolver(RuleContext ctx, CallableOwner owner)
    {
        _parameters = StateNames.Parameters(owner.Method.Parameters.Select(p => p.Name).ToHashSet());
        var state = owner.Method.Class is { } cls ? ClassState.For(ctx, cls) : null;
        var empty = MethodEffects.NoNames;
        _statics = StateNames.StaticMembers(state?.Statics ?? empty, owner.Method.Class?.Name ?? "");
        _instance = StateNames.InstanceMembers(state?.Instance ?? empty);
    }

    public string? Resolve(ExpressionSyntax expression) => _parameters.Resolve(expression)
        ?? StaticRoot(expression) ?? InstanceRoot(expression);

    private string? StaticRoot(ExpressionSyntax expression) =>
        _statics.Resolve(expression) is { } name ? StorageRoots.Static(name) : null;

    private string? InstanceRoot(ExpressionSyntax expression) =>
        _instance.Resolve(expression) is { } name ? StorageRoots.Member(name) : null;
}
