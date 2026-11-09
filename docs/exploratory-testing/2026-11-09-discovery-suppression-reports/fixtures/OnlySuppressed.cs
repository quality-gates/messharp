using System.Diagnostics.CodeAnalysis;

public class OnlySuppressed
{
    [SuppressMessage("PHPMD", "ShortVariable")]
    public int Calculate(int value)
    {
        int x = value + 1;
        return x;
    }
}
