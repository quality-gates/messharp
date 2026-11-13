using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class BindingOrigins(
    SyntaxNode body, Func<ExpressionSyntax, StorageEnvironment, StorageOrigin> origin,
    Func<SyntaxNode, StorageEnvironment, Certainty, bool> execute,
    Func<ExpressionSyntax, StorageEnvironment, StorageCallable?> callable, Certainty weak = default)
{
    private readonly BodyBindings _bindings = BodyBindingsCache.For(body);

    public StorageEnvironment Before(SyntaxNode site, StorageEnvironment initial, bool after = false)
    {
        var reaching = _bindings.Steps.Where(step => after || _bindings.Reaches(step, site)).ToList();
        var pass = new BindingPass(new StorageCallableFlow(_bindings, body, callable));
        StorageEnvironment state;
        bool changed;
        do
        {
            state = initial.Copy();
            changed = false;
            pass.Snapshots.Clear();
            foreach (var step in reaching)
                changed |= Apply(step, state, pass);
        } while (changed);
        return state;
    }

    private bool Apply(StorageStep step, StorageEnvironment state, BindingPass pass)
    {
        var loop = _bindings.RepetitionRegion(step.Node);
        EnterLoop(loop, state, pass);
        var handled = execute(step.Node, state, weak.OrIf(IsSkipped(step)));
        if (step is not StorageStep.Bound bound) return pass.Heaps.Join(loop, state.Heap);
        var (binding, reads) = pass.Evaluation(bound.Binding, state);
        var changed = !handled && ApplyBinding(binding, loop, state, reads, pass);
        changed |= pass.Callables.Bind(binding, loop, loop is not null && _bindings.IsCarried(binding, loop), state, reads);
        return changed | pass.Heaps.Join(loop, state.Heap);
    }

    private void EnterLoop(SyntaxNode? loop, StorageEnvironment state, BindingPass pass)
    {
        pass.Loops.Carry(loop, state.Bindings);
        pass.Callables.Carry(loop, state);
        pass.Heaps.Enter(loop, state.Heap);
    }

    private bool IsSkipped(StorageStep step) =>
        step is StorageStep.Bound binding ? _bindings.MayBeSkipped(binding.Binding) : _bindings.MayBeSkipped(step.Node);

    private bool ApplyBinding(StorageBinding binding, SyntaxNode? loop, StorageEnvironment state, StorageEnvironment reads,
        BindingPass pass)
    {
        var next = Bound(binding, reads);
        var previous = origin(SyntaxFactory.IdentifierName(binding.Name), state);
        state.Bindings[binding.Name] = _bindings.MayBeSkipped(binding) ? previous.Join(next) : next;
        if (loop is null || !_bindings.IsCarried(binding, loop)) return false;
        return pass.Loops.JoinCarried(loop, binding.Name, state.Bindings[binding.Name]);
    }

    private StorageOrigin Bound(StorageBinding binding, StorageEnvironment state) => binding.Source switch
    {
        BindingSource.Value value => origin(value.Expression, state),
        BindingSource.Element element => state.Heap.Read(origin(element.Expression, state)),
        BindingSource.Unknown => StorageOrigin.Unknown,
        _ => StorageOrigin.Private,
    };
}

/// <summary>What one pass over a body's bindings carries from step to step.</summary>
internal sealed class BindingPass(StorageCallableFlow callables)
{
    public LoopCarries Loops { get; } = new();
    public LoopHeaps Heaps { get; } = new();
    public StorageCallableFlow Callables { get; } = callables;
    public Dictionary<SyntaxNode, StorageEnvironment> Snapshots { get; } = [];

    /// <summary>
    /// A target of a deconstruction whose right side reads an earlier target takes its value from the state before the
    /// first target was written.
    /// </summary>
    public (StorageBinding Binding, StorageEnvironment Reads) Evaluation(StorageBinding binding, StorageEnvironment state)
    {
        if (binding.Simultaneous is not { } value) return (binding, state);
        if (!Snapshots.TryGetValue(binding.Node, out var before)) Snapshots[binding.Node] = before = state.Copy();
        return (binding with { Source = new BindingSource.Value(value), Simultaneous = null }, before);
    }
}

/// <summary>The heap each repeated region has reached so far.</summary>
internal sealed class LoopHeaps
{
    private readonly Dictionary<SyntaxNode, StorageHeap> _heaps = [];

    public void Enter(SyntaxNode? loop, StorageHeap heap)
    {
        if (loop is not null && _heaps.TryGetValue(loop, out var carried)) heap.Join(carried);
    }

    public bool Join(SyntaxNode? loop, StorageHeap heap)
    {
        if (loop is null) return false;
        if (_heaps.TryGetValue(loop, out var carried)) return carried.Join(heap);
        _heaps[loop] = heap.Copy();
        return true;
    }
}

/// <summary>The values each repeated region carries from one pass to the next.</summary>
internal sealed class LoopCarries
{
    private readonly Dictionary<SyntaxNode, Dictionary<string, StorageOrigin>> _loops = [];

    /// <summary>Gives <paramref name="owners"/> what the values carried around <paramref name="loop"/> have reached so far.</summary>
    public void Carry(SyntaxNode? loop, Dictionary<string, StorageOrigin> owners)
    {
        if (loop is null || !_loops.TryGetValue(loop, out var carried)) return;
        foreach (var (name, storage) in carried)
            owners[name] = owners.GetValueOrDefault(name, StorageOrigin.Private).Join(storage);
    }

    /// <summary>Records what a carried variable holds this pass; true when that is new.</summary>
    public bool JoinCarried(SyntaxNode loop, string name, StorageOrigin next)
    {
        if (!_loops.TryGetValue(loop, out var carried))
            _loops[loop] = carried = new Dictionary<string, StorageOrigin>(StringComparer.Ordinal);
        var previous = carried.GetValueOrDefault(name, StorageOrigin.Private);
        var joined = previous.Join(next);
        if (previous.SameRoots(joined)) return false;
        carried[name] = joined;
        return true;
    }
}
