namespace Shop.Dials;

public partial class Dial
{
    private int reading;

    private int Twice() => reading * 2;
}

public partial class Dial
{
    public int Read() => Twice();

    public void Set(int value) => reading = value;
}
