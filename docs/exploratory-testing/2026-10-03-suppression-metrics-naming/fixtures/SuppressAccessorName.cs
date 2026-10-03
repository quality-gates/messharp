using System.Diagnostics.CodeAnalysis;

namespace Demo;

public class SuppressAccessorName
{
    private int _count;

    [SuppressMessage("PHPMD", "CamelCaseMethodName")]
    public int Count
    {
        get { return _count; }
    }
}
