using System.Diagnostics.CodeAnalysis;

public class SuppressProperty
{
    [SuppressMessage("PHPMD", "ShortVariable")]
    public int AttrMethod() { int a = 1; return a; }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public int AttrProperty { get { int b = 1; return b; } }

    /** @SuppressWarnings(PHPMD.ShortVariable) */
    public int CommentProperty { get { int c = 1; return c; } }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public int this[int index] { get { int d = index; return d; } }
}
