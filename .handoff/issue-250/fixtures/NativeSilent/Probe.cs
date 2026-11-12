#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

static class Extensions {
    public static bool Contains(this IEnumerable<nint> source, int value) => false;
}
class Probe {
    static IEnumerable<nint> Gen() { Console.Read(); yield return (nint)1; }
    static IEnumerable<nint> Values() { yield return (nint)1; }
    public static bool Action(nint key) {
        var q = Gen();
        return q.Contains(key);
    }
    public static bool Pure(nint key) {
        var q = Values();
        return q.Contains(key);
    }
}
