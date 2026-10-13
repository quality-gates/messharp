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

    public static void ReportAll(RuleContext ctx, MethodModel method, IEnumerable<Finding> findings)
    {
        var kind = method.IsConstructor ? "constructor" : "method";
        var reported = method.IsStaticConstructor() ? new MethodModel
        {
            Name = "<module>",
            Class = method.Class,
            Interface = method.Interface,
            Node = method.Node,
            File = method.File,
        } : method;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in findings.OrderBy(f => f.Site.SpanStart))
        {
            if (!seen.Add(finding.What)) continue;
            var line = finding.Site.SyntaxTree.GetLineSpan(finding.Site.Span).StartLinePosition.Line + 1;
            ctx.ReportMethodAt(reported, line, line, kind, reported.Name, finding.What);
        }
    }
}
