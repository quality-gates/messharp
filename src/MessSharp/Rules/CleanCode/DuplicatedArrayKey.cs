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
        var body = method.EffectiveBody;
        if (body == null) return;

        foreach (var init in body.DescendantNodesAndSelf()
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
            CheckEntry(ctx, expr, seen);
        }
    }

    private static void CheckEntry(RuleContext ctx, ExpressionSyntax expr,
        Dictionary<string, int> seen)
    {
        var (keyExpr, line) = ExtractKeyAndLine(expr);
        if (keyExpr == null) return;

        var (key, display) = NormalizeKey(keyExpr);
        if (key == null) return;

        if (seen.TryGetValue(key, out int firstLine))
        {
            ctx.Report(line, line, display, firstLine);
        }
        else
        {
            seen[key] = line;
        }
    }

    private static (SyntaxNode? keyExpr, int line) ExtractKeyAndLine(ExpressionSyntax expr)
    {
        if (expr is AssignmentExpressionSyntax assign && assign.Kind() == SyntaxKind.SimpleAssignmentExpression)
        {
            if (assign.Left is ImplicitElementAccessSyntax implicitElem && implicitElem.ArgumentList.Arguments.Count == 1)
            {
                var keyExpr = implicitElem.ArgumentList.Arguments[0].Expression;
                return (keyExpr, keyExpr.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
            }
        }

        if (expr is InitializerExpressionSyntax nested && nested.Expressions.Count >= 2)
        {
            var keyExpr = nested.Expressions[0];
            return (keyExpr, keyExpr.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }

        return (null, 0);
    }

    private static (string? key, string display) NormalizeKey(Microsoft.CodeAnalysis.SyntaxNode expr)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax lit:
                return ($"lit:{lit.Kind()}:{lit.Token.Value}", lit.Token.Text);
            case IdentifierNameSyntax id:
                return ("ident:" + id.Identifier.Text, id.Identifier.Text);
            default:
                return (null, "");
        }
    }
}
