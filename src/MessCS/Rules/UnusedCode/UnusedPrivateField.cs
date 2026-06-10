using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.UnusedCode;

/// <summary>
/// Reports private fields that are never read within the file.
/// Port of messgo's UnusedPrivateField; adapted to C# (private modifier,
/// `this.Name` member-access, struct-literal key, nameof(x) all count as use).
/// </summary>
public sealed class UnusedPrivateFieldRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        var used = CollectUsedNames(ctx.File);
        foreach (var field in cls.Fields)
        {
            if (field.Exported) continue;
            if (field.Name == "_") continue;
            if (used.Contains(field.Name)) continue;
            ctx.ReportClass(cls, field.Name);
        }
    }

    /// <summary>
    /// Collects every identifier that appears as: a member-access selector
    /// (this.Name or x.Name), an object-initializer key, or a nameof()
    /// argument — all of which count as "read" for field-usage purposes.
    /// Also collects bare identifier reads (covers access without `this.`).
    /// </summary>
    internal static HashSet<string> CollectUsedNames(SourceFile file)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in file.Root.DescendantNodes())
        {
            switch (node)
            {
                case MemberAccessExpressionSyntax mae:
                    used.Add(mae.Name.Identifier.Text);
                    break;
                case AssignmentExpressionSyntax aes when aes.Parent is InitializerExpressionSyntax:
                    // object initializer: Prop = value — "Prop" counts as used
                    if (aes.Left is IdentifierNameSyntax lhs)
                        used.Add(lhs.Identifier.Text);
                    break;
                case InvocationExpressionSyntax inv
                    when inv.Expression is IdentifierNameSyntax id2
                      && id2.Identifier.Text == "nameof"
                      && inv.ArgumentList.Arguments.Count == 1:
                    var arg = inv.ArgumentList.Arguments[0].Expression;
                    if (arg is IdentifierNameSyntax nameofArg)
                        used.Add(nameofArg.Identifier.Text);
                    else if (arg is MemberAccessExpressionSyntax nameofMae)
                        used.Add(nameofMae.Name.Identifier.Text);
                    break;
                case IdentifierNameSyntax id:
                    // Only count reads — skip declaration nodes
                    if (!IsDeclarationContext(id))
                        used.Add(id.Identifier.Text);
                    break;
            }
        }
        return used;
    }

    private static bool IsDeclarationContext(IdentifierNameSyntax id)
    {
        // variable declarator, field declarator, parameter — these are
        // definitions not reads; exclude them from the "used" set.
        var parent = id.Parent;
        if (parent is VariableDeclaratorSyntax vd && vd.Identifier.Text == id.Identifier.Text)
            return true;
        return false;
    }
}
