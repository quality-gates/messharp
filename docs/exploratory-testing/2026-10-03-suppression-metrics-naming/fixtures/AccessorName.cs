namespace Demo;

public class AccessorName
{
    private int _count;

    public int Count
    {
        get { return _count; }
        set { _count = value; }
    }

    public int Expr => _count;

    public int ExprAccessor { get => _count; }
}
