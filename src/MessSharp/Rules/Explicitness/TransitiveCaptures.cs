using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// A local function that calls a sibling local function inherits the callee's captured variables, so a
/// mutation of a captured variable reaches the declaring scope through intermediate local functions that
/// never mention the variable themselves.
/// </summary>
internal static class TransitiveCaptures
{
    public static void Apply(IReadOnlyList<CallableOwner> owners)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var owner in owners.Where(o => o.Parent is not null && o.Body is not null))
                changed |= Inherit(owner);
        } while (changed);
    }

    private static bool Inherit(CallableOwner owner)
    {
        var changed = false;
        foreach (var call in BodyWalk.Nodes(owner.Body!).OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not IdentifierNameSyntax callee || LocalTarget(owner, callee.Identifier.Text) is not { } target)
                continue;
            var ownCount = CallableOwner.Parameters(target.Method.Node).Count();
            foreach (var capture in target.Method.Parameters.Skip(ownCount))
                changed |= Add(owner, capture);
        }
        return changed;
    }

    private static bool Add(CallableOwner owner, ParameterModel capture)
    {
        if (owner.Method.Parameters.Any(p => p.Name == capture.Name) || DeclaresLocal(owner, capture.Name)) return false;
        owner.Method.Parameters.Add(capture);
        return true;
    }

    private static bool DeclaresLocal(CallableOwner owner, string name) =>
        BodyWalk.Nodes(owner.Body!).OfType<VariableDeclaratorSyntax>().Any(v => v.Identifier.Text == name);

    private static CallableOwner? LocalTarget(CallableOwner caller, string name)
    {
        for (var scope = caller; scope is not null; scope = scope.Parent)
        {
            var target = scope.Children.FirstOrDefault(c => c.Method.Node is LocalFunctionStatementSyntax local
                && local.Identifier.Text == name);
            if (target is not null && target != caller && target.Parent != caller) return target;
        }
        return null;
    }
}
