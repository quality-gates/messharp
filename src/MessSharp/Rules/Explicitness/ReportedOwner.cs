using MessSharp.Model;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The owner a finding names: namespace, declaring type and printed name, with a static constructor printed as
/// <c>&lt;module&gt;</c>. Overloads, constructors and lambdas of one type can print alike, and they report as one owner.
/// </summary>
internal static class ReportedOwner
{
    public static MethodModel Printed(MethodModel method) => method.IsStaticConstructor() ? new MethodModel
    {
        Name = "<module>",
        Class = method.Class,
        Interface = method.Interface,
        Node = method.Node,
        File = method.File,
    } : method;

    public static (string Namespace, string Type, string Name) Identity(MethodModel method)
    {
        var reported = Printed(method);
        return (reported.Namespace, reported.DeclaringTypeName, reported.Name);
    }
}
