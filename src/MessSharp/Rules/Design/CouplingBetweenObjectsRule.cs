using MessSharp.Model;
using MessSharp.Rule;
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

        void Collect(TypeSyntax? type)
        {
            types.UnionWith(TypeShapes.NamedTypes(type).Where(name => !BuiltinTypes.Contains(name)));
        }

        foreach (var f in cls.Fields)
            Collect(f.TypeSyntax);

        foreach (var m in cls.Methods)
        {
            foreach (var p in m.Parameters)
                Collect(p.TypeSyntax);
            Collect(TypeShapes.ReturnTypeOf(m));

            var body = m.EffectiveBody;
            if (body != null)
            {
                foreach (var objCreate in body.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>())
                    Collect(objCreate.Type);
            }
        }

        int cbo = types.Count;
        if (cbo >= threshold)
            ctx.ReportClass(cls, cls.Name, cbo, threshold);
    }
}
