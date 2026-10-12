using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed record CallableOwner(MethodModel Method, SyntaxNode? Body, CallableOwner? Parent = null)
{
    public List<CallableOwner> Children { get; } = [];

    public static List<CallableOwner> Gather(SourceFile file)
    {
        var owners = file.AllMethods.Select(m => new CallableOwner(m, m.EffectiveBody)).ToList();
        foreach (var owner in owners.ToList()) AddChildren(owner, owners);
        var globals = file.Root.DescendantNodes().OfType<GlobalStatementSyntax>().ToList();
        if (globals.Count == 0) return owners;
        var top = new CallableOwner(new MethodModel { Name = "<top-level>", Node = file.Root, File = file }, file.Root);
        owners.Add(top);
        AddChildren(top, owners);
        return owners;
    }

    private static void AddChildren(CallableOwner parent, List<CallableOwner> owners)
    {
        if (parent.Body is null) return;
        foreach (var node in BodyWalk.Nodes(parent.Body))
        {
            var body = ExecutableBody(node);
            if (body is null) continue;
            var parameters = Parameters(node).Select(p => new ParameterModel
            {
                Name = p.Identifier.Text,
                Type = p.Type?.ToString() ?? "",
                Node = p,
            }).ToList();
            parameters.AddRange(parent.Method.Parameters.Where(p => parameters.All(local => local.Name != p.Name)));
            AddCaptures(parent, body, parameters);
            var method = new MethodModel
            {
                Name = node is LocalFunctionStatementSyntax local ? local.Identifier.Text : "<lambda>",
                Node = node,
                Body = body as BlockSyntax,
                File = parent.Method.File,
                Class = parent.Method.Class,
                Parameters = parameters,
            };
            var child = new CallableOwner(method, body, parent);
            parent.Children.Add(child);
            owners.Add(child);
            AddChildren(child, owners);
        }
    }

    private static void AddCaptures(CallableOwner parent, SyntaxNode body, List<ParameterModel> parameters)
    {
        foreach (var name in BodyWalk.Nodes(body).OfType<IdentifierNameSyntax>())
        {
            var binding = ExpressionTypes.Binding(name);
            if (binding is null || body.Span.Contains(binding.Span)
                || parameters.Any(p => p.Name == name.Identifier.Text)) continue;
            parameters.Add(new ParameterModel
            {
                Name = name.Identifier.Text,
                Type = new ExpressionTypes(parent).TypeOf(name) ?? "",
            });
        }
    }

    private static SyntaxNode? ExecutableBody(SyntaxNode node) => node switch
    {
        LocalFunctionStatementSyntax local => (SyntaxNode?)local.Body ?? local.ExpressionBody?.Expression,
        LambdaExpressionSyntax lambda => lambda.Body,
        AnonymousMethodExpressionSyntax anonymous => anonymous.Block,
        _ => null,
    };

    internal static IEnumerable<ParameterSyntax> Parameters(SyntaxNode node) => node switch
    {
        LocalFunctionStatementSyntax local => local.ParameterList.Parameters,
        ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.Parameters,
        SimpleLambdaExpressionSyntax lambda => [lambda.Parameter],
        AnonymousMethodExpressionSyntax anonymous => anonymous.ParameterList?.Parameters ?? [],
        _ => [],
    };
}
