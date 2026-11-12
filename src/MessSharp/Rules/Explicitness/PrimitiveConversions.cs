using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal enum ArgumentCompatibility { Exact, Incompatible, Uncertain }

internal static class PrimitiveConversions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Numeric =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["sbyte"] = new HashSet<string> { "short", "int", "long", "float", "double", "decimal" },
            ["byte"] = new HashSet<string> { "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal" },
            ["short"] = new HashSet<string> { "int", "long", "float", "double", "decimal" },
            ["ushort"] = new HashSet<string> { "int", "uint", "long", "ulong", "float", "double", "decimal" },
            ["int"] = new HashSet<string> { "long", "float", "double", "decimal" },
            ["uint"] = new HashSet<string> { "long", "ulong", "float", "double", "decimal" },
            ["long"] = new HashSet<string> { "float", "double", "decimal" },
            ["ulong"] = new HashSet<string> { "float", "double", "decimal" },
            ["char"] = new HashSet<string> { "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal" },
            ["float"] = new HashSet<string> { "double" },
            ["double"] = new HashSet<string>(),
            ["decimal"] = new HashSet<string>(),
        };

    public static ArgumentCompatibility Compare(CallableOwner caller, CallableOwner candidate, ExpressionSyntax argument, string target)
    {
        var source = new ExpressionTypes(caller).TypeOf(argument) ?? LiteralType(argument);
        source = source is null ? null : SequenceTypeAliases.Resolve(caller, source);
        target = SequenceTypeAliases.Resolve(candidate, target);
        var actual = PrimitiveTypeNames.Name(source);
        var expected = PrimitiveTypeNames.Name(target);
        if (actual is null || expected is null) return ArgumentCompatibility.Uncertain;
        if (actual == expected) return NullableMismatch(source, target, actual)
            ? ArgumentCompatibility.Incompatible : ArgumentCompatibility.Exact;
        return DifferentTypes(caller, argument, actual, expected);
    }

    private static ArgumentCompatibility DifferentTypes(CallableOwner caller, ExpressionSyntax argument, string actual, string expected)
    {
        if (!Numeric.ContainsKey(actual) || !Numeric.ContainsKey(expected)) return ArgumentCompatibility.Incompatible;
        if (DefinitelyNotConstant(caller, argument) && !Numeric[actual].Contains(expected)) return ArgumentCompatibility.Incompatible;
        return ArgumentCompatibility.Uncertain;
    }

    private static bool NullableMismatch(string? source, string target, string name) => name != "string"
        && source?.EndsWith('?') == true && !target.EndsWith('?');

    private static bool DefinitelyNotConstant(CallableOwner caller, ExpressionSyntax argument)
    {
        if (argument is not IdentifierNameSyntax name) return argument is InvocationExpressionSyntax;
        if (ExpressionTypes.Binding(name) is { } local)
            return local.Parent?.Parent is LocalDeclarationStatementSyntax declaration
                && !declaration.Modifiers.Any(SyntaxKind.ConstKeyword);
        return caller.Method.Parameters.Any(parameter => parameter.Name == name.Identifier.Text);
    }

    private static string? LiteralType(ExpressionSyntax expression) => expression is LiteralExpressionSyntax literal
        ? literal.Token.Value?.GetType().Name : null;
}
