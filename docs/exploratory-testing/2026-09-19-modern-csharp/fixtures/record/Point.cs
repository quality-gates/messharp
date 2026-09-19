namespace Shop.Records;

public record Point(int X, int Y);

public class Plain
{
    public Plain(int x, int y) { X = x; Y = y; }

    public int X { get; }

    public int Y { get; }
}
