namespace MessSharp.Metrics;

/// <summary>
/// Saturating arithmetic shared by NPath statement and expression metrics.
/// </summary>
internal static class NPathArithmetic
{
    internal static int Add(int a, int b)
    {
        long sum = (long)a + b;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    internal static int Multiply(int a, int b)
    {
        long product = (long)a * b;
        return product > int.MaxValue ? int.MaxValue : (int)product;
    }
}
