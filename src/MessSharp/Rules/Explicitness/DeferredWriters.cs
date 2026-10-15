using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Writes a reading body does not order: other owners writing shared or captured storage, and callables
/// nested in the body writing it at times the body does not control.
/// </summary>
internal sealed class DeferredWriters(CallableOwner owner, SyntaxNode body, IReadOnlyList<CallableOwner> owners,
    DeferredDeclarations declarations)
{
    /// <summary>Shared storage, or a local or parameter captured from an enclosing body, has writers outside this body.</summary>
    public IEnumerable<DeferredSource> Outside(SyntaxNode declaration) => WrittenElsewhere(declaration)
        ? Writers(declaration, node => !body.Span.Contains(node.Span)) : [];

    /// <summary>
    /// Writes from callables nested in this body add their values and leave the value read only partly known.
    /// </summary>
    public IEnumerable<DeferredSource> Nested(SyntaxNode declaration)
    {
        var writes = Writers(declaration, node => body.Span.Contains(node.Span) && IsNested(node)).ToList();
        return writes.Count == 0 ? [] : writes.Append(new DeferredSource.Opaque());
    }

    private bool WrittenElsewhere(SyntaxNode declaration) => DeferredDeclarations.IsShared(declaration)
        || (declaration is ParameterSyntax parameter ? !declarations.IsOwnParameter(parameter) : !body.Span.Contains(declaration.Span));

    private bool IsNested(SyntaxNode node) => node.Ancestors().TakeWhile(parent => parent != body)
        .Any(parent => parent is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);

    private IEnumerable<DeferredSource> Writers(SyntaxNode declaration, Func<SyntaxNode, bool> where) =>
        FileWriteSites.For(owner.Method.File.Root)
            .Where(node => where(node) && declarations.IsWrittenBy(node, declaration))
            .Select(Written);

    private DeferredSource Written(SyntaxNode node) => node is AssignmentExpressionSyntax
    {
        RawKind: (int)SyntaxKind.SimpleAssignmentExpression or (int)SyntaxKind.CoalesceAssignmentExpression
    } assignment
        ? new DeferredSource.Value(assignment.Right, OwnerOf(assignment))
        : new DeferredSource.Opaque();

    private CallableOwner? OwnerOf(SyntaxNode node) => owners
        .Where(candidate => candidate.Body is { } scope && scope.SyntaxTree == node.SyntaxTree && scope.Span.Contains(node.Span))
        .MinBy(candidate => candidate.Body!.Span.Length);
}
