using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class BindingOrigins(
    SyntaxNode body, Func<ExpressionSyntax, Dictionary<string, StorageOrigin>, StorageOrigin> origin)
{
    private readonly BodyBindings _bindings = new(body);

    public Dictionary<string, StorageOrigin> Before(SyntaxNode site)
    {
        var reaching = _bindings.All.Where(binding => _bindings.Reaches(binding, site)).ToList();
        var loops = new Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>>();
        Dictionary<string, StorageOrigin> owners;
        bool changed;
        do
        {
            owners = new Dictionary<string, StorageOrigin>(StringComparer.Ordinal);
            changed = false;
            foreach (var binding in reaching)
                changed |= Apply(binding, owners, loops);
        } while (changed);
        return owners;
    }

    private bool Apply(StorageBinding binding, Dictionary<string, StorageOrigin> owners,
        Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>> loops)
    {
        var loop = _bindings.RepetitionRegion(binding.Node);
        Carry(loop, owners, loops);
        var next = Bound(binding, owners);
        var previous = origin(SyntaxFactory.IdentifierName(binding.Name), owners);
        owners[binding.Name] = _bindings.MayBeSkipped(binding) ? previous.Join(next) : next;
        if (loop is null || !IsCarried(binding, loop)) return false;
        if (!loops.TryGetValue(loop, out var carried))
            loops[loop] = carried = new Dictionary<string, StorageOrigin>(StringComparer.Ordinal);
        return JoinLoop(carried, binding.Name, owners[binding.Name]);
    }

    private static void Carry(SyntaxNode? loop, Dictionary<string, StorageOrigin> owners,
        Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>> loops)
    {
        if (loop is null || !loops.TryGetValue(loop, out var carried)) return;
        foreach (var (name, storage) in carried)
            owners[name] = owners.GetValueOrDefault(name, StorageOrigin.Private).Join(storage);
    }

    private StorageOrigin Bound(StorageBinding binding, Dictionary<string, StorageOrigin> owners) => binding.Source switch
    {
        BindingSource.Value value => origin(value.Expression, owners),
        BindingSource.Element element => origin(element.Expression, owners).Element(),
        BindingSource.Unknown => StorageOrigin.Unknown,
        _ => StorageOrigin.Private,
    };

    private bool IsCarried(StorageBinding binding, SyntaxNode loop) => !_bindings.All.Any(declaration =>
        declaration.Name == binding.Name && declaration.Node is VariableDeclaratorSyntax
            && loop.Span.Contains(declaration.Node.Span));

    private static bool JoinLoop(Dictionary<string, StorageOrigin> origins, string name, StorageOrigin next)
    {
        var previous = origins.GetValueOrDefault(name, StorageOrigin.Private);
        var joined = previous.Join(next);
        if (previous.SameRoots(joined)) return false;
        origins[name] = joined;
        return true;
    }
}
