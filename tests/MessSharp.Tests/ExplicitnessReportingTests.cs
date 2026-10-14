using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessReportingTests
{
    private static List<(string, string, int)> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Select(f => (f.Method, f.Rule.Name, f.BeginLine)).Distinct()
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal)
            .ThenBy(f => f.Name, StringComparer.Ordinal).ToList();

    [Theory]
    [InlineData("Console.Read()", 5)]
    [InlineData("1", 6)]
    public void OneOwnerReportsOneInputAtTheFirstSite(string value, int line)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                public static int seed;
                static int Run() {
                    int a = {{value}};
                    int b = seed;
                    return a + b;
                }
            }
            """);
        Assert.Equal([("Run", "ImplicitInput", line)], findings);
    }

    [Theory]
    [InlineData("Console.Write(1);", 5)]
    [InlineData("var a = 1;", 6)]
    public void OneOwnerReportsOneOutputAtTheFirstSite(string statement, int line)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                public static int seed;
                static void Run() {
                    {{statement}}
                    seed = 2;
                }
            }
            """);
        Assert.Equal([("Run", "ImplicitOutput", line)], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void OutArgumentWritesStaticStorageWithoutReadingIt(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static Lazy<int> shared = new(() => 7);
                static void Make(out Lazy<int> lazy) {
                    lazy = new(() => {{value}});
                }
                static int Run() {
                    Make(out shared);
                    return shared.Value;
                }
            }
            """);
        var expected = new List<(string, string, int)>();
        if (input) expected.Add(("<lazy>", "ImplicitInput", 5));
        expected.AddRange([("Make", "ImplicitOutput", 5), ("Run", "ImplicitOutput", 8), ("Run", "ImplicitInput", 9)]);
        Assert.Equal(expected, findings);
    }

    [Theory]
    [InlineData("out", false)]
    [InlineData("ref", true)]
    public void OnlyRefArgumentsReadStaticStorage(string modifier, bool reads)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int seed;
                static void Set({{modifier}} int x) {
                    x = 1;
                }
                static int Run() {
                    Set({{modifier}} seed);
                    return 7;
                }
            }
            """);
        var expected = new List<(string, string, int)> { ("Set", "ImplicitOutput", 5) };
        if (reads) expected.Add(("Run", "ImplicitInput", 8));
        expected.Add(("Run", "ImplicitOutput", 8));
        Assert.Equal(expected, findings);
    }

    [Theory]
    [InlineData("seed = 1;", "writes static member 'seed'", false)]
    [InlineData("Set(out seed);", "writes static member 'seed'", false)]
    [InlineData("holder.field = 1;", "changes the object in static member 'holder'", false)]
    [InlineData("Set(out holder.field);", "changes the object in static member 'holder'", false)]
    [InlineData("int.TryParse(s, out seed);", "writes static member 'seed'", true)]
    [InlineData("Set(out var local);", null, false)]
    [InlineData("Set(out s);", null, false)]
    public void OutArgumentsWriteLikeAssignments(string statement, string? output, bool unknown)
    {
        var violations = Engine.Analyze(ModelBuilder.Parse("test.cs", $$"""
            using System;
            class Box { public int field; }
            class Probe {
                static int seed;
                static Box holder;
                static void Set(out int x) {
                    x = 1;
                }
                static void Run(string s) {
                    {{statement}}
                }
            }
            """), new Loader().Load("explicitness")).Where(v => v.Method == "Run").ToList();
        var expected = new List<(string, int, string)>();
        if (output is not null) expected.Add(("ImplicitOutput", 10, output));
        if (unknown) expected.Add(("UnknownEffect", 10, "unresolved call 'int.TryParse'"));
        Assert.Equal(expected, violations.Select(v => (v.Rule.Name, v.BeginLine, (string)v.Args[2])).OrderBy(v => v.Name, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("E += () => { };", "Console.Read();", "", true)]
    [InlineData("E += () => { };", "_ = value;", "", false)]
    [InlineData("Probe.E += () => { };", "Console.Read();", "", true)]
    [InlineData("E -= () => { };", "", "Console.Read();", true)]
    [InlineData("E -= () => { };", "Console.Read();", "", false)]
    public void CustomEventAccessorsRunOnSubscription(string subscription, string add, string remove, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static event Action E {
                    add {
                        {{add}}
                    }
                    remove {
                        {{remove}}
                    }
                }
                static int Run() {
                    {{subscription}}
                    return 7;
                }
            }
            """);
        var accessor = add.Contains("Console") ? ("add_E", "ImplicitInput", 5) : ("remove_E", "ImplicitInput", 8);
        var expected = new List<(string, string, int)>();
        if (add.Contains("Console") || remove.Contains("Console")) expected.Add(accessor);
        if (input) expected.Add(("Run", "ImplicitInput", 12));
        Assert.Equal(expected, findings);
    }

    [Theory]
    [InlineData("this.E += () => { };", true)]
    [InlineData("other.E += () => { };", true)]
    public void InstanceEventSubscriptionRunsTheAccessor(string subscription, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                event Action E {
                    add {
                        Console.Read();
                    }
                    remove {
                    }
                }
                int Run(Probe other) {
                    {{subscription}}
                    return 7;
                }
            }
            """);
        Assert.Equal(input ? [("add_E", "ImplicitInput", 5), ("Run", "ImplicitInput", 11)] : [], findings);
    }

    [Theory]
    [InlineData("Console.Read();", true)]
    [InlineData("", false)]
    public void AccessorInvokingTheHandlerResolvesTheSubscribedCallable(string handler, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static event Action E {
                    add {
                        value();
                    }
                    remove {
                    }
                }
                static int Run() {
                    E += () => { {{handler}} };
                    return 7;
                }
            }
            """);
        var expected = new List<(string, string, int)> { ("add_E", "UnknownEffect", 5) };
        if (input) expected.AddRange([("<lambda>", "ImplicitInput", 11), ("Run", "ImplicitInput", 11)]);
        Assert.Equal(expected, findings);
    }

    [Theory]
    [InlineData("", "Console.Read();", true)]
    [InlineData("this.", "Console.Read();", true)]
    [InlineData("base.", "Console.Read();", true)]
    [InlineData("", "_ = value;", false)]
    [InlineData("base.", "_ = value;", false)]
    public void InheritedEventAccessorRunsOnSubscription(string receiver, string add, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Base {
                public event Action E {
                    add {
                        {{add}}
                    }
                    remove {
                    }
                }
            }
            class Derived : Base {
                int Run() {
                    {{receiver}}E += () => { };
                    return 7;
                }
            }
            """);
        Assert.Equal(input ? [("add_E", "ImplicitInput", 5), ("Run", "ImplicitInput", 13)] : [], findings);
    }

    [Theory]
    [InlineData("virtual", true)]
    [InlineData("", false)]
    public void ReplaceableEventAccessorIsUnknownAtTheSubscription(string modifier, bool unknown)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                public {{modifier}} event Action E {
                    add {
                    }
                    remove {
                    }
                }
                int Run() {
                    E += () => { };
                    return 7;
                }
            }
            """);
        Assert.Equal(unknown ? [("Run", "UnknownEffect", 10)] : [], findings);
    }

    [Theory]
    [InlineData("handlers = value;", true)]
    [InlineData("Action local = value;", false)]
    public void AccessorStoringTheHandlerWritesThroughTheSubscription(string add, bool output)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static Action handlers;
                static event Action E {
                    add {
                        {{add}}
                    }
                    remove {
                    }
                }
                static int Run() {
                    E += () => { };
                    return 7;
                }
            }
            """);
        Assert.Equal(output ? [("add_E", "ImplicitOutput", 6), ("Run", "ImplicitOutput", 12)] : [], findings);
    }

    [Theory]
    [InlineData("static event Action E;", "E", true)]
    [InlineData("static event Action E;", "local", false)]
    public void FieldLikeEventSubscriptionWritesTheEvent(string declaration, string target, bool output)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                {{declaration}}
                static int Run() {
                    Action local = null;
                    {{target}} += () => { };
                    return 7;
                }
            }
            """);
        Assert.Equal(output ? [("Run", "ImplicitInput", 6), ("Run", "ImplicitOutput", 6)] : [], findings);
    }

    [Theory]
    [InlineData("override", true)]
    [InlineData("abstract override", true)]
    [InlineData("new virtual", true)]
    [InlineData("sealed override", false)]
    [InlineData("new", false)]
    public void ReplaceableFieldLikeEventIsUnknownAtTheSubscription(string modifiers, bool unknown)
    {
        var findings = Analyze($$"""
            using System;
            abstract class Base {
                public virtual event Action E;
            }
            abstract class Probe : Base {
                public {{modifiers}} event Action E;
                int Run() {
                    E += () => { };
                    return 7;
                }
            }
            """);
        Assert.Equal([("Run", unknown ? "UnknownEffect" : "ImplicitOutput", 8)], findings);
    }

    [Theory]
    [InlineData("sealed override", false)]
    [InlineData("override", true)]
    public void SealedEventAccessorRunsOnSubscription(string modifiers, bool unknown)
    {
        var findings = Analyze($$"""
            using System;
            abstract class Base {
                public abstract event Action E;
            }
            class Probe : Base {
                public {{modifiers}} event Action E {
                    add {
                        Console.Read();
                    }
                    remove {
                    }
                }
                int Run() {
                    E += () => { };
                    return 7;
                }
            }
            """);
        Assert.Equal(unknown ? [("add_E", "ImplicitInput", 8), ("Run", "UnknownEffect", 14)]
            : [("add_E", "ImplicitInput", 8), ("Run", "ImplicitInput", 14)], findings);
    }

    [Theory]
    [InlineData("Console.Read();", true)]
    [InlineData("", false)]
    public void SubscriptionInsideALambdaRunsTheAccessorWhenTheLambdaRuns(string add, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static event Action E {
                    add {
                        {{add}}
                    }
                    remove {
                    }
                }
                static int Run() {
                    Action subscribe = () => E += () => { };
                    subscribe();
                    return 7;
                }
            }
            """);
        Assert.Equal(input ? [("add_E", "ImplicitInput", 5), ("<lambda>", "ImplicitInput", 11), ("Run", "ImplicitInput", 12)]
            : [], findings);
    }
}
