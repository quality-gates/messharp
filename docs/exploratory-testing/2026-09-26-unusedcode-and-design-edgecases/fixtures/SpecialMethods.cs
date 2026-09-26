public class SpecialMethods
{
    private int _val;

    public int MyProp
    {
        get { return _val; }
        set { _val = value; }
    }

    public static SpecialMethods operator +(SpecialMethods a, SpecialMethods b)
    {
        return a;
    }

    ~SpecialMethods()
    {
    }
}
