using System.Collections.Generic;
namespace Acc;
public enum Color { Red, Blue }
public class Cart(string owner)
{
    private readonly List<int> _items = new();
    private int _total;
    public Color Color { get; set; }
    public int Count { get => _items.Count; }                  // S1 input _items (accessor)
    public int Total { get { return _total; } set { _total = value; } } // S2 input _total / output _total
    public string Owner() => owner;                            // S3 input owner (primary ctor)
    public void Add(int x) { _items.Add(x); _total += x; }     // S4 output _items, _total; input _total
    public bool IsRed() => Color == Color.Red;                 // S5 input Color
    public int Pure(int a, int b) => a + b;                    // S6 clean
    public void Reset() => this._items.Clear();                // S7 output _items (arrow)
    public Cart() : this("x") { _total = 1; }                  // S8 clean (constructor)
    public static int Twice(int x) => x * 2;                   // S9 clean (static)
    public int Local() { int _total = 5; return _total; }      // S10 clean (shadow)
    public Cart With(int t) => new Cart { _total = t };        // S11 ? initializer on another instance
}
