using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MessSharp.Rule;

/// <summary>Finds the member declaration a bare line report belongs to.</summary>
internal static class DeclarationLocator
{
    /// <summary>
    /// The innermost member declaration within <paramref name="scope"/> that covers the
    /// line, so a bare line report is suppressed by the member that contains it.
    /// When sibling members share the line the first one wins, so a suppression
    /// on a multi-declaration line still covers every name reported there.
    /// </summary>
    internal static SyntaxNode Find(SyntaxNode scope, int line)
    {
        var lines = scope.SyntaxTree.GetText().Lines;
        if (line < 1 || line > lines.Count) return scope;

        var lineSpan = lines[line - 1].Span;
        var best = scope;
        foreach (var node in scope.DescendantNodes(n => n.Span.OverlapsWith(lineSpan)))
        {
            if (IsCoveringMember(node, lineSpan) && (best == scope || best.Span.Contains(node.Span)))
                best = node;
        }
        return best;
    }

    private static bool IsCoveringMember(SyntaxNode node, TextSpan lineSpan) =>
        node is MemberDeclarationSyntax or AccessorDeclarationSyntax
        && node.Span.OverlapsWith(lineSpan);
}
