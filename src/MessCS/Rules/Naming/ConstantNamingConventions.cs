using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.Naming;

/// <summary>
/// Reports class/interface constants that do not follow the configured naming
/// convention. C# adaptation: default convention is PascalCase (C# idiomatic);
/// set property <c>convention=upper</c> for ALL_CAPS style.
///
/// pascal (default): first char uppercase, no underscores (pure PascalCase).
/// upper: all chars uppercase or underscore (UPPER_CASE style).
///
/// Port of phpmd's ConstantNamingConventions, adapted from the messgo port.
/// </summary>
public sealed class ConstantNamingConventionsRule : BaseRule, IClassRule, IInterfaceRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        string convention = ctx.Props.Str("convention", "pascal");
        foreach (var c in cls.Constants)
            CheckConstant(ctx, c.Name, c.Line, convention);
    }

    public void Apply(RuleContext ctx, InterfaceModel iface)
    {
        string convention = ctx.Props.Str("convention", "pascal");
        // Interface members: walk constants from the syntax node
        foreach (var member in iface.Node.Members)
        {
            if (member is not FieldDeclarationSyntax field)
                continue;
            if (!field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                continue;
            foreach (var v in field.Declaration.Variables)
            {
                var span = v.SyntaxTree.GetLineSpan(v.Span);
                int line = span.StartLinePosition.Line + 1;
                CheckConstant(ctx, v.Identifier.Text, line, convention);
            }
        }
    }

    private static void CheckConstant(RuleContext ctx, string name, int line, string convention)
    {
        bool valid = convention.Equals("upper", StringComparison.OrdinalIgnoreCase)
            ? IsUpperCase(name)
            : IsPascalCase(name);

        if (!valid)
            ctx.Report(line, line, name);
    }

    /// <summary>PascalCase: starts with uppercase, no underscores.</summary>
    private static bool IsPascalCase(string name)
    {
        if (name.Length == 0) return true;
        if (!char.IsUpper(name[0])) return false;
        return !name.Contains('_');
    }

    /// <summary>UPPER_CASE: all uppercase letters and underscores.</summary>
    private static bool IsUpperCase(string name)
    {
        foreach (char c in name)
        {
            if (c == '_') continue;
            if (!char.IsUpper(c)) return false;
        }
        return true;
    }
}
