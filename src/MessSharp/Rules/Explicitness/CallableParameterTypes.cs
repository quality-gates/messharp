using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CallableParameterTypes
{
    public static void Apply(IReadOnlyList<CallableOwner> owners)
    {
        foreach (var owner in owners.Where(owner => owner.Parent is not null)) Infer(owner, owners);
    }

    private static void Infer(CallableOwner owner, IReadOnlyList<CallableOwner> owners)
    {
        var parent = owner.Parent!;
        var inferred = new DelegateParameterTypes(parent, owners).For(owner.Method.Node);
        var ownCount = CallableOwner.Parameters(owner.Method.Node).Count();
        var count = owner.Method.Parameters.Count;
        for (var index = 0; index < count; index++)
        {
            var parameter = owner.Method.Parameters[index];
            if (parameter.Type.Length != 0) continue;
            var type = index < ownCount ? inferred.ElementAtOrDefault(index) : CaptureType(owner, parent, parameter.Name, owners);
            if (type is not null) owner.Method.Parameters[index] = WithType(parameter, type);
        }
    }

    private static string? CaptureType(CallableOwner owner, CallableOwner parent, string name, IReadOnlyList<CallableOwner> owners)
    {
        var read = owner.Body is null ? null : BodyWalk.Nodes(owner.Body).OfType<IdentifierNameSyntax>()
            .FirstOrDefault(identifier => identifier.Identifier.Text == name);
        return read is null ? null : new ExpressionTypes(parent, owners).TypeOf(read);
    }

    private static ParameterModel WithType(ParameterModel parameter, string type) => new()
    {
        Name = parameter.Name,
        Type = type,
        Node = parameter.Node,
        Line = parameter.Line,
        IsRef = parameter.IsRef,
        IsOut = parameter.IsOut,
    };
}
