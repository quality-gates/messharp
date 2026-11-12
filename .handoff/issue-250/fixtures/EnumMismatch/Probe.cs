#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
enum Key { One = 1 }
static class Extensions {
    public static bool Contains(this IEnumerable<Key> source, int value) { Console.WriteLine("CUSTOM"); return false; }
}
class Probe {
    static IEnumerable<Key> Gen() { Console.Read(); yield return Key.One; }
    static IEnumerable<Key> Values() { yield return Key.One; }
    public static bool Action(Key key) {
        var q = Gen();
        return q.Contains(key);
    }
    public static bool Pure(Key key) {
        var q = Values();
        return q.Contains(key);
    }
}
