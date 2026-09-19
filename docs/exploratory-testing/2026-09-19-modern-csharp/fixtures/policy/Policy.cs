namespace Shop.Policy;

public record Point(int X, int Y);

public class Handler
{
    public int Handle(int requestNumber, int unusedArgument)
    {
        var accumulatedTotal = requestNumber * 2;
        return accumulatedTotal;
    }
}
