using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Flow-sensitive values of one declaration within one body: the sources that reach a read, and the
/// sources a parameter can hold when the body returns.
/// </summary>
internal sealed class DeclarationFlow(SyntaxNode body, DeferredDeclarations declarations)
{
    private readonly BodyBindings _bindings = new(body);

    public List<DeferredSource> Reaching(SyntaxNode declaration, SyntaxNode reference)
    {
        var killed = Replacement(declaration, reference);
        var values = killed is null ? DeferredBindingSources.Initial(declaration).ToList() : [];
        foreach (var binding in Writing(declaration).Where(b => _bindings.Reaches(b, reference) && Survives(b, killed, reference)))
        {
            RemoveReplaced(values, binding);
            values.AddRange(DeferredBindingSources.Stored(binding));
        }
        return values;
    }

    private SyntaxNode? Replacement(SyntaxNode declaration, SyntaxNode reference) =>
        _bindings.HasJumps || declaration is not (VariableDeclaratorSyntax or ParameterSyntax) ? null
            : new DefiniteAssignment(node => declarations.IsWrittenBy(node, declaration)).LatestBefore(body, reference);

    private static bool Survives(StorageBinding binding, SyntaxNode? killed, SyntaxNode reference) =>
        killed is null || (binding.Position >= killed.SpanStart && binding.Position < reference.SpanStart);

    /// <summary>Every value a callee can leave in <paramref name="parameter"/> when it returns.</summary>
    public IReadOnlyList<DeferredSource> Exit(ParameterSyntax parameter)
    {
        List<DeferredSource> values = parameter.Modifiers.Any(SyntaxKind.OutKeyword)
            ? [] : [.. DeferredBindingSources.Initial(parameter)];
        foreach (var binding in Writing(parameter))
        {
            RemoveReplaced(values, binding);
            values.AddRange(DeferredBindingSources.Stored(binding));
        }
        return values;
    }

    /// <summary>A <c>ref</c> argument is skipped unless the callee it reaches always overwrites it.</summary>
    private bool MayBeSkipped(StorageBinding binding) =>
        binding.Joins && declarations.Overwrites(binding.Node)
            ? _bindings.MayBeSkipped(binding.Node) : _bindings.MayBeSkipped(binding);

    private void RemoveReplaced(List<DeferredSource> values, StorageBinding binding)
    {
        if (!MayBeSkipped(binding))
        {
            values.Clear();
            return;
        }
        if (binding.Node.IsKind(SyntaxKind.CoalesceAssignmentExpression) && !_bindings.MayBeSkipped(binding.Node))
            values.RemoveAll(value => value is DeferredSource.Value { Expression: var expression }
                && CallableSyntax.Unwrap(expression).IsKind(SyntaxKind.NullLiteralExpression));
    }

    /// <summary>The body's bindings of <paramref name="declaration"/>, including assignments through a member access.</summary>
    private IEnumerable<StorageBinding> Writing(SyntaxNode declaration)
    {
        var name = DeferredDeclarations.Name(declaration);
        return _bindings.All.Where(b => b.Name == name && declarations.IsWrittenBy(b.Node, declaration))
            .Concat(MemberWrites(name, declaration)).OrderBy(b => b.Position);
    }

    private IEnumerable<StorageBinding> MemberWrites(string name, SyntaxNode declaration) => BodyWalk.Nodes(body)
        .OfType<AssignmentExpressionSyntax>()
        .Where(a => a.Left is MemberAccessExpressionSyntax && Replaces(a) && declarations.IsWrittenBy(a, declaration))
        .Select(a => new StorageBinding(name, a, a.Span.End, new BindingSource.Value(a.Right),
            Joins: a.IsKind(SyntaxKind.CoalesceAssignmentExpression)));

    private static bool Replaces(AssignmentExpressionSyntax assignment) =>
        assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression);
}
