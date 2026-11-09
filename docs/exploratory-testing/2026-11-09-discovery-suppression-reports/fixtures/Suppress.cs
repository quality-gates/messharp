using System.Diagnostics.CodeAnalysis;

public class SuppressExample
{
    [SuppressMessage("PHPMD", "ShortVariable")]
    public int Intentional(int value)
    {
        int x = value + 1;
        return x;
    }

    public int Unreviewed(int value)
    {
        int y = value + 1;
        return y;
    }
}
