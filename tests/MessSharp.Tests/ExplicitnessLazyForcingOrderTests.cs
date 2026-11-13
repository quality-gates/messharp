using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>
/// A Lazy runs its factory once. After it has surely run, forcing it again must not run it again, even if what it
/// reads has changed since. Forcing it only on some path first must not stop it counting as run once a later,
/// sure forcing happens.
/// </summary>
public class ExplicitnessLazyForcingOrderTests
{
    private static List<string> Run(string body)
    {
        var source = $$"""
            using System;
            class Probe {
                static int Run(bool flag, int[] outside) {
                    var holder = new[] { new int[1] };
                    var lz = new Lazy<int>(() => { holder[0] = outside; return 1; });
                    {{body}}
                }
            }
            """;
        return Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(f => f.Method == "Run").Select(f => f.Rule.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void ALazyThatSurelyRanDoesNotRunAgainAfterAnEarlierMaybeForcing() => Assert.Empty(Run("""
        if (flag) { var a = lz.Value; }
        var b = lz.Value;
        holder[0] = new int[1];
        var c = lz.Value;
        holder[0][0] = 1;
        return b + c;
        """));

    [Fact]
    public void ALazyForcedOnlyOnOnePathStillRunsAtTheNextForcing() => Assert.Equal(["ImplicitOutput"], Run("""
        if (flag) { var a = lz.Value; }
        holder[0] = new int[1];
        var b = lz.Value;
        holder[0][0] = 1;
        return b;
        """));
}
