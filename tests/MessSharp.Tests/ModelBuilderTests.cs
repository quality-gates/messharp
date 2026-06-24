using MessSharp.Model;
using Xunit;

namespace MessSharp.Tests;

public class ModelBuilderTests
{
    [Fact]
    public void ParsesClass_ExtractsNameAndLines()
    {
        var src = @"
namespace Foo;
public class MyClass {
    public void DoWork() { }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Single(sf.Classes);
        var cls = sf.Classes[0];
        Assert.Equal("MyClass", cls.Name);
        Assert.Equal("class", cls.NodeType);
        Assert.True(cls.Exported);
        Assert.Equal("Foo", sf.Namespace);
    }

    [Fact]
    public void ParsesInterface_ExtractsNameAndMethods()
    {
        var src = @"
public interface IMyInterface {
    void Method1();
    int Method2(string s);
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Single(sf.Interfaces);
        var iface = sf.Interfaces[0];
        Assert.Equal("IMyInterface", iface.Name);
        Assert.Equal(2, iface.Methods.Count);
        Assert.Equal("Method1", iface.Methods[0].Name);
        Assert.Equal("Method2", iface.Methods[1].Name);
    }

    [Fact]
    public void ParsesStruct_NodeTypeIsStruct()
    {
        var src = "public struct Point { public int X; public int Y; }";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Single(sf.Classes);
        Assert.Equal("struct", sf.Classes[0].NodeType);
    }

    [Fact]
    public void ParsesRecord_NodeTypeIsRecord()
    {
        var src = "public record Person(string Name, int Age);";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Single(sf.Classes);
        Assert.Equal("record", sf.Classes[0].NodeType);
    }

    [Fact]
    public void ParsesFields_IncludingAutoProperties()
    {
        var src = @"
public class Foo {
    private int _x;
    public string Name { get; set; }
    protected bool _flag;
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var cls = sf.Classes[0];
        // _x and _flag are regular fields; Name is auto-property
        Assert.Equal(3, cls.Fields.Count);
        var ap = cls.Fields.First(f => f.Name == "Name");
        Assert.True(ap.IsAutoProperty);
    }

    [Fact]
    public void ParsesConstants()
    {
        var src = @"
public class Foo {
    public const int MaxItems = 100;
    private const string DefaultName = ""test"";
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var cls = sf.Classes[0];
        Assert.Equal(2, cls.Constants.Count);
        Assert.Contains(cls.Constants, c => c.Name == "MaxItems");
    }

    [Fact]
    public void ParsesBaseTypes()
    {
        var src = "public class Foo : Bar, IBaz { }";
        var sf = ModelBuilder.Parse("test.cs", src);
        var cls = sf.Classes[0];
        Assert.Contains("Bar", cls.BaseTypes);
        Assert.Contains("IBaz", cls.BaseTypes);
    }

    [Fact]
    public void ParsesMethod_WithParameters()
    {
        var src = @"
public class Foo {
    public int Add(int a, int b) { return a + b; }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var method = sf.Classes[0].Methods[0];
        Assert.Equal("Add", method.Name);
        Assert.Equal(2, method.Parameters.Count);
        Assert.Equal("a", method.Parameters[0].Name);
        Assert.Equal("int", method.Parameters[0].Type);
    }

    [Fact]
    public void ParsesConstructor_IsMarked()
    {
        var src = @"
public class Foo {
    public Foo(int x) { }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var ctor = sf.Classes[0].Methods[0];
        Assert.True(ctor.IsConstructor);
        Assert.Equal("Foo", ctor.Name);
    }

    [Fact]
    public void LineNumbers_Are1Based()
    {
        var src = "class Foo {\n    void Bar() { }\n}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal(1, sf.Classes[0].Line);
    }

    [Fact]
    public void AllMethods_ContainsMethodsFromAllClasses()
    {
        var src = @"
class A { void M1() {} }
class B { void M2() {} void M3() {} }";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal(3, sf.AllMethods.Count);
    }
}
