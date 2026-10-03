public class ForeachShort
{
    public int Sum(int[] items)
    {
        int total = 0;
        foreach (var v in items) { total += v; }
        for (int i = 0; i < 1; i++) { total += i; }
        return total;
    }
}
