using System;

public class ModernMetrics
{
    private string? _name;

    public string Coalesce(string? input) { _name = _name ?? input; return _name ?? ""; }

    public string CoalesceAssign(string? input) { _name ??= input; return _name ?? ""; }

    public int GuardedCase(object o)
    {
        switch (o)
        {
            case int i when i > 5: return 1;
            case int: return 2;
            default: return 0;
        }
    }

    public int GuardedArm(object o) => o switch
    {
        int i when i > 5 => 1,
        int => 2,
        _ => 0,
    };

    public int CatchFilter(Action a)
    {
        try { a(); return 0; }
        catch (InvalidOperationException e) when (e.Message.Length > 0) { return 1; }
    }

    public int Lambda(int[] xs) => Array.Find(xs, x => x > 0 && x < 10);

    public int LocalFn(int v)
    {
        int Twice(int n) { if (n > 0) return n * 2; return 0; }
        return Twice(v);
    }
}
