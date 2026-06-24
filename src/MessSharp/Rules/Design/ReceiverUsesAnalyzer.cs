using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// Scans a method body for `this.X` and bare-identifier accesses to sibling
/// fields and methods. Extracted from Lcom4Calculator to keep WMC low.
/// </summary>
internal static class ReceiverUsesAnalyzer
{
    internal static (List<string> UsedFields, List<string> CalledMethods)
        Collect(MethodModel m, HashSet<string> fields, Dictionary<string, int> methodIndex)
    {
        var usedFields    = new List<string>();
        var calledMethods = new List<string>();
        if (m.Body == null) return (usedFields, calledMethods);

        bool isStatic = IsStaticMethod(m);
        var seenFields  = new HashSet<string>(StringComparer.Ordinal);
        var seenMethods = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in m.Body.DescendantNodes())
            ProcessNode(node, isStatic, fields, methodIndex, seenFields, seenMethods, usedFields, calledMethods);

        return (usedFields, calledMethods);
    }

    private static void ProcessNode(
        SyntaxNode node, bool isStatic,
        HashSet<string> fields, Dictionary<string, int> methodIndex,
        HashSet<string> seenFields, HashSet<string> seenMethods,
        List<string> usedFields, List<string> calledMethods)
    {
        if (TryGetThisMember(node, out var thisName))
        {
            ClassifyName(thisName!, fields, methodIndex, seenFields, seenMethods, usedFields, calledMethods);
            return;
        }
        if (TryGetBareIdent(node, out var bareName))
            ApplyBareIdent(bareName!, isStatic, fields, methodIndex, seenFields, seenMethods, usedFields, calledMethods);
    }

    private static bool TryGetThisMember(SyntaxNode node, out string? name)
    {
        if (node is MemberAccessExpressionSyntax ma && ma.Expression is ThisExpressionSyntax)
        {
            name = ma.Name.Identifier.Text;
            return true;
        }
        name = null;
        return false;
    }

    private static bool TryGetBareIdent(SyntaxNode node, out string? name)
    {
        if (node is not IdentifierNameSyntax id) { name = null; return false; }
        if (id.Parent is MemberAccessExpressionSyntax pma && pma.Name == id) { name = null; return false; }
        if (id.Parent is QualifiedNameSyntax qn && qn.Right == id) { name = null; return false; }
        name = id.Identifier.Text;
        return true;
    }

    private static void ApplyBareIdent(
        string name, bool isStatic,
        HashSet<string> fields, Dictionary<string, int> methodIndex,
        HashSet<string> seenFields, HashSet<string> seenMethods,
        List<string> usedFields, List<string> calledMethods)
    {
        if (isStatic)
        {
            if (fields.Contains(name) && seenFields.Add(name)) usedFields.Add(name);
        }
        else
        {
            ClassifyName(name, fields, methodIndex, seenFields, seenMethods, usedFields, calledMethods);
        }
    }

    private static void ClassifyName(
        string name,
        HashSet<string> fields, Dictionary<string, int> methodIndex,
        HashSet<string> seenFields, HashSet<string> seenMethods,
        List<string> usedFields, List<string> calledMethods)
    {
        if (fields.Contains(name) && seenFields.Add(name))
            usedFields.Add(name);
        else if (methodIndex.ContainsKey(name) && seenMethods.Add(name))
            calledMethods.Add(name);
    }

    private static bool IsStaticMethod(MethodModel m)
    {
        if (m.Node is MethodDeclarationSyntax md)
            return md.Modifiers.Any(SyntaxKind.StaticKeyword);
        if (m.Node is ConstructorDeclarationSyntax cd)
            return cd.Modifiers.Any(SyntaxKind.StaticKeyword);
        return false;
    }
}
