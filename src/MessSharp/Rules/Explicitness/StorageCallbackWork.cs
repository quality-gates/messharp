using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageCallbackWork(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public IReadOnlyList<StorageDeferredWork> From(InvocationExpressionSyntax call, StorageOrigin element) =>
        call.ArgumentList.Arguments.Select(argument => Create(argument.Expression, call, element))
            .OfType<StorageDeferredWork>().ToList();

    public StorageDeferredWork? Create(ExpressionSyntax actual, ExpressionSyntax site, StorageOrigin element)
    {
        var callable = new StorageCallables(context, origin, state).Find(actual);
        if (callable is null) return null;
        var parameterCount = callable.Target.Parent is null ? callable.Target.Method.Parameters.Count
            : CallableOwner.Parameters(callable.Target.Method.Node).Count();
        var elementName = "__mess_element_" + site.SpanStart;
        var arguments = Enumerable.Repeat(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(elementName)), parameterCount);
        var call = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("__mess_callback"),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
        StorageOrigin Bound(ExpressionSyntax expression) => expression is IdentifierNameSyntax name
            && name.Identifier.Text == elementName ? element : origin(expression);
        var frame = new StorageCallFrame(callable.Target, state.Bindings.GetValueOrDefault(StorageContext.ThisBinding), callable);
        var initial = StorageCallBindings.Bind(context, frame, call, state, Bound);
        var parameters = callable.Target.Method.Parameters.Take(parameterCount).Select(parameter => parameter.Name).ToList();
        return new StorageDeferredWork(callable.Target, initial, parameters);
    }
}
