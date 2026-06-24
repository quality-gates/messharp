using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// Counts distinct non-builtin type names a class references through field types,
/// method parameter types, return types, and object creation expressions.
/// Violation when the count reaches the `maximum` property (default 13).
/// </summary>
public sealed class CouplingBetweenObjectsRule : BaseRule, IClassRule
{
    private static readonly HashSet<string> BuiltinTypes = new(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float",
        "int", "uint", "long", "ulong", "short", "ushort", "nint", "nuint",
        "object", "string", "void", "dynamic",
        // common aliases
        "String", "Object", "Boolean", "Byte", "SByte", "Char", "Decimal",
        "Double", "Single", "Int16", "Int32", "Int64", "UInt16", "UInt32",
        "UInt64", "IntPtr", "UIntPtr",
        // extra common
        "var", "Task", "ValueTask",
    };

    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("maximum", 13);
        var types = new HashSet<string>(StringComparer.Ordinal);

        void Collect(string typeStr)
        {
            var name = BaseTypeName(typeStr);
            if (!string.IsNullOrEmpty(name) && !BuiltinTypes.Contains(name))
                types.Add(name);
        }

        foreach (var f in cls.Fields)
            Collect(f.Type);

        foreach (var m in cls.Methods)
        {
            foreach (var p in m.Parameters)
                Collect(p.Type);
            Collect(m.ReturnType);

            if (m.Body != null)
            {
                foreach (var objCreate in m.Body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                    Collect(objCreate.Type.ToString());
            }
        }

        int cbo = types.Count;
        if (cbo >= threshold)
            ctx.ReportClass(cls, cls.Name, cbo, threshold);
    }

    private static string BaseTypeName(string t)
    {
        if (string.IsNullOrEmpty(t)) return "";

        // Strip leading ?, [], *, &
        t = t.TrimStart('?', '[', ']', '*', '&');

        // Strip generic like List<T> -> List
        var ltIdx = t.IndexOf('<');
        if (ltIdx >= 0) t = t[..ltIdx];

        // Strip nullable suffix ?
        t = t.TrimEnd('?');

        // Take last part of qualified name A.B -> B
        var dotIdx = t.LastIndexOf('.');
        if (dotIdx >= 0) t = t[(dotIdx + 1)..];

        return t.Trim();
    }
}
