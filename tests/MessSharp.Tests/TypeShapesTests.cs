using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules.CleanCode;
using MessSharp.Rules.Design;
using MessSharp.Rules.Naming;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MessSharp.Tests;

public class TypeShapesTests
{
    [Theory]
    [InlineData("bool", true)]
    [InlineData("Boolean", true)]
    [InlineData("System.Boolean", true)]
    [InlineData("global::System.Boolean", true)]
    [InlineData("bool?", true)]
    [InlineData("System.Boolean?", true)]
    [InlineData("Nullable<bool>", true)]
    [InlineData("Nullable<Boolean>", true)]
    [InlineData("System.Nullable<bool>", true)]
    [InlineData("Nullable<System.Boolean>", true)]
    [InlineData("System.Nullable<System.Boolean>", true)]
    [InlineData("int", false)]
    [InlineData("int?", false)]
    [InlineData("Nullable<int>", false)]
    [InlineData("Other.Boolean", false)]
    [InlineData("Task<bool>", false)]
    [InlineData("bool[]", false)]
    public void IsBoolean_ClassifiesSpellings(string type, bool expected) =>
        Assert.Equal(expected, TypeShapes.IsBoolean(SyntaxFactory.ParseTypeName(type)));

    [Fact]
    public void IsBoolean_NullType_IsFalse() => Assert.False(TypeShapes.IsBoolean(null));

    [Theory]
    [InlineData("void", true)]
    [InlineData("int", false)]
    public void IsVoid_ClassifiesVoid(string type, bool expected) =>
        Assert.Equal(expected, TypeShapes.IsVoid(SyntaxFactory.ParseTypeName(type)));

    [Theory]
    [InlineData("Task", true)]
    [InlineData("ValueTask", true)]
    [InlineData("System.Threading.Tasks.Task", true)]
    [InlineData("Task<int>", false)]
    [InlineData("int", false)]
    public void IsTaskWithoutResult_ClassifiesTasks(string type, bool expected) =>
        Assert.Equal(expected, TypeShapes.IsTaskWithoutResult(SyntaxFactory.ParseTypeName(type)));

    [Theory]
    [InlineData("Customer", "Customer")]
    [InlineData("System.Threading.Tasks.Task<Customer>", "Task,Customer")]
    [InlineData("Dictionary<string, List<Order>>", "Dictionary,string?,List,Order")]
    [InlineData("(Alpha, Beta)[]", "Alpha,Beta")]
    [InlineData("Gamma?", "Gamma")]
    [InlineData("global::Acme.Delta", "Delta")]
    [InlineData("int", "")]
    public void NamedTypes_ExtractsConstituentsWithoutQualifiers(string type, string expected)
    {
        var names = TypeShapes.NamedTypes(SyntaxFactory.ParseTypeName(type)).Where(n => n != "string").ToList();
        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(n => n != "string?"), names);
    }

    [Fact]
    public void NamedTypes_Null_IsEmpty() => Assert.Empty(TypeShapes.NamedTypes(null));

    private static BaseRule Rule<T>(string name, Dictionary<string, string>? props = null) where T : BaseRule, new() =>
        new T
        {
            Name = name,
            Message = "m",
            Priority = 3,
            SetName = "t",
            ExternalUrl = "",
            Description = "",
            Since = "0.1",
            RuleProps = new Properties(props),
        };

    [Theory]
    [InlineData("System.Nullable<bool>")]
    [InlineData("Nullable<System.Boolean>")]
    [InlineData("System.Nullable<System.Boolean>")]
    public void BooleanArgumentFlag_NullableWrapperSpellings_Fire(string type)
    {
        var v = Engine.Analyze($"public class C {{ public void M({type} flag) {{ }} }}",
            [Rule<BooleanArgumentFlagRule>("BooleanArgumentFlag")]);
        Assert.Contains(v, x => x.Rule.Name == "BooleanArgumentFlag");
    }

    [Theory]
    [InlineData("System.Nullable<bool>")]
    [InlineData("Nullable<System.Boolean>")]
    [InlineData("bool?")]
    [InlineData("System.Boolean")]
    public void BooleanGetMethodName_BooleanSpellings_Fire(string type)
    {
        var v = Engine.Analyze($"public class C {{ public {type} GetReady() => default; }}",
            [Rule<BooleanGetMethodNameRule>("BooleanGetMethodName")]);
        Assert.Contains(v, x => x.Rule.Name == "BooleanGetMethodName");
    }

    [Fact]
    public void BooleanRules_NonBoolean_Silent()
    {
        var v = Engine.Analyze("public class C { public int? GetCount() => 0; public void M(int? n, Nullable<int> m) { } }",
            [Rule<BooleanGetMethodNameRule>("BooleanGetMethodName"), Rule<BooleanArgumentFlagRule>("BooleanArgumentFlag")]);
        Assert.Empty(v);
    }

    [Fact]
    public void CouplingBetweenObjects_CountsNestedTypes_ExcludesBuiltinsAndQualifiers()
    {
        const string src = """
            public class C
            {
                private System.Collections.Generic.Dictionary<string, List<Aa>> f;
                private (Bb, int)[] g;
                private Cc? h;
                public System.Threading.Tasks.Task<Dd> M(global::Acme.Ee p) => null;
                public void N() { var x = new Ff<Gg>(); }
            }
            """;
        var rule = Rule<CouplingBetweenObjectsRule>("CouplingBetweenObjects",
            new Dictionary<string, string> { ["maximum"] = "1" });
        var v = Engine.Analyze(src, [rule]);
        // Dictionary, List, Aa, Bb, Cc, Dd, Ee, Ff, Gg  (Task, string, int and qualifiers excluded)
        Assert.Contains(v, x => x.Rule.Name == "CouplingBetweenObjects" && x.Args.Contains(9));
    }
}
