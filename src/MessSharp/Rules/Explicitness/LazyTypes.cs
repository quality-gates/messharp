using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Recognises <c>System.Lazy</c> and same-file subclasses, keeping hidden Value members distinct
/// from the inherited property that executes the base factory.
/// </summary>
internal static class LazyTypes
{
    public static bool IsLazy(CallableOwner owner, string type) =>
        IsSystemLazy(owner, type) || Subclass(owner, type) is not null;

    public static bool IsSystemLazy(CallableOwner owner, string type) =>
        Name(type) == "Lazy" && !KnownTypeChecks.IsCustomType(owner, type);

    /// <summary>The same-file class named by <paramref name="type"/> when its base chain reaches Lazy.</summary>
    public static ClassModel? Subclass(CallableOwner owner, string type)
    {
        var cls = owner.Method.File.Classes.FirstOrDefault(candidate => candidate.Name == Name(type));
        return cls is not null && ReachesLazy(owner, cls) ? cls : null;
    }

    private static bool ReachesLazy(CallableOwner owner, ClassModel cls)
    {
        var seen = new HashSet<ClassModel>();
        for (var current = cls; seen.Add(current);)
        {
            if (current.BaseTypes.FirstOrDefault() is not { } baseType) return false;
            if (IsSystemLazy(owner, baseType)) return true;
            if (ConstructorTypes.BaseClass(current) is not { } next) return false;
            current = next;
        }
        return false;
    }

    /// <summary>The System.Lazy base type, or null when its type arguments cannot be established.</summary>
    public static string? SystemType(CallableOwner owner, string type)
    {
        if (IsSystemLazy(owner, type)) return type;
        var seen = new HashSet<ClassModel>();
        for (var cls = Subclass(owner, type); cls is not null && seen.Add(cls); cls = ConstructorTypes.BaseClass(cls))
            if (cls.BaseTypes.FirstOrDefault() is { } baseType && IsSystemLazy(owner, baseType)) return baseType;
        return null;
    }

    public static bool HasStandardValue(CallableOwner owner, string type)
    {
        if (IsSystemLazy(owner, type)) return true;
        var seen = new HashSet<ClassModel>();
        for (var cls = Subclass(owner, type); cls is not null && seen.Add(cls); cls = ConstructorTypes.BaseClass(cls))
            if (cls.Node.ChildNodes().OfType<PropertyDeclarationSyntax>().Any(p => p.Identifier.Text == "Value")
                || cls.Fields.Any(field => field.Name == "Value")) return false;
        return IsLazy(owner, type);
    }

    public static string Name(string type) => TypeNames.Simple(type);
}
