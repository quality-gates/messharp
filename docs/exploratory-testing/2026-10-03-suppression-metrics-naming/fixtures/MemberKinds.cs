using System;

public class MemberKinds
{
    private EventHandler? _handler;

    public int Method() { int unusedA = 1; int q = 2; return q; }

    public int Property { get { int unusedB = 1; int r = 2; return r; } }

    public int this[int index] { get { int unusedC = 1; int s = index; return s; } }

    public event EventHandler Changed
    {
        add { int unusedD = 1; int t = 2; _handler += value; }
        remove { _handler -= value; }
    }
}
