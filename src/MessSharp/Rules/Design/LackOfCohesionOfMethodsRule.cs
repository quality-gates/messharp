using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Design;

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
        int lcom = Lcom4Calculator.Compute(cls);
        if (lcom > threshold)
            ctx.ReportClass(cls, cls.Name, lcom, threshold);
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
        int len = _active.Length;
        for (int i = 0; i < len; i++)
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
