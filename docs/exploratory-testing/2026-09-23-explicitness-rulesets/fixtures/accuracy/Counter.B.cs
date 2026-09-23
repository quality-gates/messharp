using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
namespace Acc;
public partial class Counter
{
    static int A1() => _hits;                                  // input: _hits (other partial file)
    static void A2() { _hits += 1; }                           // input+output
    static int A3() { var _hits = 3; return _hits; }           // clean: local shadows
    static int A4(int[] xs) => xs.Select(_hits => _hits * 2).Sum(); // clean: lambda param shadows
    static bool A5(object o) => o is int _hits && _hits > 0;   // clean: pattern shadows
    static int A6() => Max;                                    // clean: get-only
    static int A7() => Limit;                                  // input: Limit
    static void A8() { lock (Sync) { } }                       // clean: readonly
    static void A9() => Interlocked.Increment(ref _hits);      // output: writes _hits (ref)
    static string A10() => nameof(_hits);                      // clean
    static void A11(int[] a) { a[0] = 1; }                     // output: changes argument a
    static int A12(int x) { x = x + 1; return x; }             // clean
    static void A13(ref int x) => x++;                         // output: writes ref x
    static async Task A14(string p) => await File.WriteAllTextAsync(p, "");  // output: File.WriteAllTextAsync
    static void A15() => Console.Out.WriteLine("x");           // output: Console.Out
    static void A16(List<int> o) => o?.Add(1);                 // output: changes argument o (arrow)
    static void A17(Order o) { o.Lines?.Add(1); }              // output: changes argument o
    static void A18(Order o) { o?.Lines.Add(1); }              // output: changes argument o
    static void A19((int, int) t) { (_hits, Limit) = t; }      // output: writes _hits, Limit
    static int A20(Order o) { return o.Total; }                // clean
    static void A21(List<int> xs, List<int> sink) { xs.ForEach(x => sink.Add(x)); } // output: changes argument sink (lambda)
    static void A22(List<int> sink) { void Local() { sink.Add(1); } Local(); } // output: changes sink
    static int A23() => new Random(42).Next();                 // clean: seeded
    static int A24() => new System.Random().Next();            // input: new Random()
    static void A25() { if (Counter._hits > 0) Counter.Limit = 3; } // input _hits, output Limit
    class Nested { int N() => _hits; }                         // ? outer static from nested
}
public class Order { public List<int> Lines = new(); public int Total; }
public class Cache<T>
{
    static T? _value;
    public static T? Get() => Cache<T>._value;                 // input: _value (generic qualifier)
}
