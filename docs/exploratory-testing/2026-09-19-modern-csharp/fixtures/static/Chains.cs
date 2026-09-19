namespace Shop.Static;

using System.Collections.Generic;
using System.Linq;

public class Cart
{
    public List<int> Items { get; } = new();
}

public class Checkout
{
    public int Count(Cart cart) => cart.Items.Count();

    public void Add(Cart cart) => cart.Items.Add(1);

    public int Control(int value) => System.Math.Abs(value);
}
