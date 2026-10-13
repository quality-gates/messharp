using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessStorageTests
{
    private static IEnumerable<Violation> Run(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(finding => finding.Method == "Run");

    [Theory]
    [InlineData("var box = new List<List<int>>(); box.Add(values);")]
    [InlineData("var box = new List<List<int>>(); box.Insert(0, values);")]
    [InlineData("var box = new List<List<int>> { new List<int>() }; box[0] = values;")]
    [InlineData("var box = new List<List<int>> { values };")]
    [InlineData("var box = new[] { values };")]
    [InlineData("var box = new List<int>[] { values };")]
    [InlineData("var box = new List<int>[1]; box[0] = values;")]
    [InlineData("var box = new List<List<int>>(new[] { values });")]
    [InlineData("List<List<int>> box = [values];")]
    public void FreshHoldersRetainInsertedCallerStorage(string setup)
    {
        var findings = Run("class Probe { static void Run(List<int> values) { " + setup + "\nbox[0].Add(1); } }").ToList();
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(2, output.BeginLine);
        Assert.Equal("The method Run() has an implicit output: changes argument 'values'.", output.Description);
    }

    [Theory]
    [InlineData("var box = new List<List<int>>(); box.Add(new List<int>());")]
    [InlineData("var box = new List<List<int>> { new List<int>() };")]
    [InlineData("var box = new[] { new List<int>() };")]
    [InlineData("var box = new List<int>[1]; box[0] = new List<int>();")]
    [InlineData("List<List<int>> box = [new List<int>()];")]
    [InlineData("var box = new[] { values }; box[0] = new List<int>();")]
    [InlineData("var box = new List<List<int>> { values }; box.Clear(); box.Add(new List<int>());")]
    [InlineData("var box = new List<List<int>> { values }; box.Insert(0, new List<int>());")]
    [InlineData("var box = new[] { values }; var alias = box; alias[0] = new List<int>();")]
    public void PrivateContentsAndStrongReplacementRemainQuiet(string setup) =>
        Assert.Empty(Run("class Probe { static void Run(List<int> values) { " + setup + " box[0].Add(1); } }"));

    [Fact]
    public void AliasesShareHolderIdentityButDistinctAllocationsDoNot()
    {
        var findings = Run("""
            class Probe {
                static void Run(List<int> values) {
                    var box = new List<List<int>>();
                    var other = new List<List<int>>();
                    var alias = box;
                    alias.Add(values);
                    other.Add(new List<int>());
                    other[0].Add(1);
                    box[0].Add(1);
                }
            }
            """);
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(9, output.BeginLine);
    }

    [Fact]
    public void HelperInsertionsAndReturnsCarryTheSameHeap()
    {
        var findings = Run("""
            class Probe {
                static void Put(List<List<int>> box, List<int> item) { box.Add(item); }
                static List<List<int>> Build(List<int> item) { var box = new List<List<int>>(); Put(box, item); return box; }
                static void Run(List<int> values) {
                    var box = Build(values);
                    box[0].Add(1);
                }
            }
            """);
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(6, output.BeginLine);
        Assert.Equal("The method Run() has an implicit output: changes argument 'values'.", output.Description);
    }

    [Fact]
    public void HelperAllocationsAreDistinctAtDifferentCallSites()
    {
        var findings = Run("""
            class Probe {
                static List<List<int>> Build(List<int> item) => new List<List<int>> { item };
                static void Run(List<int> values) {
                    var shared = Build(values);
                    var box = Build(new List<int>());
                    box[0].Add(1);
                }
            }
            """);
        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("var box = new Holder { Value = values };")]
    [InlineData("var box = new Holder(values);")]
    [InlineData("var box = new Holder(new List<int>()); box.Value = values;")]
    public void ProvenFieldSlotsRetainCallerStorage(string setup)
    {
        var findings = Run("class Holder { public List<int> Value; public Holder() {} public Holder(List<int> item) { Value = item; } }"
            + " class Probe { static void Run(List<int> values) { " + setup + "\nbox.Value.Add(1); } }");
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(2, output.BeginLine);
    }

    [Fact]
    public void ConditionalReplacementKeepsThePossibleCallerAlias()
    {
        var findings = Run("class Probe { static void Run(List<int> values, bool replace) { var box = new[] { values };"
            + " if (replace) box[0] = new List<int>();\nbox[0].Add(1); } }");
        Assert.Equal("ImplicitOutput", Assert.Single(findings).Rule.Name);
    }

    [Fact]
    public void IteratorCreationDoesNotExecuteAnInsertion()
    {
        Assert.Empty(Run("""
            class Probe {
                static IEnumerable<int> Put(List<List<int>> box, List<int> item) { box.Add(item); yield return 1; }
                static void Run(List<int> values) {
                    var box = new List<List<int>>();
                    var sequence = Put(box, values);
                    box.Add(new List<int>());
                    box[0].Add(1);
                }
            }
            """));
    }

    [Theory]
    [InlineData("Action insert = () => box.Add(values); insert();")]
    [InlineData("Invoke(() => box.Add(values));")]
    [InlineData("Action insert = () => box.Add(values); Invoke(insert);")]
    public void InvokedCallbacksInsertIntoTheSameHolder(string statement)
    {
        var findings = Run("class Probe { static void Invoke(Action action) { action(); } static void Run(List<int> values) {"
            + " var box = new List<List<int>>(); " + statement + "\nbox[0].Add(1); } }");
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(2, output.BeginLine);
        Assert.Equal("The method Run() has an implicit output: changes argument 'values'.", output.Description);
    }

    [Theory]
    [InlineData("Action insert = () => box.Add(new List<int>()); insert();")]
    [InlineData("Invoke(() => box.Add(new List<int>()));")]
    [InlineData("Action insert = () => box.Add(new List<int>()); Invoke(insert);")]
    public void PrivateCallbackInsertionsRemainQuiet(string statement) =>
        Assert.Empty(Run("class Probe { static void Invoke(Action action) { action(); } static void Run() {"
            + " var box = new List<List<int>>(); " + statement + " box[0].Add(1); } }"));

    [Fact]
    public void ReplacingOneFieldPreservesOnlyItsSiblingAlias()
    {
        var findings = Run("""
            class Holder { public List<int> Left; public List<int> Right; }
            class Probe {
                static void Run(List<int> values) {
                    var box = new Holder { Left = values, Right = values };
                    box.Left = new List<int>();
                    box.Left.Add(1);
                    box.Right.Add(1);
                }
            }
            """);
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(7, output.BeginLine);
    }

    [Fact]
    public void ReplacingOneElementPreservesOnlyItsSiblingAlias()
    {
        var findings = Run("""
            class Probe {
                static void Run(List<int> values) {
                    var box = new[] { values, values };
                    box[0] = new List<int>();
                    box[0].Add(1);
                    box[1].Add(1);
                }
            }
            """);
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(6, output.BeginLine);
    }

    [Fact]
    public void ReplacedCallerFieldDoesNotMakeItsPrivateContentsShared()
    {
        var findings = Run("""
            class Holder { public List<int> Value; }
            class Probe {
                static void Run(Holder caller) {
                    caller.Value = new List<int>();
                    caller.Value.Add(1);
                }
            }
            """);
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(4, output.BeginLine);
    }

    [Fact]
    public void UncalledClosureDoesNotRebindItsCreator()
    {
        Assert.Empty(Run("""
            class Probe {
                static void Run(List<int> values) {
                    var alias = new List<int>();
                    Action rebind = () => alias = values;
                    alias.Add(1);
                }
            }
            """));
    }

    [Fact]
    public void QualifiedConstructorFieldIsNotShadowedByItsParameter()
    {
        var findings = Run("class Holder { public List<int> Value; public Holder(List<int> Value) { this.Value = Value; } }"
            + " class Probe { static void Run(List<int> values) { var box = new Holder(values);\nbox.Value.Add(1); } }");
        Assert.Equal("ImplicitOutput", Assert.Single(findings).Rule.Name);
    }
}
