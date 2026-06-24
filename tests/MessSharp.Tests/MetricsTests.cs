using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MetricsCalc = MessSharp.Metrics.Metrics;
using Xunit;

namespace MessSharp.Tests;

/// <summary>
/// Pins metric values to phpmd 2.15.0 reference output (CCN=12, NPath=324).
/// The reference C# function is a one-for-one translation of the PHP snippet
/// validated in messgo's metrics_test.go: same decision points, same boolean
/// operators, same loop, same switch with three case labels, same ifs.
/// </summary>
public class MetricsTests
{
    // C# translation of messgo's referenceFunc:
    //   if (a && b)            => 1 if + 1 &&  => CCN+2
    //   if (a || b)            => 1 if + 1 ||  => CCN+2
    //   for + inner if         => 1 for + 1 if => CCN+2
    //   switch 3 cases         => 3 case labels => CCN+3
    //   if (d > 0)             => CCN+1
    //   if (e > 0)             => CCN+1
    //   base = 1               => total = 12 ✓
    private const string ReferenceMethod = @"
class C {
    int HighComplexity(int a, int b, int c, int d, int e) {
        int x = 0;
        if (a > 0 && b > 0) {
            x++;
        }
        if (a > 1 || b > 1) {
            x++;
        }
        for (int i = 0; i < a; i++) {
            if (i % 2 == 0) {
                x++;
            }
        }
        switch (c) {
            case 1:
                x++;
                break;
            case 2:
                x++;
                break;
            case 3:
                x++;
                break;
        }
        if (d > 0) {
            x++;
        }
        if (e > 0) {
            x++;
        }
        return x;
    }
}";

    private static BlockSyntax GetMethodBody(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return method.Body!;
    }

    [Fact]
    public void CyclomaticComplexity_ReferenceFunction_Returns12()
    {
        var body = GetMethodBody(ReferenceMethod);
        Assert.Equal(12, MetricsCalc.CyclomaticComplexity(body));
    }

    [Fact]
    public void NPathComplexity_ReferenceFunction_Returns324()
    {
        var body = GetMethodBody(ReferenceMethod);
        Assert.Equal(324, MetricsCalc.NPathComplexity(body));
    }

    [Fact]
    public void CyclomaticComplexity_EmptyMethod_Returns1()
    {
        var body = GetMethodBody("class C { void F() { } }");
        Assert.Equal(1, MetricsCalc.CyclomaticComplexity(body));
    }

    [Fact]
    public void NPathComplexity_Linear_Returns1()
    {
        var body = GetMethodBody("class C { void F() { int a = 1; int b = 2; } }");
        Assert.Equal(1, MetricsCalc.NPathComplexity(body));
    }

    [Fact]
    public void NPathComplexity_SingleIfNoElse_Returns2()
    {
        var body = GetMethodBody("class C { void F(int a) { if (a > 0) { } } }");
        Assert.Equal(2, MetricsCalc.NPathComplexity(body));
    }

    [Fact]
    public void NPathComplexity_IfWithElse_Returns2()
    {
        var body = GetMethodBody("class C { void F(int a) { if (a > 0) { } else { } } }");
        Assert.Equal(2, MetricsCalc.NPathComplexity(body));
    }

    [Fact]
    public void NPathComplexity_IfWithAnd_Returns3()
    {
        var body = GetMethodBody("class C { void F(int a, int b) { if (a > 0 && b > 0) { } } }");
        Assert.Equal(3, MetricsCalc.NPathComplexity(body));
    }

    [Fact]
    public void NPathComplexity_TwoSequentialIfs_Returns4()
    {
        var body = GetMethodBody("class C { void F(int a) { if (a > 0) {} if (a > 1) {} } }");
        Assert.Equal(4, MetricsCalc.NPathComplexity(body));
    }

    [Fact]
    public void CyclomaticComplexity_NullBody_Returns1()
    {
        Assert.Equal(1, MetricsCalc.CyclomaticComplexity(null));
    }

    [Fact]
    public void NPathComplexity_NullBody_Returns1()
    {
        Assert.Equal(1, MetricsCalc.NPathComplexity(null));
    }

    private static Microsoft.CodeAnalysis.SyntaxNode GetClassNode(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        return root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
    }

    [Fact]
    public void LinesOfCode_SingleLineNode_Returns1()
    {
        var node = GetClassNode("class C { }");
        Assert.Equal(1, MetricsCalc.LinesOfCode(node));
    }

    [Fact]
    public void LinesOfCode_MultilineNode_ReturnsCorrectCount()
    {
        var src = @"class C {
    void F() {
    }
}";
        var node = GetClassNode(src);
        Assert.Equal(4, MetricsCalc.LinesOfCode(node));
    }

    [Fact]
    public void EffectiveLinesOfCode_SkipsBlankLinesAndComments()
    {
        var src = @"class C {
    // This is a comment

    /*
     * This is a block comment
     */
    void F() {
        int a = 1; // code here
    }
}";
        var node = GetClassNode(src);
        Assert.Equal(5, MetricsCalc.EffectiveLinesOfCode(node, src));
    }

    [Fact]
    public void EffectiveLinesOfCode_MultipleCommentsOnSameLine_HandledCorrectly()
    {
        var src = @"class C {
    void F() {
        /* block */ int a = 1; /* block2 */
        int b = 2; // inline comment
    }
}";
        var node = GetClassNode(src);
        Assert.Equal(6, MetricsCalc.EffectiveLinesOfCode(node, src));
    }

    [Fact]
    public void EffectiveLinesOfCode_PlainSlash_NotComment()
    {
        var src = @"class C {
    int a = 10 / 2;
}";
        var node = GetClassNode(src);
        Assert.Equal(3, MetricsCalc.EffectiveLinesOfCode(node, src));
    }
}

