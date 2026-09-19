namespace Shop.Gauges;

public partial class Gauge
{
    public int Read() => reading;

    public void Set(int value) => reading = value;
}
