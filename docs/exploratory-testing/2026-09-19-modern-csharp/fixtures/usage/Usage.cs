namespace Shop.Usage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public abstract class Shape
{
    public abstract double Area(double scale);

    public virtual string Name(string prefix) => "shape";
}

public sealed class Square : Shape
{
    private readonly double side = 2;
    private readonly List<int> counts = new();

    public override double Area(double scale) => side * side;

    public override string Name(string prefix) => "square";

    public IEnumerable<string> Mapped(IEnumerable<int> values) => values.Select(Format);

    public Func<int, string> Formatter() => Format;

    public int Total()
    {
        using var reader = new StringReader("x");
        var point = (X: 1, Y: 2);
        var moved = point with { X = 3 };
        var text = $"{moved.X}";
        if (counts is { Count: > 0 } nonEmpty)
        {
            return nonEmpty.Count + text.Length;
        }

        return int.TryParse(reader.ReadLine(), out _) ? 1 : 0;
    }

    private string Format(int value) => value.ToString();
}
