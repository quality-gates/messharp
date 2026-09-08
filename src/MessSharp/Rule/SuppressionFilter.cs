using MessSharp.Model;

namespace MessSharp.Rule;

/// <summary>
/// Filters suppressed violations by resolving target syntax nodes and delegating
/// suppression checks to SuppressionMatcher.
/// </summary>
internal static class SuppressionFilter
{
    public static List<Violation> Filter(List<Violation> violations, SourceFile file)
    {
        if (violations.Count == 0) return violations;
        return violations.Where(v => !IsSuppressed(v, file)).ToList();
    }

    private static bool IsSuppressed(Violation v, SourceFile file)
    {
        if (SuppressionMatcher.IsNodeSuppressed(file.Root, v.Rule)) return true;

        var cls = FindClass(file, v);
        var iface = FindInterface(file, v);
        if (IsTypeSuppressed(cls, iface, v.Rule)) return true;

        var method = FindMethod(file, cls, iface, v);
        if (method != null && SuppressionMatcher.IsNodeSuppressed(method.Node, v.Rule)) return true;

        var field = FindField(cls, v);
        return field != null && SuppressionMatcher.IsNodeSuppressed(field.Node, v.Rule);
    }

    private static bool IsTypeSuppressed(ClassModel? cls, InterfaceModel? iface, IRule rule) =>
        (cls != null && SuppressionMatcher.IsNodeSuppressed(cls.Node, rule))
        || (iface != null && SuppressionMatcher.IsNodeSuppressed(iface.Node, rule));

    private static ClassModel? FindClass(SourceFile file, Violation v)
    {
        if (!string.IsNullOrEmpty(v.Class))
            return file.Classes.FirstOrDefault(c => c.Name == v.Class);

        return file.Classes.FirstOrDefault(c => v.BeginLine >= c.Line && v.BeginLine <= c.EndLine);
    }

    private static InterfaceModel? FindInterface(SourceFile file, Violation v)
    {
        if (!string.IsNullOrEmpty(v.Class))
            return file.Interfaces.FirstOrDefault(i => i.Name == v.Class);

        return file.Interfaces.FirstOrDefault(i => v.BeginLine >= i.Line && v.BeginLine <= i.EndLine);
    }

    private static MethodModel? FindMethod(SourceFile file, ClassModel? cls, InterfaceModel? iface, Violation v)
    {
        var methods = cls?.Methods ?? iface?.Methods ?? file.AllMethods;
        return methods.FirstOrDefault(m =>
            (string.IsNullOrEmpty(v.Method) || m.Name == v.Method)
            && v.BeginLine >= m.Line
            && v.BeginLine <= m.EndLine);
    }

    private static FieldModel? FindField(ClassModel? cls, Violation v)
    {
        if (cls == null) return null;
        return cls.Fields.FirstOrDefault(f => f.Line == v.BeginLine
            || (f.EndLine > 0 && v.BeginLine >= f.Line && v.BeginLine <= f.EndLine))
            ?? cls.Constants.FirstOrDefault(c => c.Line == v.BeginLine
            || (c.EndLine > 0 && v.BeginLine >= c.Line && v.BeginLine <= c.EndLine));
    }
}
