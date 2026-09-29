using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// Counts distinct non-builtin type names (including generic type arguments)
/// a class references through field types,
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
            types.UnionWith(CoupledTypeNames(typeStr));
        }

        foreach (var f in cls.Fields)
            Collect(f.Type);

        foreach (var m in cls.Methods)
        {
            foreach (var p in m.Parameters)
                Collect(p.Type);
            Collect(m.ReturnType);

            var body = m.EffectiveBody;
            if (body != null)
            {
                foreach (var objCreate in body.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>())
                    Collect(objCreate.Type.ToString());
            }
        }

        int cbo = types.Count;
        if (cbo >= threshold)
            ctx.ReportClass(cls, cls.Name, cbo, threshold);
    }

    /// <summary>
    /// Yields the simple name of every non-builtin named type in <paramref name="t"/>,
    /// including generic type arguments, tuple elements and array/nullable
    /// element types. Namespace and alias qualifiers are skipped, so
    /// <c>System.Threading.Tasks.Task&lt;Customer&gt;</c> yields only Customer.
    /// </summary>
    private static IEnumerable<string> CoupledTypeNames(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return [];

        return SyntaxFactory.ParseTypeName(t)
            .DescendantNodesAndSelf()
            .OfType<SimpleNameSyntax>()
            .Where(n => !IsQualifier(n))
            .Select(n => n.Identifier.ValueText)
            .Where(name => name.Length > 0 && !BuiltinTypes.Contains(name));
    }

    private static bool IsQualifier(SimpleNameSyntax name)
    {
        SyntaxNode current = name;
        while (current.Parent is QualifiedNameSyntax qualified)
        {
            if (qualified.Left == current) return true;
            current = qualified;
        }
        return current.Parent is AliasQualifiedNameSyntax alias && alias.Alias == current;
    }
}
