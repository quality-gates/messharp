using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.Design;

/// <summary>
/// Flags calls to development/debug functions: Console.Write*, Debug.WriteLine,
/// Debugger.Break, Debugger.Launch.
/// The `unwanted-functions` property extends the list (comma-separated, case-insensitive).
/// </summary>
public sealed class DevelopmentCodeFragmentRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        var unwanted = BuildUnwantedSet(ctx.Props.Str("unwanted-functions", ""));

        foreach (var invocation in method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = GetCallName(invocation.Expression);
            if (name == null) continue;

            var lower = name.ToLowerInvariant();
            if (IsDefaultUnwanted(lower) || unwanted.Contains(lower))
            {
                var line = invocation.SyntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                var kind = method.IsConstructor ? "constructor" : "method";
                ctx.Report(line, line, kind, method.Name, name);
            }
        }
    }

    private static bool IsDefaultUnwanted(string lower) =>
        lower == "console.write" ||
        lower == "console.writeline" ||
        lower == "debug.writeline" ||
        lower == "debugger.break" ||
        lower == "debugger.launch";

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
