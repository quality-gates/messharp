using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed record ConstructorCall(CallableOwner Target, InvocationExpressionSyntax Invocation)
{
    public static ConstructorCall? Resolve(CallableOwner? owner, IReadOnlyList<CallableOwner> owners, BaseObjectCreationExpressionSyntax creation) =>
        owner is not null && new ExpressionTypes(owner).TypeOf(creation) is { } type ? Resolve(owners, creation, type) : null;

    public static ConstructorCall? Resolve(IReadOnlyList<CallableOwner> owners, BaseObjectCreationExpressionSyntax creation, string type)
    {
        var name = type.Split('<')[0].Split('.').Last();
        var arguments = creation.ArgumentList ?? SyntaxFactory.ArgumentList();
        var count = arguments.Arguments.Count;
        var targets = owners.Where(o => o.Method.IsConstructor && !o.Method.IsStaticConstructor()
            && o.Method.Class?.Name == name && o.Method.Parameters.Count == count).Take(2).ToList();
        if (targets.Count != 1) return null;
        var call = SyntaxFactory.InvocationExpression(SyntaxFactory.ParseExpression(type), arguments);
        return new ConstructorCall(targets[0], call);
    }
}
