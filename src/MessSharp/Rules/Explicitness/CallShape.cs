using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// What a call supplies to the callable it names: an argument count for a delegate conversion, or an argument list
/// whose positions, names, <c>ref</c> kinds and literals rule out overloads that cannot accept it. When several
/// overloads remain, one needing no default or <c>params</c> expansion wins only if every other takes the supplied
/// arguments at the same parameter types; otherwise the call stays unresolved.
/// </summary>
internal sealed record CallShape(int Count, IReadOnlyList<ArgumentSyntax>? Arguments = null)
{
    private sealed record Candidate(CallableOwner Owner, bool Exact, IReadOnlyList<string> Types);

    public static CallShape From(ArgumentListSyntax list) => new(list.Arguments.Count, list.Arguments);

    /// <summary>The one candidate the call selects, skipping <paramref name="receivers"/> leading extension receivers.</summary>
    public CallableOwner? Best(IEnumerable<CallableOwner> candidates, int receivers = 0)
    {
        var fits = candidates.Select(owner => Match(owner, receivers)).OfType<Candidate>().ToList();
        if (fits.Count == 1) return fits[0].Owner;
        var exact = fits.Where(candidate => candidate.Exact).ToList();
        return exact.Count == 1 && fits.All(candidate => candidate.Types.SequenceEqual(exact[0].Types)) ? exact[0].Owner : null;
    }

    private Candidate? Match(CallableOwner owner, int receivers)
    {
        var parameters = Own(owner).Skip(receivers).ToList();
        if (Arguments is null) return parameters.Count == Count ? new(owner, true, []) : null;
        var mapped = Arguments.Select((argument, position) => Index(parameters, argument, position)).ToList();
        if (mapped.Where((index, position) => index < 0 || !Passes(parameters[index], Arguments[position])).Any()) return null;
        var omitted = parameters.Where((_, index) => !mapped.Contains(index)).ToList();
        if (!omitted.All(IsOptional)) return null;
        var exact = omitted.Count == 0 && Arguments.Count == parameters.Count && !parameters.Any(IsParams);
        return new(owner, exact, mapped.Select(index => parameters[index].Type).ToList());
    }

    private static IEnumerable<ParameterModel> Own(CallableOwner owner) => owner.Parent is null
        ? owner.Method.Parameters
        : owner.Method.Parameters.Take(CallableOwner.Parameters(owner.Method.Node).Count());

    private static int Index(IReadOnlyList<ParameterModel> parameters, ArgumentSyntax argument, int position)
    {
        if (argument.NameColon is { } named)
            return parameters.ToList().FindIndex(parameter => parameter.Name == named.Name.Identifier.Text);
        if (position < parameters.Count && !IsParams(parameters[position])) return position;
        return parameters.Count > 0 && IsParams(parameters[^1]) ? parameters.Count - 1 : -1;
    }

    private static bool Passes(ParameterModel parameter, ArgumentSyntax argument) => argument.RefKindKeyword.Kind() switch
    {
        SyntaxKind.OutKeyword => parameter.IsOut,
        SyntaxKind.RefKeyword => parameter.IsRef,
        SyntaxKind.InKeyword => parameter.Node.Modifiers.Any(SyntaxKind.InKeyword),
        _ => !parameter.IsOut && !parameter.IsRef && LiteralArguments.Accepts(parameter.Type, argument.Expression),
    };

    private static bool IsOptional(ParameterModel parameter) => parameter.Node.Default is not null || IsParams(parameter);

    private static bool IsParams(ParameterModel parameter) => parameter.Node.Modifiers.Any(SyntaxKind.ParamsKeyword);
}

/// <summary>Whether a literal argument can convert to a parameter of a predefined type.</summary>
internal static class LiteralArguments
{
    private static readonly HashSet<string> Numeric = new(StringComparer.Ordinal)
    {
        "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "nint", "nuint", "float", "double", "decimal",
    };

    public static bool Accepts(string type, ExpressionSyntax argument)
    {
        var declared = type.TrimEnd('?');
        return !IsPredefined(declared) || Matches(declared, type.EndsWith('?'), Literal(argument));
    }

    private static bool IsPredefined(string declared) =>
        Numeric.Contains(declared) || declared is "bool" or "char" or "string";

    private static bool Matches(string declared, bool nullable, SyntaxKind kind) => kind switch
    {
        SyntaxKind.StringLiteralExpression or SyntaxKind.InterpolatedStringExpression => declared == "string",
        SyntaxKind.NumericLiteralExpression => Numeric.Contains(declared),
        SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => declared == "bool",
        SyntaxKind.CharacterLiteralExpression => IsCharacterCompatible(declared),
        SyntaxKind.NullLiteralExpression => IsNullCompatible(declared, nullable),
        _ => true,
    };

    private static bool IsCharacterCompatible(string declared) => Numeric.Contains(declared) || declared == "char";

    private static bool IsNullCompatible(string declared, bool nullable) => declared == "string" || nullable;

    private static SyntaxKind Literal(ExpressionSyntax argument) => argument is ParenthesizedExpressionSyntax grouped
        ? Literal(grouped.Expression) : argument.Kind();
}
