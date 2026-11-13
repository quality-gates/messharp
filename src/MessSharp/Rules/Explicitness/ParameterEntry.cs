using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ParameterEntry(RuleContext ctx, CallEdge edge, IReadOnlyList<CallableOwner> owners)
{
    /// <summary>What the parameter holds on entry: the caller's argument, or the argument of the call that created the closure reading it.</summary>
    public DeferredResolution? Resolve(DeferredExecution execution)
    {
        if (Captured(execution) is { } captured) return captured;
        if (!edge.Target.Method.Parameters.Any(parameter => parameter.Name == execution.ParameterName)
            || CallArguments.Actual(edge, execution.ParameterName) is not { } actual
            || edge.Caller.Body is not { } body) return null;
        var values = new DeferredValueResolver(ctx, owners);
        return execution.Elements ? values.Elements(edge.Caller, actual, body, execution.Kind)
            : values.Resolve(edge.Caller, actual, body, execution.Kind);
    }

    private DeferredResolution? Captured(DeferredExecution execution) =>
        edge.Frame is { Caller: { } caller } frame && !edge.Target.OwnParameters().Contains(execution.ParameterName)
        && frame.Argument(execution.ParameterName) is { } argument
            ? Walk(caller.Context, argument, execution) : null;

    private DeferredResolution Walk(DeferredValueContext context, ExpressionSyntax argument, DeferredExecution execution) =>
        execution.Elements ? new DeferredValueWalk(owners).ResolveElements(context, argument, execution.Kind)
            : new DeferredValueWalk(owners).Resolve(context, argument, execution.Kind);
}
