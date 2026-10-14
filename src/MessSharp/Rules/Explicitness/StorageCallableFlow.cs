using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The callable a local holds after each binding in one pass over a body. A local keeps a callable only while every
/// value reaching it names the same one; a skippable or repeated binding that names another drops it, and so does
/// a local whose storage a <c>ref</c> alias can rewrite.
/// </summary>
internal sealed class StorageCallableFlow(
    BodyBindings bindings, SyntaxNode body, Func<ExpressionSyntax, StorageEnvironment, StorageCallable?> callable)
{
    private readonly Dictionary<SyntaxNode, Dictionary<string, CallableOwner?>> _loops = new();
    private readonly HashSet<string> _aliased = BodyWalk.Nodes(body).OfType<RefExpressionSyntax>()
        .Where(reference => !RefAliasCache.For(body).Initializes(reference)).Select(reference => CallableSyntax.Unwrap(reference.Expression)).OfType<IdentifierNameSyntax>()
        .Select(name => name.Identifier.Text).ToHashSet(StringComparer.Ordinal);

    public void Carry(SyntaxNode? loop, StorageEnvironment state)
    {
        if (loop is null || !_loops.TryGetValue(loop, out var carried)) return;
        foreach (var (name, target) in carried)
            if (target is null || state.Callables.GetValueOrDefault(name)?.Target != target) state.Callables.Remove(name);
    }

    /// <summary>Rebinds the local; true when a loop-carried callable changed and the pass must repeat.</summary>
    public bool Bind(StorageBinding binding, SyntaxNode? loop, bool carried, StorageEnvironment state)
    {
        var next = Next(binding, state);
        var previous = state.Callables.GetValueOrDefault(binding.Name);
        var kept = bindings.MayBeSkipped(binding) && previous?.Target != next?.Target ? null : next;
        if (kept is null) state.Callables.Remove(binding.Name);
        else state.Callables[binding.Name] = kept;
        return loop is not null && carried && Join(loop, binding.Name, kept?.Target);
    }

    private StorageCallable? Next(StorageBinding binding, StorageEnvironment state) =>
        binding.Source is BindingSource.Value value && !_aliased.Contains(binding.Name) ? callable(value.Expression, state) : null;

    private bool Join(SyntaxNode loop, string name, CallableOwner? target)
    {
        if (!_loops.TryGetValue(loop, out var carried)) _loops[loop] = carried = new(StringComparer.Ordinal);
        if (!carried.TryGetValue(name, out var previous))
        {
            carried[name] = target;
            return true;
        }
        if (previous is null || previous == target) return false;
        carried[name] = null;
        return true;
    }
}
