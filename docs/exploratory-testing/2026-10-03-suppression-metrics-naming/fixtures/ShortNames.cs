using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo;

public class ShortNames
{
    public int Locals(int[] items, object obj)
    {
        int a = 1;                                  // 11 local
        foreach (var b in items) { a += b; }        // 12 foreach
        for (int c = 0; c < 2; c++) { a += c; }     // 13 for
        var (d, e) = (1, 2);                        // 14 deconstruction
        if (obj is int f) { a += f; }               // 15 is pattern
        int.TryParse("1", out var g);               // 16 out var
        try { a++; } catch (Exception h) { _ = h; } // 17 catch
        using var i = new System.IO.MemoryStream(); // 18 using
        return a + d + e + g + (int)i.Length;
    }

    public int Lambdas(int[] items)
    {
        var sum = items.Where(j => j > 0).Sum();    // 23 lambda param
        Func<int, int, int> add = (k, l) => k + l;  // 24 lambda params
        var query = from m in items select m;       // 25 query range variable
        int Local(int n) => n;                      // 26 local function param
        return sum + add(1, 2) + query.Count() + Local(1);
    }

    public void Param(int o) { _ = o; }             // 30 method param
}
