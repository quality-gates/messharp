using System.Diagnostics.CodeAnalysis;

public class SuppressPropertyMin
{
    [SuppressMessage("PHPMD", "ShortVariable")]
    public int SuppressedMethod() { int a = 1; return a; }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public int SuppressedProperty { get { int b = 1; return b; } }
}
