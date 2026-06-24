using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.UnusedCode;

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
            if (!field.IsPrivate) continue;
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
            CollectUsedNode(node, used);
        return used;
    }

    private static void CollectUsedNode(SyntaxNode node, HashSet<string> used)
    {
        if (node is MemberAccessExpressionSyntax mae)
        {
            used.Add(mae.Name.Identifier.Text);
            return;
        }

        if (node is AssignmentExpressionSyntax aes && aes.Parent is InitializerExpressionSyntax)
        {
            if (aes.Left is IdentifierNameSyntax lhs) used.Add(lhs.Identifier.Text);
            return;
        }

        if (TryCollectNameof(node, used)) return;

        if (node is IdentifierNameSyntax id && !IsDeclarationContext(id))
            used.Add(id.Identifier.Text);
    }

    private static bool TryCollectNameof(SyntaxNode node, HashSet<string> used)
    {
        if (node is not InvocationExpressionSyntax inv) return false;
        if (inv.Expression is not IdentifierNameSyntax id2) return false;
        if (id2.Identifier.Text != "nameof") return false;
        if (inv.ArgumentList.Arguments.Count != 1) return false;

        var arg = inv.ArgumentList.Arguments[0].Expression;
        if (arg is IdentifierNameSyntax nameofArg)
            used.Add(nameofArg.Identifier.Text);
        else if (arg is MemberAccessExpressionSyntax nameofMae)
            used.Add(nameofMae.Name.Identifier.Text);
        return true;
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
