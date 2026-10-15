using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessConstructorContextTests
{
    [Fact]
    public void ContextualConstructorTypesPreserveUnseededRandomInput()
    {
        var findings = Engine.Analyze(ModelBuilder.Parse("test.cs", """
            using System;
            class Probe {
                static Random Make() => new();
                static Random Seeded() => new(1);
                static Random Assign() {
                    Random value;
                    value = new();
                    return value;
                }
                static Random Quiet() {
                    Random value;
                    value = new(1);
                    return value;
                }
            }
            """), new Loader().Load("explicitness"));
        Assert.Equal(new[] { ("Make", "ImplicitInput", 3), ("Assign", "ImplicitInput", 7) },
            findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }
}
