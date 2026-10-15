using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessStorageExecutionTests
{
    private static List<Violation> Findings(string source, string method = "Run") =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(finding => finding.Method == method).ToList();

    private static void OutputAt(string source, int line)
    {
        var output = Assert.Single(Findings(source));
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(line, output.BeginLine);
    }

    [Theory]
    [InlineData("Remove(box[0])")]
    [InlineData("RemoveAt(0)")]
    [InlineData("RemoveRange(0, 1)")]
    [InlineData("Reverse()")]
    public void CollectionEditsMoveCallerStorageIntoTheReadSlot(string edit) =>
        OutputAt("class Probe { static void Run(List<int> values) {"
            + " var box = new List<List<int>> { new List<int>(), values }; box." + edit + ";\nbox[0].Add(9); } }", 2);

    [Theory]
    [InlineData("Remove(box[0])")]
    [InlineData("RemoveAt(0)")]
    [InlineData("RemoveRange(0, 1)")]
    [InlineData("Reverse()")]
    [InlineData("Sort()")]
    public void CollectionEditsPreservePrivateContents(string edit) =>
        Assert.Empty(Findings("class Probe { static void Run() {"
            + " var box = new List<List<int>> { new List<int>(), new List<int>() }; box." + edit + "; box[0].Add(9); } }"));

    [Theory]
    [InlineData("new Box()", "box.Items", "box.Items[0]")]
    [InlineData("new int[1][]", "box[0]", "box[0][0]")]
    public void RefLocalsWriteTheOriginalLocation(string creation, string location, string item) =>
        OutputAt("class Box { public int[] Items; } class Probe { static void Run(int[] values) {"
            + " var box = " + creation + "; ref var slot = ref " + location + "; slot = values;\n" + item + " = 9; } }", 2);

    [Theory]
    [InlineData("new Box()", "box.Items")]
    [InlineData("new int[1][]", "box[0]")]
    public void RefLocalsReadLaterWritesToTheOriginalLocation(string creation, string location) =>
        OutputAt("class Box { public int[] Items; } class Probe { static void Run(int[] values) {"
            + " var box = " + creation + "; ref var slot = ref " + location + "; " + location + " = values;\nslot[0] = 9; } }", 2);

    [Theory]
    [InlineData("new Box { Items = values }", "box.Items", "box.Items[0]")]
    [InlineData("new[] { values }", "box[0]", "box[0][0]")]
    public void RefLocalsStronglyReplaceOneSlotWithPrivateStorage(string creation, string location, string item) =>
        Assert.Empty(Findings("class Box { public int[] Items; } class Probe { static void Run(int[] values) {"
            + " var box = " + creation + "; ref var slot = ref " + location + "; slot = new int[1]; " + item + " = 9; } }"));

    [Fact]
    public void ConditionalRefReplacementRetainsTheCallerAlternative() =>
        OutputAt("class Probe { static void Run(int[] values, bool replace) { var box = new[] { values };"
            + " ref var slot = ref box[0]; if (replace) slot = new int[1];\nbox[0][0] = 9; } }", 2);

    [Fact]
    public void RefToScalarCallerElementWritesTheCaller() =>
        OutputAt("class Probe { static void Run(int[] values) { ref var slot = ref values[0];\nslot = 9; } }", 2);

    [Theory]
    [InlineData("foreach (var value in Fill(box, values)) { }")]
    [InlineData("var sequence = Fill(box, values); foreach (var value in sequence) { }")]
    [InlineData("var sequence = Fill(box, values); var copy = sequence.ToList();")]
    public void ForcingAnIteratorAppliesItsWritesBeforeLaterReads(string force) =>
        OutputAt("class Box { public int[] Items; } class Probe {"
            + " static IEnumerable<int> Fill(Box box, int[] values) { box.Items = values; yield return 1; }"
            + " static void Run(int[] values) { var box = new Box { Items = new int[1] }; " + force + "\nbox.Items[0] = 9; } }", 2);

    [Theory]
    [InlineData("var sequence = Fill(box, values);")]
    [InlineData("Action setter = () => box.Items = values;")]
    [InlineData("var lazy = new Lazy<int>(() => { box.Items = values; return 1; });")]
    public void DeferredCreationDoesNotApplyStorageWrites(string create) =>
        Assert.Empty(Findings("class Box { public int[] Items; } class Probe {"
            + " static IEnumerable<int> Fill(Box box, int[] values) { box.Items = values; yield return 1; }"
            + " static void Run(int[] values) { var box = new Box { Items = new int[1] }; " + create + " box.Items[0] = 9; } }"));

    [Fact]
    public void ForcingLazyAppliesItsWritesBeforeLaterReads() =>
        OutputAt("class Box { public int[] Items; } class Probe { static void Run(int[] values) {"
            + " var box = new Box { Items = new int[1] }; var lazy = new Lazy<int>(() => { box.Items = values; return 1; });"
            + " var value = lazy.Value;\nbox.Items[0] = 9; } }", 2);

    [Fact]
    public void LazyRunsOnceAndLaterPrivateReplacementRemainsQuiet() =>
        Assert.Empty(Findings("class Box { public int[] Items; } class Probe { static void Run(int[] values) {"
            + " var box = new Box { Items = new int[1] }; var lazy = new Lazy<int>(() => { box.Items = values; return 1; });"
            + " var first = lazy.Value; box.Items = new int[1]; var second = lazy.Value; box.Items[0] = 9; } }"));

    [Fact]
    public void IteratorArgumentsRetainTheirCreationTimeStorage() =>
        OutputAt("class Box { public int[] Items; } class Probe {"
            + " static IEnumerable<int> Fill(Box box, int[] item) { box.Items = item; yield return 1; }"
            + " static void Run(int[] values) { var box = new Box { Items = new int[1] }; var item = values;"
            + " var sequence = Fill(box, item); item = new int[1]; foreach (var value in sequence) { }\nbox.Items[0] = 9; } }", 2);

    [Fact]
    public void CompletedIterationPreservesReplacementAfterTheLastYield() =>
        Assert.Empty(Findings("class Box { public int[] Items; } class Probe {"
            + " static IEnumerable<int> Fill(Box box, int[] item) { box.Items = item; yield return 1; box.Items = new int[1]; }"
            + " static void Run(int[] values) { var box = new Box { Items = new int[1] };"
            + " foreach (var value in Fill(box, values)) { } box.Items[0] = 9; } }"));

    [Fact]
    public void QueryCallbacksReceiveStorageProducedByTheIterator() =>
        OutputAt("class Box { public int[] Items; } class Probe {"
            + " static IEnumerable<Box> Boxes(Box box) { yield return box; }"
            + " static void Run(int[] values) { var box = new Box { Items = new int[1] };"
            + " var query = Boxes(box).Select(holder => { holder.Items = values; return holder; });"
            + " foreach (var holder in query) { }\nbox.Items[0] = 9; } }", 2);

    [Fact]
    public void QueryCallbacksCanProducePrivateReplacements() =>
        Assert.Empty(Findings("class Probe { static void Run(List<int> values) {"
            + " var query = new[] { values }.Select(item => new List<int>());"
            + " var box = query.ToList(); box[0].Add(9); } }"));

    [Theory]
    [InlineData("foreach (var value in Fill(box, new int[1])) { }")]
    [InlineData("var lazy = new Lazy<int>(() => { box.Items = new int[1]; return 1; }); var value = lazy.Value;")]
    public void ForcingPrivateWritesRemainsQuiet(string force) =>
        Assert.Empty(Findings("class Box { public int[] Items; } class Probe {"
            + " static IEnumerable<int> Fill(Box box, int[] values) { box.Items = values; yield return 1; }"
            + " static void Run() { var box = new Box { Items = new int[1] }; " + force + " box.Items[0] = 9; } }"));

    [Fact]
    public void KnownDelegateContextResolvesCallbackFields() =>
        OutputAt("class Box { public int[] Items; } class Probe {"
            + " static void With(Action<Box> action, Box box) { action(box); }"
            + " static void Run(int[] values) { var box = new Box(); With(holder => holder.Items = values, box);\nbox.Items[0] = 9; } }", 2);

    [Fact]
    public void KnownDelegateContextPreservesPrivateCallbackWrites() =>
        Assert.Empty(Findings("class Box { public int[] Items; } class Probe {"
            + " static void With(Action<Box> action, Box box) { action(box); }"
            + " static void Run() { var box = new Box(); With(holder => holder.Items = new int[1], box); box.Items[0] = 9; } }"));

    [Theory]
    [InlineData("while (false) { Console.WriteLine(1); }")]
    [InlineData("for (; false; Console.WriteLine(1)) { Console.WriteLine(1); }")]
    [InlineData("var value = false ? Console.Read() : 7;")]
    public void LiteralFalsePathsDoNotContributeEffects(string statement) =>
        Assert.Empty(Findings("class Probe { static int Run() { " + statement + " return 7; } }"));

    [Theory]
    [InlineData("if (!true) {\nConsole.WriteLine(1); }")]
    [InlineData("while (true) {\nConsole.WriteLine(1); break; }")]
    public void NonliteralConditionsAndLiveLoopsRetainEffects(string statement) =>
        OutputAt("class Probe { static void Run() { " + statement + " } }", 2);
}
