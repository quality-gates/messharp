using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>Forcing a Lazy with no factory constructs T; only built-in scalar defaults are inherently quiet.</summary>
internal static class LazyDefaultConstruction
{
    private static readonly IReadOnlySet<string> Scalars = new HashSet<string>(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "nint", "nuint",
        "char", "float", "double", "decimal", "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32",
        "Int64", "UInt64", "IntPtr", "UIntPtr", "Char", "Single", "Double", "Decimal", "DateTime", "DateTimeOffset",
        "TimeSpan", "Guid", "object", "Object",
    };

    public static DeferredResolution Resolve(IReadOnlyList<CallableOwner> owners, DeferredValueContext context,
        BaseObjectCreationExpressionSyntax lazy)
    {
        var owner = context.Owner;
        var type = new ExpressionTypes(owner).TypeOf(lazy);
        var systemType = type is null ? null : LazyTypes.SystemType(owner, type);
        var element = systemType is null ? null : ExpressionTypes.Element(systemType);
        if (element is null) return new DeferredResolution([], false);
        var creation = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(element))
            .WithArgumentList(SyntaxFactory.ArgumentList());
        if (ConstructorCall.Resolve(owners, creation, element) is { } constructor)
            return new DeferredResolution([new DeferredWork(DeferredKind.Lazy, constructor.Target,
                constructor.Invocation, new Dictionary<string, StorageOrigin>())], true);
        return new DeferredResolution([], IsScalar(owner, element) || ConstructorDefaults.IsEmpty(owner, element, creation));
    }

    private static bool IsScalar(CallableOwner owner, string type) =>
        Scalars.Contains(TypeNames.Simple(type)) && !KnownTypeChecks.IsCustomType(owner, type);
}
