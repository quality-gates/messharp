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
        if (cls != null && SuppressionMatcher.IsNodeSuppressed(cls.Node, v.Rule)) return true;

        var method = FindMethod(file, cls, v);
        if (method != null && SuppressionMatcher.IsNodeSuppressed(method.Node, v.Rule)) return true;

        var field = FindField(cls, v);
        if (field != null && SuppressionMatcher.IsNodeSuppressed(field.Node, v.Rule)) return true;

        return false;
    }

    private static ClassModel? FindClass(SourceFile file, Violation v)
    {
        if (!string.IsNullOrEmpty(v.Class))
            return file.Classes.FirstOrDefault(c => c.Name == v.Class);

        return file.Classes.FirstOrDefault(c => v.BeginLine >= c.Line && v.BeginLine <= c.EndLine);
    }

    private static MethodModel? FindMethod(SourceFile file, ClassModel? cls, Violation v)
    {
        var methods = cls != null ? cls.Methods : file.AllMethods;
        if (!string.IsNullOrEmpty(v.Method))
            return methods.FirstOrDefault(m => m.Name == v.Method);

        return methods.FirstOrDefault(m => v.BeginLine >= m.Line && v.BeginLine <= m.EndLine);
    }

    private static FieldModel? FindField(ClassModel? cls, Violation v)
    {
        if (cls == null) return null;
        return cls.Fields.FirstOrDefault(f => f.Line == v.BeginLine)
            ?? cls.Constants.FirstOrDefault(c => c.Line == v.BeginLine);
    }
}
