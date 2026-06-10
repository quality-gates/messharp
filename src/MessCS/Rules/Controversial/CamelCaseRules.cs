using MessCS.Model;
using MessCS.Rule;
using MessCS.Rules.UnusedCode;

namespace MessCS.Rules.Controversial;

// ─── Shared helpers ──────────────────────────────────────────────────────────

internal static class NamingConventions
{
    /// <summary>
    /// PascalCase: starts with an uppercase letter and contains no underscores.
    /// The blank identifier is passed through; callers decide whether to skip it.
    /// </summary>
    internal static bool IsPascalCase(string name) =>
        name.Length > 0 && char.IsUpper(name[0]) && !name.Contains('_');

    /// <summary>
    /// camelCase: starts with a lowercase letter and contains no underscores.
    /// `allowUnderscorePrefix` (default true) permits a leading `_` on
    /// private fields/locals — if the remainder is camelCase the name is OK.
    /// </summary>
    internal static bool IsCamelCase(string name, bool allowUnderscorePrefix = false)
    {
        if (name.Length == 0) return true;
        if (allowUnderscorePrefix && name[0] == '_')
            name = name.Substring(1);
        if (name.Length == 0) return true;   // bare `_` — not a real name
        return char.IsLower(name[0]) && !name.Contains('_');
    }
}

// ─── CamelCaseClassName ───────────────────────────────────────────────────────

/// <summary>
/// C# adaptation: classes/structs/records/interfaces must be PascalCase.
/// phpmd rule name kept; message adapted to say "PascalCase".
/// </summary>
public sealed class CamelCaseClassNameRule : BaseRule, IClassRule, IInterfaceRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        if (!NamingConventions.IsPascalCase(cls.Name))
            ctx.ReportClass(cls, cls.Name);
    }

    public void Apply(RuleContext ctx, InterfaceModel iface)
    {
        if (!NamingConventions.IsPascalCase(iface.Name))
            ctx.ReportInterface(iface, iface.Name);
    }
}

// ─── CamelCaseMethodName ──────────────────────────────────────────────────────

/// <summary>
/// C# adaptation: methods must be PascalCase (C# convention).
/// Constructors are excluded (their name equals the class name and is always
/// valid). phpmd rule name kept; message adapted to say "PascalCase".
/// </summary>
public sealed class CamelCaseMethodNameRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.IsConstructor) return;
        if (!NamingConventions.IsPascalCase(method.Name))
            ctx.ReportMethod(method, method.Name);
    }
}

// ─── CamelCasePropertyName ────────────────────────────────────────────────────

/// <summary>
/// C# adaptation: public fields/auto-properties must be PascalCase;
/// private fields may be camelCase or _camelCase (allowUnderscorePrefix=true).
/// phpmd rule name kept; message adapted accordingly.
/// </summary>
public sealed class CamelCasePropertyNameRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        bool allowPrefix = ctx.Props.Bool("allowUnderscorePrefix", true);

        foreach (var field in cls.Fields)
        {
            if (field.Exported)
            {
                // Public fields / auto-properties must be PascalCase
                if (!NamingConventions.IsPascalCase(field.Name))
                    ctx.Report(field.Line, field.Line, field.Name);
            }
            else
            {
                // Private fields: camelCase or _camelCase
                if (!NamingConventions.IsCamelCase(field.Name, allowPrefix))
                    ctx.Report(field.Line, field.Line, field.Name);
            }
        }
    }
}

// ─── CamelCaseParameterName ───────────────────────────────────────────────────

/// <summary>
/// Parameters must be camelCase (C# convention).
/// phpmd rule name kept; message unchanged (already says "camelCase").
/// </summary>
public sealed class CamelCaseParameterNameRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        foreach (var p in method.Parameters)
        {
            if (string.IsNullOrEmpty(p.Name) || p.Name == "_") continue;
            if (!NamingConventions.IsCamelCase(p.Name))
                ctx.Report(p.Line, p.Line, p.Name);
        }
    }
}

// ─── CamelCaseVariableName ────────────────────────────────────────────────────

/// <summary>
/// Local variables must be camelCase (C# convention).
/// `allowUnderscorePrefix` (default true) permits `_camelCase` locals.
/// phpmd rule name kept; message unchanged.
/// </summary>
public sealed class CamelCaseVariableNameRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        var body = BodyAnalysis.EffectiveBody(method);
        if (body == null) return;

        bool allowPrefix = ctx.Props.Bool("allowUnderscorePrefix", true);
        var locals = BodyAnalysis.LocalVariables(body);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, line) in locals)
        {
            if (name == "_") continue;
            if (seen.Contains(name)) continue;
            if (!NamingConventions.IsCamelCase(name, allowPrefix))
            {
                seen.Add(name);
                ctx.Report(line, line, name);
            }
        }
    }
}
