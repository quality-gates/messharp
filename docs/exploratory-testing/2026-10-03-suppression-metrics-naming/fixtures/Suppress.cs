using System.Diagnostics.CodeAnalysis;

namespace Demo;

public class Suppress
{
    // Control: unsuppressed short variable.
    public int Control() { int q = 1; return q; }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public int AttrMethod() { int a = 1; return a; }

    /** @SuppressWarnings(PHPMD.ShortVariable) */
    public int CommentMethod() { int b = 1; return b; }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public int AttrProperty { get { int c = 1; return c; } }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public Suppress() { int d = 1; _ = d; }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public static Suppress operator +(Suppress l, Suppress r) { int e = 1; _ = e; return l; }

    public int Getter
    {
        [SuppressMessage("PHPMD", "ShortVariable")]
        get { int f = 1; return f; }
    }

    public int LocalFn()
    {
        [SuppressMessage("PHPMD", "ShortVariable")]
        int Inner() { int g = 1; return g; }
        return Inner();
    }
}

[SuppressMessage("PHPMD", "ShortVariable")]
public struct SuppressedStruct
{
    public int M() { int h = 1; return h; }
}

[SuppressMessage("PHPMD", "ShortVariable")]
public record SuppressedRecord
{
    public int M() { int i = 1; return i; }
}

[SuppressMessage("PHPMD", "ShortVariable")]
public enum SuppressedEnum { A, B }
