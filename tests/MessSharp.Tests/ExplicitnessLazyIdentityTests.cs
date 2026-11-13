using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>Lazy values flowing through generic identity helpers, recursion and collections (#245).</summary>
public class ExplicitnessLazyIdentityTests
{
    private const string Prelude = """
        using System;
        using System.Collections.Generic;
        static class Ext {
            public static T Id<T>(this T x) { return x; }
            public static int ForceFirst(this List<Lazy<int>> xs) { return xs[0].Value; }
        }
        """;

    public static TheoryData<string, string> Bodies => new()
    {
        { "extension-identity", "var cell = new Lazy<int>({0}); var same = cell.Id(); return same.Value;" },
        { "recursive-identity", "var cell = new Lazy<int>({0}); var same = Deep(cell, 3); return same.Value;" },
        { "recursive-chain", "var cell = Wrap(new Lazy<int>({0}), 3); return cell.Value;" },
        { "list-index", "var cells = new List<Lazy<int>>(); cells.Add(new Lazy<int>({0})); var first = cells[0]; return first.Value;" },
        { "array-foreach", "var cells = new[] { new Lazy<int>(Seven), new Lazy<int>({0}) }; var total = 0; foreach (var c in cells) { total += c.Value; } return total;" },
        { "extension-collection", "var cells = new List<Lazy<int>> { new Lazy<int>({0}) }; return cells.ForceFirst();" },
    };

    private static List<string> RunRules(string body, string callee)
    {
        var source = $$"""
            {{Prelude}}
            class Probe {
                public static int Read() { return Console.Read(); }
                public static int Seven() { return 7; }
                static Lazy<int> Deep(Lazy<int> c, int n) { if (n == 0) { return c; } return Deep(c, n - 1); }
                static Lazy<int> Wrap(Lazy<int> c, int n) {
                    if (n == 0) { return c; }
                    var next = new Lazy<int>(() => c.Value + 1);
                    return Wrap(next, n - 1);
                }
                static int Run() { {{body.Replace("{0}", callee)}} }
            }
            """;
        return Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(f => f.Method == "Run").Select(f => f.Rule.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void ActionThroughValueFlowReportsImplicitInput(string name, string body)
    {
        Assert.True(name.Length > 0);
        Assert.Equal(["ImplicitInput"], RunRules(body, "Read"));
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void PureTwinReportsNothing(string name, string body)
    {
        Assert.True(name.Length > 0);
        Assert.Empty(RunRules(body, "Seven"));
    }
}
