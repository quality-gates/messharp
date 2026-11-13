using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessScalingTests
{
    private static string Chain(int depth, bool branching)
    {
        var lines = new List<string> { "public class Chain {" };
        for (var i = 0; i < depth; i++)
        {
            var first = branching ? $"M{i + 1}(value); " : "";
            lines.Add($"public object M{i}(object value) {{ {first}return M{i + 1}(value); }}");
        }
        lines.Add($"public object M{depth}(object value) {{ return value; }}");
        lines.Add("}");
        return string.Join('\n', lines);
    }

    [Fact]
    public void LayeredHelpersCalledTwiceEachDoNotScaleExponentially()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Engine.Analyze(ModelBuilder.Parse("test.cs", Chain(22, true)), new Loader().Load("explicitness"));
        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"took {watch.Elapsed}");
    }
}
