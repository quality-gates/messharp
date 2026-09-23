namespace Acc;
public partial class Counter
{
    private static int _hits;
    public static int Limit { get; private set; } = 10;
    public static int Max { get; } = 5;
    private static readonly object Sync = new();
}
