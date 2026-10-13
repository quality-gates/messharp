using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ReturnedCaptureBindings
{
    public static IReadOnlyDictionary<string, StorageOrigin>? For(
        RuleContext ctx, IReadOnlyList<CallableOwner> owners, CallEdge edge)
    {
        if (edge.Target.Parent is not { Body: { } factoryBody } factoryOwner || factoryOwner == edge.Caller
            || edge.Caller.Body is not { } callerBody) return null;
        var factoryCall = Factory(edge.Call.Expression);
        if (factoryCall is null || new CallResolver(owners).Resolve(edge.Caller, factoryCall) != factoryOwner) return null;
        var callerStorage = CallArguments.Ownership(ctx, edge.Caller, owners, callerBody);
        var factoryStorage = CallArguments.Ownership(ctx, factoryOwner, owners, factoryBody);
        return Captures(edge.Target, factoryOwner, factoryCall, callerStorage, factoryStorage, factoryBody);
    }

    private static IReadOnlyDictionary<string, StorageOrigin> Captures(CallableOwner target, CallableOwner factoryOwner,
        InvocationExpressionSyntax factoryCall, ArgumentOwnership callerStorage, ArgumentOwnership factoryStorage,
        SyntaxNode body)
    {
        var site = BodyWalk.Nodes(body).Last();
        var result = new Dictionary<string, StorageOrigin>(StringComparer.Ordinal);
        foreach (var parameter in target.Method.Parameters.Skip(CallableOwner.Parameters(target.Method.Node).Count()))
        {
            var relative = factoryStorage.StorageAt(SyntaxFactory.IdentifierName(parameter.Name), site);
            result[parameter.Name] = relative.Outer.Count == 0 && relative.Reachable.Count == 0
                ? StorageOrigin.Shared(StorageRoots.Capture(parameter.Name, factoryOwner.Method.Node.SpanStart))
                : StorageProjection.Apply(relative, Actual);
        }
        return result;

        StorageOrigin Actual(string root)
        {
            if (StorageRoots.IsExternal(root)) return StorageOrigin.Shared(root);
            var position = factoryOwner.Method.Parameters.FindIndex(p => p.Name == root);
            return CallArguments.Actual(factoryCall, root, position) is { } actual
                ? callerStorage.StorageAt(actual, factoryCall) : StorageOrigin.Unknown;
        }
    }

    private static InvocationExpressionSyntax? Factory(ExpressionSyntax value) => value switch
    {
        InvocationExpressionSyntax call => call,
        IdentifierNameSyntax name when ExpressionTypes.Binding(name)?.Initializer is { } initializer =>
            Factory(initializer.Value),
        _ => null,
    };
}
