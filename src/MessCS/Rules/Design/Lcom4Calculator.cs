using MessCS.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.Design;

/// <summary>
/// Computes LCOM4 (Lack of Cohesion of Methods, version 4) for a class.
/// Extracted from LackOfCohesionOfMethodsRule to keep that class's WMC low.
/// Mirrors messgo's lcom4 and receiverUses logic, adapted to C# (no receiver
/// variable; `this.X` is the equivalent of `r.X` in Go).
/// </summary>
internal static class Lcom4Calculator
{
    internal static int Compute(ClassModel cls)
    {
        var fields = BuildFieldSet(cls);
        var (methodIndex, accessorOf) = IndexMethods(cls, fields);
        var graph = new UnionFind(cls.Methods.Count);

        for (int i = 0; i < cls.Methods.Count; i++)
        {
            var m = cls.Methods[i];
            if (accessorOf.ContainsKey(m.Name)) continue;

            var (usedFields, calledMethods) =
                ReceiverUsesAnalyzer.Collect(m, fields, methodIndex);

            foreach (var f in usedFields)
                graph.AddFieldUse(i, f);

            foreach (var callee in calledMethods)
            {
                if (accessorOf.TryGetValue(callee, out var backingField))
                    graph.AddFieldUse(i, backingField);
                else if (methodIndex.TryGetValue(callee, out var calleeIdx))
                    graph.AddCall(i, calleeIdx);
            }
        }

        return graph.CountComponents();
    }

    private static HashSet<string> BuildFieldSet(ClassModel cls)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in cls.Fields) fields.Add(f.Name);
        return fields;
    }

    private static (Dictionary<string, int> MethodIndex, Dictionary<string, string> AccessorOf)
        IndexMethods(ClassModel cls, HashSet<string> fields)
    {
        var methodIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var accessorOf  = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < cls.Methods.Count; i++)
        {
            var m = cls.Methods[i];
            methodIndex[m.Name] = i;
            var backing = TrivialAccessorField(m, fields);
            if (backing != null) accessorOf[m.Name] = backing;
        }
        return (methodIndex, accessorOf);
    }

    private static string? TrivialAccessorField(MethodModel m, HashSet<string> fields)
    {
        if (m.Body == null || m.Body.Statements.Count != 1) return null;
        var stmt = m.Body.Statements[0];

        if (stmt is ReturnStatementSyntax ret && ret.Expression != null)
            return ExtractFieldAccess(ret.Expression, fields);

        if (stmt is ExpressionStatementSyntax exprStmt &&
            exprStmt.Expression is AssignmentExpressionSyntax assign &&
            assign.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
            assign.Right is IdentifierNameSyntax or LiteralExpressionSyntax)
            return ExtractFieldAccess(assign.Left, fields);

        return null;
    }

    private static string? ExtractFieldAccess(ExpressionSyntax expr, HashSet<string> fields)
    {
        if (expr is MemberAccessExpressionSyntax ma &&
            ma.Expression is ThisExpressionSyntax &&
            fields.Contains(ma.Name.Identifier.Text))
            return ma.Name.Identifier.Text;

        if (expr is IdentifierNameSyntax id && fields.Contains(id.Identifier.Text))
            return id.Identifier.Text;

        return null;
    }
}
