using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// Flags calls to development/debug functions: Console.Write*, Debug.WriteLine,
/// Debugger.Break, Debugger.Launch.
/// The `unwanted-functions` property extends the list (comma-separated, case-insensitive).
/// </summary>
public sealed class DevelopmentCodeFragmentRule : BaseRule, IMethodRule
{
    private static readonly string[] DefaultUnwanted =
    {
        "console.write",
        "console.writeline",
        "debug.writeline",
        "debugger.break",
        "debugger.launch",
    };

    public void Apply(RuleContext ctx, MethodModel method)
    {
        var body = method.EffectiveBody;
        if (body == null) return;

        var unwanted = BuildUnwantedSet(ctx.Props.Str("unwanted-functions", ""));

        foreach (var invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            var name = GetCallName(invocation.Expression);
            if (name == null) continue;

            var lower = name.ToLowerInvariant();
            if (MatchesUnwanted(lower, unwanted))
            {
                var line = invocation.SyntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                var kind = method.IsConstructor ? "constructor" : "method";
                ctx.Report(line, line, kind, method.Name, name);
            }
        }
    }

    private static bool MatchesUnwanted(string lower, HashSet<string> unwanted)
    {
        if (unwanted.Contains(lower)) return true;
        foreach (var def in DefaultUnwanted)
        {
            if (lower == def || lower.EndsWith("." + def, StringComparison.Ordinal))
                return true;
        }
        foreach (var u in unwanted)
        {
            if (lower.EndsWith("." + u, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static HashSet<string> BuildUnwantedSet(string prop)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(prop)) return set;
        foreach (var item in prop.Split(','))
        {
            var t = item.Trim();
            if (t.Length > 0) set.Add(t.ToLowerInvariant());
        }
        return set;
    }

    private static string? GetCallName(Microsoft.CodeAnalysis.SyntaxNode expr)
    {
        if (expr is MemberAccessExpressionSyntax ma)
            return ma.ToString();
        if (expr is IdentifierNameSyntax id)
            return id.Identifier.Text;
        return null;
    }
}
