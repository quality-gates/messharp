using MessSharp.Model;
using MessSharp.Rule;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Collects the member names read by a type's code, for the unused private
/// member rules. Scans the current file plus the parts of the same partial
/// type declared in other files.
/// </summary>
internal static class UsedMemberNames
{
    /// <summary>
    /// Collects every identifier that appears as: a member-access selector
    /// (this.Name or x.Name), an object-initializer key, or a nameof()
    /// argument — all of which count as "read" for field-usage purposes.
    /// Also collects bare identifier reads (covers access without `this.`),
    /// except when the identifier is only a plain-assignment target or is
    /// lexically bound to a local variable or parameter that shadows the
    /// member name.
    /// </summary>
    public static IReadOnlyList<IReadOnlySet<string>> Collect(RuleContext ctx, ClassModel cls)
    {
        var sets = new List<IReadOnlySet<string>> { UsedIn(ctx.File.Root) };
        foreach (var part in ctx.Partials.PartsInOtherFiles(cls))
            sets.Add(UsedIn(part.Node));
        return sets;
    }

    /// <summary>True when any collected set contains <paramref name="name"/>.</summary>
    public static bool Contains(this IReadOnlyList<IReadOnlySet<string>> used, string name)
    {
        foreach (var set in used)
            if (set.Contains(name)) return true;
        return false;
    }

    // Names read by a syntax tree never change, so every class and both rules share one scan per root.
    // Keyed by node identity, so nothing carries over between files or analyses.
    private static readonly ConditionalWeakTable<SyntaxNode, HashSet<string>> Scans = new();

    private static HashSet<string> UsedIn(SyntaxNode root) => Scans.GetValue(root, Scan);

    private static HashSet<string> Scan(SyntaxNode root)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in root.DescendantNodes())
            CollectUsedNode(node, used);
        return used;
    }

    private static void CollectUsedNode(SyntaxNode node, HashSet<string> used)
    {
        if (TryCollectMemberAccess(node, used)) return;
        if (TryCollectInitializerAssignment(node, used)) return;
        if (TryCollectNameof(node, used)) return;
        CollectBareIdentifier(node, used);
    }

    private static bool TryCollectMemberAccess(SyntaxNode node, HashSet<string> used)
    {
        var mae = node as MemberAccessExpressionSyntax;
        if (mae is null) return false;

        if (!WriteOnlyTarget.Matches(mae))
            used.Add(mae.Name.Identifier.Text);
        return true;
    }

    private static bool TryCollectInitializerAssignment(SyntaxNode node, HashSet<string> used)
    {
        var aes = node as AssignmentExpressionSyntax;
        if (aes is null) return false;

        var initializer = aes.Parent as InitializerExpressionSyntax;
        if (initializer is null) return false;

        if (aes.Left is IdentifierNameSyntax lhs)
            used.Add(lhs.Identifier.Text);
        return true;
    }

    private static void CollectBareIdentifier(SyntaxNode node, HashSet<string> used)
    {
        var id = node as IdentifierNameSyntax;
        if (id is null) return;
        if (IsDeclarationContext(id)) return;
        if (WriteOnlyTarget.Matches(id)) return;
        if (LexicalScopes.IsShadowed(id)) return;
        used.Add(id.Identifier.Text);
    }

    private static bool TryCollectNameof(SyntaxNode node, HashSet<string> used)
    {
        var inv = node as InvocationExpressionSyntax;
        if (inv is null) return false;

        var id2 = inv.Expression as IdentifierNameSyntax;
        if (id2 is null) return false;
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
