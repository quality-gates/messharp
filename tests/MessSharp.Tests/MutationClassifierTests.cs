using MessSharp.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MessSharp.Tests;

/// <summary>One classifier decides which syntax forms write a target and whether they also read it (#231).</summary>
public class MutationClassifierTests
{
    private static List<string> Classified(string statements)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {statements} }} }}");
        return tree.GetRoot().DescendantNodes().SelectMany(MutationClassifier.Classify)
            .Select(m => $"{m.Target}:{m.Form}:{(m.IsPureWrite ? "write" : "readwrite")}").ToList();
    }

    [Fact]
    public void Simple_assignment_is_a_pure_write() =>
        Assert.Equal(["x:Assignment:write"], Classified("x = 1;"));

    [Fact]
    public void Compound_assignment_reads_and_writes() =>
        Assert.Equal(["x:CompoundAssignment:readwrite"], Classified("x += 1;"));

    [Theory]
    [InlineData("x++;")]
    [InlineData("++x;")]
    [InlineData("x--;")]
    [InlineData("--x;")]
    public void Increment_and_decrement_read_and_write(string statement) =>
        Assert.Equal(["x:IncrementOrDecrement:readwrite"], Classified(statement));

    [Fact]
    public void Out_argument_is_a_pure_write() =>
        Assert.Equal(["x:OutArgument:write"], Classified("F(out x);"));

    [Fact]
    public void Ref_argument_reads_and_writes() =>
        Assert.Equal(["x:RefArgument:readwrite"], Classified("F(ref x);"));

    [Fact]
    public void Declaration_out_argument_writes_nothing_existing() =>
        Assert.Empty(Classified("F(out var x);"));

    [Fact]
    public void Tuple_targets_are_flattened_through_nested_levels() =>
        Assert.Equal(["a:Assignment:write", "b:Assignment:write", "c:Assignment:write", "d:Assignment:write"],
            Classified("(a, (b, (c, d))) = t;"));

    [Fact]
    public void Discards_are_targets_like_any_other_expression() =>
        Assert.Equal(["a:Assignment:write", "_:Assignment:write"], Classified("(a, _) = t;"));

    [Fact]
    public void Nested_var_declaration_in_deconstruction_is_not_an_identifier_target() =>
        Assert.Equal(["a:Assignment:write"], Classified("(a, (var b, var c)) = t;").Where(t => t.StartsWith("a:")).ToList());

    [Fact]
    public void Object_initializer_assignment_is_flagged_as_such() =>
        Assert.Equal(["Count:InitializerAssignment:write"], Classified("var o = new T { Count = 1 };"));

    [Fact]
    public void Member_access_targets_are_kept_whole() =>
        Assert.Equal(["this._f:Assignment:write"], Classified("this._f = 1;"));

    [Fact]
    public void Reads_are_not_mutations() =>
        Assert.Empty(Classified("var y = x + F(x);"));

    [Fact]
    public void Written_names_cover_every_identifier_target()
    {
        var body = CSharpSyntaxTree.ParseText("class C { void M() { a = 1; b += 1; c++; F(out d); F(ref e); (f, (g, _)) = t; this.h = 1; } }").GetRoot();
        Assert.Equal(["_", "a", "b", "c", "d", "e", "f", "g"], MutationClassifier.CollectWrittenNames(body).OrderBy(n => n, StringComparer.Ordinal).ToList());
        Assert.Equal(["_", "a", "d", "f", "g"], MutationClassifier.CollectWrittenNames(body, pureWritesOnly: true).OrderBy(n => n, StringComparer.Ordinal).ToList());
    }

    [Theory]
    [InlineData("x = 1;", "x", true)]
    [InlineData("x += 1;", "x", false)]
    [InlineData("x++;", "x", false)]
    [InlineData("F(out x);", "x", true)]
    [InlineData("F(ref x);", "x", false)]
    [InlineData("(a, (x, b)) = t;", "x", true)]
    [InlineData("var o = new T { x = 1 };", "x", false)]
    [InlineData("y = x;", "x", false)]
    [InlineData("y = (x, b);", "x", false)]
    public void Pure_write_target_accepts_only_pure_writes_outside_initializers(string statement, string name, bool expected)
    {
        var root = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {statement} }} }}").GetRoot();
        var target = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>().First(i => i.Identifier.Text == name);
        Assert.Equal(expected, MutationClassifier.IsPureWriteTarget(target));
    }
}
