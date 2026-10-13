using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class DeferredValueContext(
    RuleContext ctx, IReadOnlyList<CallableOwner> owners, CallableOwner owner, SyntaxNode body,
    IReadOnlyDictionary<string, StorageOrigin> bindings)
{
    public DeferredValueContext(RuleContext ctx, IReadOnlyList<CallableOwner> owners, CallableOwner owner, SyntaxNode body)
        : this(ctx, owners, owner, body, new Dictionary<string, StorageOrigin>()) { }

    public CallableOwner Owner { get; } = owner;
    public IReadOnlyDictionary<string, StorageOrigin> Bindings { get; } = bindings;

    public DeferredValueContext Bind(CallableOwner target, InvocationExpressionSyntax call, SyntaxNode targetBody)
    {
        var bound = target.Method.Parameters.Select((parameter, index) =>
            (parameter.Name, Value: CallArguments.Actual(call, parameter.Name, index)))
            .ToDictionary(pair => pair.Name, pair => pair.Value is null ? StorageOrigin.Unknown : Storage(pair.Value));
        return new DeferredValueContext(ctx, owners, target, targetBody, bound);
    }

    public Dictionary<string, StorageOrigin> Captures(CallableOwner target) => target.Method.Parameters
        .Skip(CallableOwner.Parameters(target.Method.Node).Count()).ToDictionary(parameter => parameter.Name,
            parameter => Bindings.GetValueOrDefault(parameter.Name) ?? Storage(SyntaxFactory.IdentifierName(parameter.Name)));

    public StorageOrigin Storage(ExpressionSyntax value)
    {
        var origin = CallArguments.Ownership(ctx, Owner, owners, body).StorageAt(value, value);
        return StorageProjection.Apply(origin, root => Bindings.GetValueOrDefault(root) ?? StorageOrigin.Shared(root));
    }
}
