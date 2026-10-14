using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class BindingOrigins(
    SyntaxNode body, Func<ExpressionSyntax, StorageEnvironment, StorageOrigin> origin,
    Func<SyntaxNode, StorageEnvironment, bool, bool> execute,
    Func<ExpressionSyntax, StorageEnvironment, StorageCallable?> callable, bool weak = false)
{
    private readonly BodyBindings _bindings = new(body);

    public StorageEnvironment Before(SyntaxNode site, StorageEnvironment initial, bool after = false)
    {
        var reaching = StorageSteps.Gather(body, _bindings.All).Where(step => after || _bindings.Reaches(step, site)).ToList();
        var loops = new Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>>();
        var heaps = new Dictionary<SyntaxNode, StorageHeap>();
        var callables = new StorageCallableFlow(_bindings, body, callable);
        StorageEnvironment state;
        bool changed;
        do
        {
            state = initial.Copy();
            changed = false;
            foreach (var step in reaching)
                changed |= Apply(step, state, loops, heaps, callables);
        } while (changed);
        return state;
    }

    private bool Apply(StorageStep step, StorageEnvironment state, Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>> loops,
        Dictionary<SyntaxNode, StorageHeap> heaps, StorageCallableFlow callables)
    {
        var loop = _bindings.RepetitionRegion(step.Node);
        EnterLoop(loop, state, loops, heaps, callables);
        var handled = execute(step.Node, state, weak || IsSkipped(step));
        if (step is not StorageStep.Bound bound) return JoinHeap(loop, state.Heap, heaps);
        var changed = !handled && ApplyBinding(bound.Binding, loop, state, loops);
        changed |= callables.Bind(bound.Binding, loop, loop is not null && IsCarried(bound.Binding, loop), state);
        return changed | JoinHeap(loop, state.Heap, heaps);
    }

    private void EnterLoop(SyntaxNode? loop, StorageEnvironment state,
        Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>> loops, Dictionary<SyntaxNode, StorageHeap> heaps,
        StorageCallableFlow callables)
    {
        Carry(loop, state.Bindings, loops);
        callables.Carry(loop, state);
        if (loop is not null && heaps.TryGetValue(loop, out var heap)) state.Heap.Join(heap);
    }

    private bool IsSkipped(StorageStep step) =>
        step is StorageStep.Bound binding ? _bindings.MayBeSkipped(binding.Binding) : _bindings.MayBeSkipped(step.Node);

    private bool ApplyBinding(StorageBinding binding, SyntaxNode? loop, StorageEnvironment state,
        Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>> loops)
    {
        var next = Bound(binding, state);
        var previous = origin(SyntaxFactory.IdentifierName(binding.Name), state);
        state.Bindings[binding.Name] = _bindings.MayBeSkipped(binding) ? previous.Join(next) : next;
        if (loop is null || !IsCarried(binding, loop)) return false;
        if (!loops.TryGetValue(loop, out var carried))
            loops[loop] = carried = new Dictionary<string, StorageOrigin>(StringComparer.Ordinal);
        return JoinLoop(carried, binding.Name, state.Bindings[binding.Name]);
    }

    private static bool JoinHeap(SyntaxNode? loop, StorageHeap heap, Dictionary<SyntaxNode, StorageHeap> heaps)
    {
        if (loop is null) return false;
        if (heaps.TryGetValue(loop, out var carried)) return carried.Join(heap);
        heaps[loop] = heap.Copy();
        return true;
    }

    private static void Carry(SyntaxNode? loop, Dictionary<string, StorageOrigin> owners,
        Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>> loops)
    {
        if (loop is null || !loops.TryGetValue(loop, out var carried)) return;
        foreach (var (name, storage) in carried)
            owners[name] = owners.GetValueOrDefault(name, StorageOrigin.Private).Join(storage);
    }

    private StorageOrigin Bound(StorageBinding binding, StorageEnvironment state) => binding.Source switch
    {
        BindingSource.Value value => origin(value.Expression, state),
        BindingSource.Element element => state.Heap.Read(origin(element.Expression, state), StorageCell.AnyElement),
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
