using System;

public class LongNames
{
    public Func<int, int> M(int[] items)
    {
        foreach (var thisIsAVeryLongForeachVariableName in items) { Console.WriteLine(thisIsAVeryLongForeachVariableName); }
        Func<int, int> f = thisIsAVeryLongLambdaParameterName => thisIsAVeryLongLambdaParameterName;
        return f;
    }
}
