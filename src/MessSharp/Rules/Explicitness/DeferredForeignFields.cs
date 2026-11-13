using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The values a field read through another receiver (<c>holder.cell</c>) can hold: the field's initializer,
/// evaluated by the initializer owner, plus every write to a member of that name in the file. A write that
/// is not a plain assignment stays opaque.
/// </summary>
internal sealed class DeferredForeignFields(CallableOwner owner, IReadOnlyList<CallableOwner> owners)
{
    public IReadOnlyList<DeferredSource>? Values(ExpressionSyntax reference)
    {
        if (reference is not MemberAccessExpressionSyntax member || member.Expression is ThisExpressionSyntax) return null;
        var type = new ExpressionTypes(owner).TypeOf(member.Expression)?.Split('<')[0].Split('.').Last();
        var classes = owner.Method.File.Classes.Where(candidate => candidate.Name == type).Take(2).ToList();
        if (classes.Count != 1 || classes[0].BaseTypes.Count > 0) return null;
        var name = member.Name.Identifier.Text;
        var declarator = Field(classes[0], name);
        if (declarator?.Initializer is null) return null;
        var sources = new List<DeferredSource> { Initializer(declarator) };
        sources.AddRange(FileWriteSites.For(owner.Method.File.Root).Select(site => Write(site, name)).OfType<DeferredSource>());
        return sources;
    }

    private static VariableDeclaratorSyntax? Field(ClassModel cls, string name) => cls.Node.ChildNodes()
        .OfType<FieldDeclarationSyntax>().Where(field => !field.Modifiers.Any(SyntaxKind.StaticKeyword))
        .SelectMany(field => field.Declaration.Variables).FirstOrDefault(variable => variable.Identifier.Text == name);

    private DeferredSource Initializer(VariableDeclaratorSyntax declarator) =>
        owners.FirstOrDefault(candidate => candidate.Method.Node == declarator && FieldInitializerOwners.IsInstance(candidate))
            is { } initializer ? new DeferredSource.Value(declarator.Initializer!.Value, initializer) : new DeferredSource.Opaque();

    private DeferredSource? Write(SyntaxNode site, string name) => site switch
    {
        AssignmentExpressionSyntax { Left: TupleExpressionSyntax tuple } =>
            DeconstructionBindings.Targets(tuple).Any(target => Named(target, name)) ? new DeferredSource.Opaque() : null,
        AssignmentExpressionSyntax assignment when Named(assignment.Left, name) => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            ? new DeferredSource.Value(assignment.Right, OwnerOf(assignment)) : new DeferredSource.Opaque(),
        ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.RefKeyword or (int)SyntaxKind.OutKeyword } argument
            when Named(argument.Expression, name) => new DeferredSource.Opaque(),
        _ => null,
    };

    private static bool Named(ExpressionSyntax target, string name) => CallableSyntax.Unwrap(target) switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text == name,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text == name,
        _ => false,
    };

    private CallableOwner? OwnerOf(SyntaxNode node) => owners
        .Where(candidate => candidate.Body is { } scope && scope.SyntaxTree == node.SyntaxTree && scope.Span.Contains(node.Span))
        .MinBy(candidate => candidate.Body!.Span.Length);
}
