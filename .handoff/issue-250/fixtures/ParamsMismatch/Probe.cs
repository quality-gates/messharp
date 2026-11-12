#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

static class Extensions {
    public static bool Contains(this IEnumerable<int> source, params string[] value) { Console.WriteLine("CUSTOM"); return false; }
}
class Probe {
    static IEnumerable<int> Gen() { Console.Read(); yield return 1; }
    static IEnumerable<int> Values() { yield return 1; }
    public static bool Action(int key) {
        var q = Gen();
        return q.Contains(key);
    }
    public static bool Pure(int key) {
        var q = Values();
        return q.Contains(key);
    }
}
