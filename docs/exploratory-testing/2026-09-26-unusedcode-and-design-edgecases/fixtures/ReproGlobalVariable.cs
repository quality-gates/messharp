public class GenericCacheRepro<T>
{
    public static int GenericCounter;

    public void IncrQualified()
    {
        GenericCacheRepro<T>.GenericCounter++;
    }
}

public class NonGenericCacheControl
{
    public static int NormalCounter;

    public void IncrQualified()
    {
        NonGenericCacheControl.NormalCounter++;
    }
}

public class TupleStaticRepro
{
    public static int StateA;
    public static int StateB;

    public void ResetTuple()
    {
        (StateA, StateB) = (1, 2);
    }
}

public class ScalarStaticControl
{
    public static int StateA;
    public static int StateB;

    public void ResetScalar()
    {
        StateA = 1;
        StateB = 2;
    }
}
