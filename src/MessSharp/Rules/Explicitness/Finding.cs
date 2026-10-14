using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

internal readonly record struct Finding(SyntaxNode Site, string What)
{
    public static Finding FromAccess(StateAccess access, string noun) => new(access.Site, access.Kind switch
    {
        AccessKind.Read => $"reads {noun} '{access.Name}'",
        AccessKind.Write => $"writes {noun} '{access.Name}'",
        _ => $"changes the object in {noun} '{access.Name}'",
    });

    /// <summary>
    /// Reports each distinct finding once per printed owner, all on the earliest site among every callable printed alike.
    /// The first of those callables reports for all of them.
    /// </summary>
    public static void ReportAll(RuleContext ctx, MethodModel method, Func<MethodModel, IEnumerable<Finding>> findings)
    {
        var alike = CallableGraph.For(ctx).PrintedAlike(method).DefaultIfEmpty(method).ToList();
        if (alike[0] != method) return;
        var all = alike.SelectMany(findings).OrderBy(f => f.Site.SpanStart).ToList();
        if (all.Count == 0) return;
        var kind = method.IsConstructor ? "constructor" : "method";
        var reported = ReportedOwner.Printed(method);
        var line = all[0].Site.SyntaxTree.GetLineSpan(all[0].Site.Span).StartLinePosition.Line + 1;
        foreach (var what in all.Select(f => f.What).Distinct(StringComparer.Ordinal))
            ctx.ReportMethodAt(reported, line, line, kind, reported.Name, what);
    }
}
