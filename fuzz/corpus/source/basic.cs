namespace FuzzSeed;

public sealed class Example
{
    public int Add(int left, int right)
    {
        if (left > right)
        {
            return left - right;
        }

        return left + right;
    }
}
