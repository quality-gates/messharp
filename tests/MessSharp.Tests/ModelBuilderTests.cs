using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        Assert.Single(cls.Methods);
        Assert.Equal("MyClass", cls.Methods[0].DeclaringTypeName);
        Assert.Same(cls, cls.Methods[0].Class);
        Assert.Null(cls.Methods[0].Interface);
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
        Assert.Same(iface, iface.Methods[0].Interface);
        Assert.Null(iface.Methods[0].Class);
        Assert.Equal("IMyInterface", iface.Methods[0].DeclaringTypeName);
        Assert.Equal("Method2", iface.Methods[1].Name);
        Assert.Same(iface, iface.Methods[1].Interface);
        Assert.Null(iface.Methods[1].Class);
        Assert.Equal("IMyInterface", iface.Methods[1].DeclaringTypeName);
        Assert.True(iface.Methods[0].Exported);
        Assert.True(iface.Methods[1].Exported);
    }

    [Fact]
    public void ParsesInterface_MethodAccessibility()
    {
        var src = @"
public interface IPublicInterface {
    void ImplicitPublic();
    public void ExplicitPublic();
    private void ExplicitPrivate() { }
    internal void ExplicitInternal();
}

interface IInternalInterface {
    void ImplicitInternal();
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var pubIface = sf.Interfaces[0];
        Assert.True(pubIface.Exported);
        Assert.True(pubIface.Methods[0].Exported);
        Assert.False(pubIface.Methods[0].IsPrivate);

        Assert.True(pubIface.Methods[1].Exported);
        Assert.False(pubIface.Methods[1].IsPrivate);

        Assert.False(pubIface.Methods[2].Exported);
        Assert.True(pubIface.Methods[2].IsPrivate);

        Assert.False(pubIface.Methods[3].Exported);
        Assert.False(pubIface.Methods[3].IsPrivate);

        var internalIface = sf.Interfaces[1];
        Assert.False(internalIface.Exported);
        Assert.False(internalIface.Methods[0].Exported);
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

    [Fact]
    public void ParsesNestedClass_CollectsOuterAndNested()
    {
        var src = @"
namespace Foo;
public class Outer
{
    public class Inner
    {
    }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal(2, sf.Classes.Count);
        Assert.Contains(sf.Classes, c => c.Name == "Outer");
        var inner = Assert.Single(sf.Classes, c => c.Name == "Inner");
        Assert.Equal("class", inner.NodeType);
        Assert.Equal("Foo", inner.Namespace);
        Assert.Equal(5, inner.Line);
    }

    [Fact]
    public void ParsesNestedStructAndRecord()
    {
        var src = @"
public class Outer
{
    public struct NestedStruct { }
    public record NestedRecord();
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal(3, sf.Classes.Count);
        Assert.Equal("struct", Assert.Single(sf.Classes, c => c.Name == "NestedStruct").NodeType);
        Assert.Equal("record", Assert.Single(sf.Classes, c => c.Name == "NestedRecord").NodeType);
    }

    [Fact]
    public void ParsesEnum_ExtractsMembersUnderlyingTypeAndLocation()
    {
        var src = @"
namespace Foo;
public enum Status : byte
{
    pending = 1,
    Complete
}";
        var sf = ModelBuilder.Parse("enum.cs", src);
        var enumModel = Assert.Single(sf.Classes);

        Assert.Equal("Status", enumModel.Name);
        Assert.Equal("enum", enumModel.NodeType);
        Assert.True(enumModel.Exported);
        Assert.Equal("Foo", enumModel.Namespace);
        Assert.Equal(new[] { "byte" }, enumModel.BaseTypes);
        Assert.IsType<EnumDeclarationSyntax>(enumModel.Node);
        Assert.Same(sf, enumModel.File);
        Assert.Equal(3, enumModel.Line);
        Assert.Equal(7, enumModel.EndLine);
        Assert.Empty(enumModel.Methods);
        Assert.Empty(sf.AllMethods);

        Assert.Equal(new[] { "pending", "Complete" }, enumModel.Fields.Select(f => f.Name));
        Assert.Equal(new[] { "pending", "Complete" }, enumModel.Constants.Select(c => c.Name));
        Assert.All(enumModel.Fields, field =>
        {
            Assert.True(field.Exported);
            Assert.True(field.IsStatic);
            Assert.True(field.IsReadonly);
        });
        Assert.Equal(5, enumModel.Fields[0].Line);
        Assert.Equal(6, enumModel.Fields[1].Line);
    }

    [Fact]
    public void ParsesNestedEnum_AsSeparateTypeArtifact()
    {
        var src = @"
namespace Foo;
public class Container
{
    private enum State : short
    {
        Ready,
        Done
    }
}";
        var sf = ModelBuilder.Parse("nested-enum.cs", src);
        var outer = Assert.Single(sf.Classes, c => c.Name == "Container");
        var enumModel = Assert.Single(sf.Classes, c => c.Name == "State");

        Assert.Equal("Foo", enumModel.Namespace);
        Assert.Equal("enum", enumModel.NodeType);
        Assert.False(enumModel.Exported);
        Assert.Equal(new[] { "short" }, enumModel.BaseTypes);
        Assert.Equal(new[] { "Ready", "Done" }, enumModel.Fields.Select(f => f.Name));
        Assert.Empty(outer.Fields);
    }

    [Fact]
    public void ParsesNestedInterface()
    {
        var src = @"
public class Outer
{
    public interface INested
    {
        void M();
    }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var nested = Assert.Single(sf.Interfaces);
        Assert.Equal("INested", nested.Name);
        Assert.Equal("Outer", sf.Classes[0].Name);
        Assert.Single(nested.Methods);
        Assert.Equal("M", nested.Methods[0].Name);
        Assert.Same(nested, nested.Methods[0].Interface);
    }

    [Fact]
    public void NestedTypeMembers_AttributedToNestedTypeNotOuter()
    {
        var src = @"
public class Outer
{
    public void OuterMethod() { }
    private int outerField;

    public class Inner
    {
        public void NestedMethod() { }
        private int nestedField;
    }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var outer = Assert.Single(sf.Classes, c => c.Name == "Outer");
        var inner = Assert.Single(sf.Classes, c => c.Name == "Inner");

        Assert.Single(outer.Methods);
        Assert.Equal("OuterMethod", outer.Methods[0].Name);
        Assert.Same(outer, outer.Methods[0].Class);
        Assert.Contains(outer.Fields, f => f.Name == "outerField");
        Assert.DoesNotContain(outer.Fields, f => f.Name == "nestedField");
        Assert.DoesNotContain(outer.Methods, m => m.Name == "NestedMethod");

        Assert.Single(inner.Methods);
        Assert.Equal("NestedMethod", inner.Methods[0].Name);
        Assert.Same(inner, inner.Methods[0].Class);
        Assert.Contains(inner.Fields, f => f.Name == "nestedField");
        Assert.DoesNotContain(inner.Fields, f => f.Name == "outerField");
    }

    [Fact]
    public void NestedType_NotCollectedAsFieldOfOuter()
    {
        var src = @"
public class Outer
{
    public class Inner { }
    private int x;
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var outer = Assert.Single(sf.Classes, c => c.Name == "Outer");
        Assert.DoesNotContain(outer.Fields, f => f.Name == "Inner");
        Assert.Single(outer.Fields);
        Assert.Equal("x", outer.Fields[0].Name);
    }

    [Fact]
    public void AllMethods_IncludesNestedClassMethods()
    {
        var src = @"
public class Outer
{
    public void OuterMethod() { }
    public class Inner
    {
        public void NestedMethod() { }
    }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal(2, sf.AllMethods.Count);
        Assert.Contains(sf.AllMethods, m => m.Name == "OuterMethod");
        Assert.Contains(sf.AllMethods, m => m.Name == "NestedMethod");
    }

    [Fact]
    public void ParsesDeeplyNestedClass()
    {
        var src = @"
public class Outer
{
    public class Middle
    {
        public class Inner { }
    }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal(3, sf.Classes.Count);
        Assert.Contains(sf.Classes, c => c.Name == "Outer");
        Assert.Contains(sf.Classes, c => c.Name == "Middle");
        Assert.Contains(sf.Classes, c => c.Name == "Inner");
    }

    [Fact]
    public void ParsesMultipleNamespaces_EachTypeKeepsItsNamespace()
    {
        var src = @"
namespace First
{
    public class FirstType { }
}

namespace Second
{
    public class SecondType { }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal("First", sf.Namespace);
        Assert.Equal("First", Assert.Single(sf.Classes, c => c.Name == "FirstType").Namespace);
        Assert.Equal("Second", Assert.Single(sf.Classes, c => c.Name == "SecondType").Namespace);
    }

    [Fact]
    public void ParsesThreeNamespaces_EachTypeKeepsItsNamespace()
    {
        var src = @"
namespace First { public class FirstType { } }
namespace Second { public class SecondType { } }
namespace Third { public class ThirdType { } }";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal("First", Assert.Single(sf.Classes, c => c.Name == "FirstType").Namespace);
        Assert.Equal("Second", Assert.Single(sf.Classes, c => c.Name == "SecondType").Namespace);
        Assert.Equal("Third", Assert.Single(sf.Classes, c => c.Name == "ThirdType").Namespace);
    }

    [Fact]
    public void ParsesDottedNamespace_FullyQualifiedName()
    {
        var src = @"
namespace A.B
{
    public class C { }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal("A.B", sf.Classes[0].Namespace);
    }

    [Fact]
    public void ParsesNestedNamespaces_ConcatenatesNames()
    {
        var src = @"
namespace A
{
    namespace B
    {
        public class C { }
    }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal("A.B", sf.Classes[0].Namespace);
    }

    [Fact]
    public void ParsesInterfaceInSecondNamespace()
    {
        var src = @"
namespace First { public interface IFirst { } }
namespace Second { public interface ISecond { } }";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal("First", Assert.Single(sf.Interfaces, i => i.Name == "IFirst").Namespace);
        Assert.Equal("Second", Assert.Single(sf.Interfaces, i => i.Name == "ISecond").Namespace);
    }

    [Fact]
    public void ParsesGlobalTypeAndNamespacedType_GlobalHasEmptyNamespace()
    {
        var src = @"
public class GlobalType { }
namespace First { public class FirstType { } }";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Equal("", Assert.Single(sf.Classes, c => c.Name == "GlobalType").Namespace);
        Assert.Equal("First", Assert.Single(sf.Classes, c => c.Name == "FirstType").Namespace);
    }

    // Recoverable syntax errors must be surfaced, not silently dropped (#75).
    private const string BrokenSource = @"
public class Broken
{
    public void Method(
}";

    [Fact]
    public void Parse_SyntaxInvalidSource_ExposesSyntaxErrorMessages()
    {
        var sf = ModelBuilder.Parse("Broken.cs", BrokenSource);

        Assert.NotEmpty(sf.SyntaxErrorMessages);
        Assert.Contains(sf.SyntaxErrorMessages, m => m.Contains("CS1026") && m.Contains(") expected"));
        Assert.All(sf.SyntaxErrorMessages, m => Assert.Matches(@"on line \d+$", m));
    }

    [Fact]
    public void Parse_SyntaxValidSource_HasNoSyntaxErrorMessages()
    {
        var sf = ModelBuilder.Parse("Valid.cs", "public class Valid { }");

        Assert.Empty(sf.SyntaxErrorMessages);
    }

    [Fact]
    public void ParsesClass_WithPrimaryConstructor()
    {
        var src = @"
public class Service(int id, string name)
{
    public void Execute() { }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Single(sf.Classes);
        var cls = sf.Classes[0];
        Assert.Equal(2, cls.Methods.Count);

        var ctor = cls.Methods[0];
        Assert.Equal("Service", ctor.Name);
        Assert.True(ctor.IsConstructor);
        Assert.False(ctor.IsPositionalRecordConstructor);
        Assert.True(ctor.Exported);
        Assert.False(ctor.IsPrivate);
        Assert.Null(ctor.Body);
        Assert.Null(ctor.EffectiveBody);
        Assert.Equal(2, ctor.Parameters.Count);
        Assert.Equal("id", ctor.Parameters[0].Name);
        Assert.Equal("int", ctor.Parameters[0].Type);
        Assert.Equal("name", ctor.Parameters[1].Name);
        Assert.Equal("string", ctor.Parameters[1].Type);
        Assert.Same(cls, ctor.Class);

        var method = cls.Methods[1];
        Assert.Equal("Execute", method.Name);
        Assert.False(method.IsConstructor);

        Assert.Equal(2, sf.AllMethods.Count);
        Assert.Same(ctor, sf.AllMethods[0]);
        Assert.Same(method, sf.AllMethods[1]);
    }

    [Fact]
    public void ParsesRecord_WithPrimaryConstructor()
    {
        var src = @"public record Person(string FirstName, string LastName);";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Single(sf.Classes);
        var rec = sf.Classes[0];
        Assert.Equal("record", rec.NodeType);
        Assert.Single(rec.Methods);

        var ctor = rec.Methods[0];
        Assert.Equal("Person", ctor.Name);
        Assert.True(ctor.IsConstructor);
        Assert.True(ctor.IsPositionalRecordConstructor);
        Assert.Equal(2, ctor.Parameters.Count);
        Assert.Equal("FirstName", ctor.Parameters[0].Name);
        Assert.Equal("LastName", ctor.Parameters[1].Name);
    }

    [Fact]
    public void ParsesStruct_WithPrimaryConstructor()
    {
        var src = @"public struct Point(double X, double Y);";
        var sf = ModelBuilder.Parse("test.cs", src);
        Assert.Single(sf.Classes);
        var str = sf.Classes[0];
        Assert.Equal("struct", str.NodeType);
        Assert.Single(str.Methods);

        var ctor = str.Methods[0];
        Assert.Equal("Point", ctor.Name);
        Assert.True(ctor.IsConstructor);
        Assert.False(ctor.IsPositionalRecordConstructor);
        Assert.Equal(2, ctor.Parameters.Count);
    }

    [Fact]
    public void ParsesClass_WithPrimaryAndSecondaryConstructor()
    {
        var src = @"
public class Worker(int id)
{
    public Worker(int id, string name) : this(id) { }
}";
        var sf = ModelBuilder.Parse("test.cs", src);
        var cls = sf.Classes[0];
        Assert.Equal(2, cls.Methods.Count);
        Assert.True(cls.Methods[0].IsConstructor);
        Assert.Single(cls.Methods[0].Parameters);
        Assert.True(cls.Methods[1].IsConstructor);
        Assert.Equal(2, cls.Methods[1].Parameters.Count);
    }

    [Fact]
    public void ParsesExecutablePropertyAccessorsOperatorsConversionsAndDestructor()
    {
        var src = @"
public class Widget
{
    public int Value
    {
        get { if (true) { return 1; } return 0; }
        set => _ = value;
    }

    public static Widget operator +(Widget left, Widget right)
    {
        if (left is null) { return right; }
        return left;
    }

    public static explicit operator int(Widget value) => 0;

    ~Widget()
    {
        if (true) { }
    }

    public void Ordinary() { }
}";

        var sf = ModelBuilder.Parse("issue-167.cs", src);
        var cls = Assert.Single(sf.Classes);

        Assert.Equal(6, cls.Methods.Count);
        Assert.Equal(6, sf.AllMethods.Count);
        Assert.All(cls.Methods, method => Assert.Same(cls, method.Class));

        var getter = Assert.Single(cls.Methods, method => method.Name == "get_Value");
        Assert.IsType<AccessorDeclarationSyntax>(getter.Node);
        Assert.NotNull(getter.Body);
        Assert.Equal("int", getter.ReturnType);
        Assert.Equal(6, getter.Line);
        Assert.Equal(6, getter.EndLine);

        var setter = Assert.Single(cls.Methods, method => method.Name == "set_Value");
        Assert.IsType<AccessorDeclarationSyntax>(setter.Node);
        Assert.Null(setter.Body);
        Assert.NotNull(setter.EffectiveBody);
        Assert.Equal("void", setter.ReturnType);
        Assert.Single(setter.Parameters);
        Assert.Equal("value", setter.Parameters[0].Name);

        var addition = Assert.Single(cls.Methods, method => method.Name == "operator +");
        Assert.IsType<OperatorDeclarationSyntax>(addition.Node);
        Assert.NotNull(addition.Body);
        Assert.Equal("Widget", addition.ReturnType);
        Assert.Equal(new[] { "left", "right" }, addition.Parameters.Select(p => p.Name));
        Assert.Equal(10, addition.Line);
        Assert.Equal(14, addition.EndLine);

        var conversion = Assert.Single(cls.Methods, method => method.Name == "operator explicit int");
        Assert.IsType<ConversionOperatorDeclarationSyntax>(conversion.Node);
        Assert.NotNull(conversion.EffectiveBody);
        Assert.Equal("int", conversion.ReturnType);
        Assert.Single(conversion.Parameters);
        Assert.Equal("value", conversion.Parameters[0].Name);
        Assert.Equal(16, conversion.Line);
        Assert.Equal(16, conversion.EndLine);

        var destructor = Assert.Single(cls.Methods, method => method.Name == "~Widget");
        Assert.IsType<DestructorDeclarationSyntax>(destructor.Node);
        Assert.NotNull(destructor.Body);
        Assert.False(destructor.IsPrivate);
        Assert.Empty(destructor.ReturnType);
        Assert.Empty(destructor.Parameters);
        Assert.Equal(18, destructor.Line);
        Assert.Equal(21, destructor.EndLine);

        var ordinary = Assert.Single(cls.Methods, method => method.Name == "Ordinary");
        Assert.IsType<MethodDeclarationSyntax>(ordinary.Node);
    }

    [Fact]
    public void ParsesExpressionBodiedDestructor()
    {
        var src = @"
public class Widget
{
    ~Widget() => System.Console.WriteLine(""cleaned"");
}";
        var sf = ModelBuilder.Parse("issue-180.cs", src);
        var cls = Assert.Single(sf.Classes);

        var destructor = Assert.Single(cls.Methods, method => method.Name == "~Widget");
        Assert.IsType<DestructorDeclarationSyntax>(destructor.Node);
        Assert.Null(destructor.Body);
        Assert.NotNull(destructor.EffectiveBody);
        Assert.False(destructor.IsPrivate);
        Assert.Empty(destructor.ReturnType);
        Assert.Empty(destructor.Parameters);
        Assert.Equal(4, destructor.Line);
        Assert.Equal(4, destructor.EndLine);
    }
}
