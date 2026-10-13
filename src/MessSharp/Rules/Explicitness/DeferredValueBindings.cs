using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class DeferredValueBindings(CallableOwner owner, SyntaxNode body)
{
    public IEnumerable<ExpressionSyntax> Values(ExpressionSyntax reference)
    {
        var declaration = Declaration(reference);
        if (declaration is null) return [];
        var values = new List<ExpressionSyntax>();
        if (declaration.Initializer is { } initializer) values.Add(initializer.Value);
        var bindings = new BodyBindings(body);
        foreach (var binding in Bindings(declaration, bindings).Where(b => bindings.Reaches(b, reference)))
        {
            if (!Matches(binding.Node, declaration)) continue;
            if (!bindings.MayBeSkipped(binding)) values.Clear();
            if (binding.Source is BindingSource.Value value) values.Add(value.Expression);
        }
        return values;
    }

    private IEnumerable<StorageBinding> Bindings(VariableDeclaratorSyntax declaration, BodyBindings bindings) =>
        bindings.All.Where(b => b.Name == declaration.Identifier.Text).Concat(BodyWalk.Nodes(body)
            .OfType<AssignmentExpressionSyntax>().Where(a => a.Left is MemberAccessExpressionSyntax && Matches(a, declaration)
                && (a.IsKind(SyntaxKind.SimpleAssignmentExpression) || a.IsKind(SyntaxKind.CoalesceAssignmentExpression)))
            .Select(a => new StorageBinding(declaration.Identifier.Text, a, a.Span.End, new BindingSource.Value(a.Right),
                Joins: a.IsKind(SyntaxKind.CoalesceAssignmentExpression)))).OrderBy(b => b.Position);

    private VariableDeclaratorSyntax? Declaration(ExpressionSyntax reference)
    {
        if (reference is IdentifierNameSyntax name && ExpressionTypes.Binding(name) is { } local) return local;
        var fieldName = reference switch
        {
            IdentifierNameSyntax field => field.Identifier.Text,
            MemberAccessExpressionSyntax member when member.Expression.ToString() == owner.Method.Class?.Name =>
                member.Name.Identifier.Text,
            _ => null,
        };
        return owner.Method.Class?.Fields.FirstOrDefault(f => f.IsStatic && f.Name == fieldName)?.Node
            is FieldDeclarationSyntax fieldDeclaration
            ? fieldDeclaration.Declaration.Variables.FirstOrDefault(v => v.Identifier.Text == fieldName) : null;
    }

    private bool Matches(SyntaxNode node, VariableDeclaratorSyntax declaration) => node switch
    {
        VariableDeclaratorSyntax variable => variable == declaration,
        AssignmentExpressionSyntax assignment => Declaration(assignment.Left) == declaration,
        _ => false,
    };
}
