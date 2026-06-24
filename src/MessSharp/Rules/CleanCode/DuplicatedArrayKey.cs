using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.CleanCode;

/// <summary>
/// Flags duplicate literal keys in dictionary/collection initializers.
/// Covers:
///   - `new Dictionary&lt;K,V&gt; { ["k"] = v }` — ImplicitElementAccessSyntax on left
///   - `new Dictionary&lt;K,V&gt; { { "k", v } }` — nested InitializerExpression
/// Port of phpmd's DuplicatedArrayKey rule.
/// </summary>
public sealed class DuplicatedArrayKeyRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        foreach (var init in method.Body.DescendantNodesAndSelf()
                     .OfType<InitializerExpressionSyntax>())
        {
            CheckInitializer(ctx, init);
        }
    }

    private static void CheckInitializer(RuleContext ctx, InitializerExpressionSyntax init)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var expr in init.Expressions)
        {
            // `{ ["key"] = value }` — AssignmentExpression with ImplicitElementAccess on left
            // (used in object/collection initializer context)
            if (expr is AssignmentExpressionSyntax assign2
                && assign2.Kind() == SyntaxKind.SimpleAssignmentExpression)
            {
                if (assign2.Left is ImplicitElementAccessSyntax implicitElem
                    && implicitElem.ArgumentList.Arguments.Count == 1)
                {
                    var keyExpr = implicitElem.ArgumentList.Arguments[0].Expression;
                    RecordKey(ctx, keyExpr, seen);
                    continue;
                }
            }

            // `{ { "key", value } }` — nested InitializerExpression (complex element)
            if (expr is InitializerExpressionSyntax nested
                && nested.Expressions.Count >= 2)
            {
                var keyExpr = nested.Expressions[0];
                RecordKey(ctx, keyExpr, seen);
                continue;
            }
        }
    }

    private static void RecordKey(RuleContext ctx, Microsoft.CodeAnalysis.SyntaxNode keyExpr,
        Dictionary<string, int> seen)
    {
        var (key, display) = NormalizeKey(keyExpr);
        if (key == null) return;

        var line = keyExpr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        if (seen.TryGetValue(key, out int firstLine))
        {
            ctx.Report(line, line, display, firstLine);
        }
        else
        {
            seen[key] = line;
        }
    }

    private static (string? key, string display) NormalizeKey(Microsoft.CodeAnalysis.SyntaxNode expr)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax lit:
                return ("lit:" + lit.Token.Value?.ToString(), lit.Token.Text);
            case IdentifierNameSyntax id:
                return ("ident:" + id.Identifier.Text, id.Identifier.Text);
            default:
                return (null, "");
        }
    }
}
