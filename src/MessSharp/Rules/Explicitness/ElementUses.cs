using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>Classifies the uses of a collection variable: element reads, element writes and list mutators.</summary>
internal static class ElementUses
{
    public static bool OnlyReads(CallableOwner owner, SyntaxNode body, string parameter) =>
        owner.Method.Parameters.Any(candidate => candidate.Name == parameter)
        && body.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(use => use.Identifier.Text == parameter && ExpressionTypes.Binding(use) is null)
            .All(IsElementRead);

    /// <summary>Whether <paramref name="use"/> is passed to a resolved callee that only reads the elements of that parameter.</summary>
    public static bool PassedToReader(DeferredValueContext context, IdentifierNameSyntax use)
    {
        var argument = use.Parent as ArgumentSyntax;
        var call = argument?.Parent?.Parent ?? (use.Parent as MemberAccessExpressionSyntax)?.Parent;
        if (call is not InvocationExpressionSyntax invocation || argument is not null && IsByReference(argument)) return false;
        return new CallResolver(context.Owners).Resolve(context.Owner, invocation) is { Body: { } body } target
            && ReadsParameter(target, body, invocation, use);
    }

    private static bool ReadsParameter(CallableOwner target, SyntaxNode body, InvocationExpressionSyntax invocation, IdentifierNameSyntax use)
    {
        var parameter = target.Method.Parameters.Where((candidate, index) =>
            CallArguments.Actual(target, invocation, candidate.Name, index) == use).FirstOrDefault();
        return parameter is not null && OnlyReads(target, body, parameter.Name);
    }

    internal static bool IsElementRead(IdentifierNameSyntax use) =>
        use.Parent is ForEachStatementSyntax loop && loop.Expression == use
        || ElementAccess(use) is { } access && !(access.Parent is AssignmentExpressionSyntax assignment && assignment.Left == access);

    /// <summary>Adds the value of an element write; false when the use is not an element read or simple write.</summary>
    public static bool Record(IdentifierNameSyntax use, List<ExpressionSyntax> elements)
    {
        if (use.Parent is ForEachStatementSyntax loop && loop.Expression == use) return true;
        if (ListAdd(use) is { } added)
        {
            elements.AddRange(added);
            return true;
        }
        return ElementAccess(use) is { } access && RecordWrite(access, elements);
    }

    private static bool RecordWrite(ElementAccessExpressionSyntax access, List<ExpressionSyntax> elements)
    {
        if (access.Parent is not AssignmentExpressionSyntax assignment || assignment.Left != access) return true;
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)) return false;
        elements.Add(assignment.Right);
        return true;
    }

    /// <summary>
    /// The values a list call through <paramref name="use"/> stores: none for a call that only reads or shrinks the
    /// list; null when the use is not such a call.
    /// </summary>
    private static IReadOnlyList<ExpressionSyntax>? ListAdd(IdentifierNameSyntax use)
    {
        if (use.Parent is not MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax call } member
            || member.Expression != use || call.Expression != member) return null;
        var arguments = call.ArgumentList.Arguments;
        return arguments.Any(IsByReference) ? null : Stored(member.Name.Identifier.Text, arguments);
    }

    internal static bool IsByReference(ArgumentSyntax argument) => argument.RefKindKeyword.RawKind != (int)SyntaxKind.None;

    private static IReadOnlyList<ExpressionSyntax>? Stored(string method, SeparatedSyntaxList<ArgumentSyntax> arguments) =>
        method switch
        {
            "Add" when arguments.Count == 1 => [arguments[0].Expression],
            "Insert" when arguments.Count == 2 => [arguments[1].Expression],
            "Clear" or "RemoveAt" or "Remove" => [],
            _ => null,
        };

    /// <summary>The element access <paramref name="use"/> indexes, unless that element is passed by reference.</summary>
    private static ElementAccessExpressionSyntax? ElementAccess(IdentifierNameSyntax use) =>
        use.Parent is ElementAccessExpressionSyntax access && access.Expression == use
            && access.Parent is not ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } ? access : null;
}
