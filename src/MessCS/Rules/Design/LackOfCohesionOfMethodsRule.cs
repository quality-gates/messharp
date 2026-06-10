using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.Design;

/// <summary>
/// Computes LCOM4 per class. Methods are nodes in a graph; edges connect them
/// when they share a field or one calls the other (via `this.` or unqualified).
/// Trivial getters/setters are excluded from the graph (a call to one counts as
/// use of its backing field). Stateless methods (no field use, no calls) are
/// ignored. Violation when connected-component count exceeds `maximum` (default 1).
/// </summary>
public sealed class LackOfCohesionOfMethodsRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("maximum", 1);
        int lcom = ComputeLcom4(cls);
        if (lcom > threshold)
            ctx.ReportClass(cls, cls.Name, lcom, threshold);
    }

    private static int ComputeLcom4(ClassModel cls)
    {
        var fields = BuildFieldSet(cls);
        var (methodIndex, accessorOf) = IndexMethods(cls, fields);

        var graph = new UnionFind(cls.Methods.Count);

        for (int i = 0; i < cls.Methods.Count; i++)
        {
            var m = cls.Methods[i];
            if (accessorOf.ContainsKey(m.Name)) continue; // skip trivial accessor

            var (usedFields, calledMethods) = CollectReceiverUses(m, fields, methodIndex);

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
        foreach (var f in cls.Fields)
            fields.Add(f.Name);
        return fields;
    }

    /// <summary>
    /// Maps method names to their index in cls.Methods, and trivial accessor
    /// names to the field they wrap.
    /// </summary>
    private static (Dictionary<string, int> MethodIndex, Dictionary<string, string> AccessorOf)
        IndexMethods(ClassModel cls, HashSet<string> fields)
    {
        var methodIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var accessorOf = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < cls.Methods.Count; i++)
        {
            var m = cls.Methods[i];
            methodIndex[m.Name] = i;
            var backing = TrivialAccessorField(m, fields);
            if (backing != null)
                accessorOf[m.Name] = backing;
        }

        return (methodIndex, accessorOf);
    }

    /// <summary>
    /// Returns the backing field name if the method is a trivial getter
    /// (single `return this.field`) or trivial setter (`this.field = param`).
    /// Returns null for anything more complex.
    /// </summary>
    private static string? TrivialAccessorField(MethodModel m, HashSet<string> fields)
    {
        if (m.Body == null || m.Body.Statements.Count != 1)
            return null;

        var stmt = m.Body.Statements[0];

        // Trivial getter: return this.field  or  return field
        if (stmt is ReturnStatementSyntax ret && ret.Expression != null)
        {
            return ExtractFieldAccess(ret.Expression, fields);
        }

        // Trivial setter: this.field = value  (plain identifier or literal rhs)
        if (stmt is ExpressionStatementSyntax exprStmt &&
            exprStmt.Expression is AssignmentExpressionSyntax assign &&
            assign.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
            IsPlainValue(assign.Right))
        {
            return ExtractFieldAccess(assign.Left, fields);
        }

        return null;
    }

    private static string? ExtractFieldAccess(ExpressionSyntax expr, HashSet<string> fields)
    {
        // this.fieldName
        if (expr is MemberAccessExpressionSyntax ma &&
            ma.Expression is ThisExpressionSyntax &&
            fields.Contains(ma.Name.Identifier.Text))
            return ma.Name.Identifier.Text;

        // bare fieldName
        if (expr is IdentifierNameSyntax id && fields.Contains(id.Identifier.Text))
            return id.Identifier.Text;

        return null;
    }

    private static bool IsPlainValue(ExpressionSyntax e) =>
        e is IdentifierNameSyntax or LiteralExpressionSyntax;

    /// <summary>
    /// Scans a method body for `this.X` accesses and splits them into used
    /// field names and called sibling method names.
    /// Also handles unqualified references inside the class body.
    /// </summary>
    private static (List<string> UsedFields, List<string> CalledMethods)
        CollectReceiverUses(MethodModel m, HashSet<string> fields,
            Dictionary<string, int> methodIndex)
    {
        var usedFields = new List<string>();
        var calledMethods = new List<string>();

        if (m.Body == null) return (usedFields, calledMethods);

        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        var seenMethods = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in m.Body.DescendantNodes())
        {
            // this.Member  — could be field or method
            if (node is MemberAccessExpressionSyntax ma &&
                ma.Expression is ThisExpressionSyntax)
            {
                var name = ma.Name.Identifier.Text;
                ClassifyName(name, fields, methodIndex, seenFields, seenMethods,
                    usedFields, calledMethods);
                continue;
            }

            // Unqualified reference: a bare identifier naming a sibling field
            // (e.g. `_conns[addr] = 1`) or sibling method (`Snapshot()`).
            // Identifiers that are the right side of a member access or
            // qualified name refer to another receiver's members — skip them.
            if (node is IdentifierNameSyntax id &&
                !(id.Parent is MemberAccessExpressionSyntax pma && pma.Name == id) &&
                !(id.Parent is QualifiedNameSyntax qn && qn.Right == id))
            {
                ClassifyName(id.Identifier.Text, fields, methodIndex, seenFields,
                    seenMethods, usedFields, calledMethods);
            }
        }

        return (usedFields, calledMethods);
    }

    private static void ClassifyName(
        string name,
        HashSet<string> fields,
        Dictionary<string, int> methodIndex,
        HashSet<string> seenFields,
        HashSet<string> seenMethods,
        List<string> usedFields,
        List<string> calledMethods)
    {
        if (fields.Contains(name) && !seenFields.Contains(name))
        {
            seenFields.Add(name);
            usedFields.Add(name);
        }
        else if (methodIndex.ContainsKey(name) && !seenMethods.Contains(name))
        {
            seenMethods.Add(name);
            calledMethods.Add(name);
        }
    }
}

/// <summary>
/// Union-find data structure tracking active (field-touching or call-linked)
/// method nodes for LCOM4.
/// </summary>
internal sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly bool[] _active;
    private readonly Dictionary<string, int> _fieldOwner;

    public UnionFind(int n)
    {
        _parent = new int[n];
        _active = new bool[n];
        _fieldOwner = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < n; i++) _parent[i] = i;
    }

    public void AddFieldUse(int method, string field)
    {
        _active[method] = true;
        if (_fieldOwner.TryGetValue(field, out var owner))
            Union(method, owner);
        else
            _fieldOwner[field] = method;
    }

    public void AddCall(int caller, int callee)
    {
        _active[caller] = true;
        _active[callee] = true;
        Union(caller, callee);
    }

    public int CountComponents()
    {
        var roots = new HashSet<int>();
        for (int i = 0; i < _active.Length; i++)
            if (_active[i]) roots.Add(Find(i));
        return roots.Count == 0 ? 1 : roots.Count;
    }

    private void Union(int a, int b) => _parent[Find(a)] = Find(b);

    private int Find(int x)
    {
        while (_parent[x] != x)
        {
            _parent[x] = _parent[_parent[x]];
            x = _parent[x];
        }
        return x;
    }
}
