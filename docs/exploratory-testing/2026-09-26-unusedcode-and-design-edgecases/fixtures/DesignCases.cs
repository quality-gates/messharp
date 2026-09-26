namespace ExploratoryTesting.Design;

public class GenericCache<T>
{
    public static int Counter;

    // Mutated via class-qualified access with generic name
    public void Incr()
    {
        GenericCache<T>.Counter++;
    }
}

public class StaticTupleCases
{
    public static int StateA;
    public static int StateB;

    // Mutated via tuple deconstruction assignment
    public void Mutate()
    {
        (StateA, StateB) = (1, 2);
    }
}

public class ControlClass
{
    public static int NormalState;

    // Mutated via simple assignment (differential control)
    public void Mutate()
    {
        NormalState = 1;
    }
}

public class CatchCases
{
    public void NormalEmptyCatch()
    {
        try { DoWork(); } catch (Exception) { }
    }

    public void CommentOnlyCatch()
    {
        try { DoWork(); } catch (Exception) { /* ignored */ }
    }

    // Empty catch inside a lambda in an expression-bodied method
    public Action LambdaEmptyCatch() => () =>
    {
        try { DoWork(); } catch (Exception) { }
    };

    private static void DoWork() {}
}

public class LoopCases
{
    private readonly List<int> _list = new();

    public void NormalForCount()
    {
        for (int i = 0; i < _list.Count; i++) {}
    }

    public Action LambdaForCount() => () =>
    {
        for (int i = 0; i < _list.Count; i++) {}
    };
}
