using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>One implicit input or output: where it happens and what it is.</summary>
internal readonly record struct Finding(SyntaxNode Site, string What)
{
    /// <summary>Describes a state access, e.g. "writes static member 'total'".</summary>
    public static Finding FromAccess(StateAccess access, string noun) => new(access.Site, access.Kind switch
    {
        AccessKind.Read => $"reads {noun} '{access.Name}'",
        AccessKind.Write => $"writes {noun} '{access.Name}'",
        _ => $"changes the object in {noun} '{access.Name}'",
    });

    /// <summary>
    /// Reports each distinct finding once per method, at its first use in
    /// source order.
    /// </summary>
    public static void ReportAll(RuleContext ctx, MethodModel method, IEnumerable<Finding> findings)
    {
        var kind = method.IsConstructor ? "constructor" : "method";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in findings.OrderBy(f => f.Site.SpanStart))
        {
            if (!seen.Add(finding.What)) continue;
            var line = finding.Site.SyntaxTree.GetLineSpan(finding.Site.Span).StartLinePosition.Line + 1;
            ctx.Report(line, line, kind, method.Name, finding.What);
        }
    }
}
