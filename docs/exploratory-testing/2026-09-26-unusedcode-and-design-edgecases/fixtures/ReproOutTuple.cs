public class ReproOutTuple
{
    public void OutTuple(out int x, out int y)
    {
        (x, y) = (1, 2);
    }

    public void OutScalar(out int x, out int y)
    {
        x = 1;
        y = 2;
    }
}
