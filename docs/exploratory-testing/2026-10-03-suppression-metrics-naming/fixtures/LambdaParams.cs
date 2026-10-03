using System;

public class LambdaParams
{
    public int Run(int[] items)
    {
        Func<int, int> twice = q => q * 2;
        Func<int, int> echo = thisIsAVeryLongLambdaParameterName => thisIsAVeryLongLambdaParameterName;
        return twice(1) + echo(2) + items.Length;
    }
}
