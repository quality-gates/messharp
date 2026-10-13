using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal enum AccessKind
{
    Read,

    Write,

    Change,
}

internal readonly record struct StateAccess(string Name, AccessKind Kind, SyntaxNode Site, bool Deep = false);

internal readonly record struct ResolvedState(string Name, bool Deep = false);

internal sealed class StateAccessCollector : IBodyVisitor
{
    private readonly Func<ExpressionSyntax, IReadOnlyCollection<ResolvedState>> _resolve;
    private readonly IReadOnlySet<string>? _mutableCollections;
    private readonly Func<SyntaxNodeCall, bool>? _knownMutation;
    private readonly bool _recordsReads;
    private readonly bool _readsMutations;
    private readonly List<StateAccess> _mutations = [];
    private readonly List<StateAccess> _reads = [];
    private readonly HashSet<SyntaxNode> _pureWrites = [];

    public StateAccessCollector(
        Func<ExpressionSyntax, string?> resolve, IReadOnlySet<string>? mutableCollections = null,
        Func<SyntaxNodeCall, bool>? knownMutation = null, bool readsMutations = true)
        : this(expr => resolve(expr) is { } name ? [new ResolvedState(name)] : [], mutableCollections, knownMutation,
            recordsReads: true, readsMutations)
    {
    }

    private StateAccessCollector(
        Func<ExpressionSyntax, IReadOnlyCollection<ResolvedState>> resolve, IReadOnlySet<string>? mutableCollections,
        Func<SyntaxNodeCall, bool>? knownMutation, bool recordsReads, bool readsMutations)
    {
        _resolve = resolve;
        _mutableCollections = mutableCollections;
        _knownMutation = knownMutation;
        _recordsReads = recordsReads;
        _readsMutations = readsMutations;
    }

    public static StateAccessCollector MutationsOf(
        Func<ExpressionSyntax, IReadOnlyCollection<ResolvedState>> resolve, Func<SyntaxNodeCall, bool> knownMutation) =>
        new(resolve, null, knownMutation, recordsReads: false, readsMutations: false);

    public List<StateAccess> Accesses() => [.. _mutations, .. _reads];

    public void Visit(SyntaxNode node)
    {
        foreach (var (target, isPureWrite) in MutationTargets.Find(node))
            AddMutation(target, isPureWrite);
        if (CollectionCall(node) is { } call && IsMutation(call))
        {
            AddAll(ResolveChain(call.Receiver), AccessKind.Change, node);
            if (!_readsMutations) ExcludeReads(call.Receiver);
        }
        if (_recordsReads && node is ExpressionSyntax expr && !_pureWrites.Contains(expr))
            foreach (var state in _resolve(expr))
                _reads.Add(new StateAccess(state.Name, AccessKind.Read, expr));
    }

    private void AddMutation(ExpressionSyntax target, bool isPureWrite)
    {
        if (AddDirectMutation(target, isPureWrite)) return;
        if (ReceiverOf(target) is not { } receiver) return;
        AddAll(ResolveChain(receiver), AccessKind.Change, target);
        if (isPureWrite) ExcludeReads(target);
    }

    private bool AddDirectMutation(ExpressionSyntax target, bool isPureWrite)
    {
        if (target is ElementAccessExpressionSyntax || _resolve(target) is not { Count: > 0 } written) return false;
        foreach (var state in written)
            AddAll([state], MutationKind(target, state), target);
        if (isPureWrite) ExcludeReads(target);
        return true;
    }

    private static AccessKind MutationKind(ExpressionSyntax target, ResolvedState state) =>
        target is MemberAccessExpressionSyntax && state.Deep ? AccessKind.Change : AccessKind.Write;

    private void ExcludeReads(ExpressionSyntax target)
    {
        for (ExpressionSyntax? expression = target; expression is not null; expression = ReceiverOf(expression))
            _pureWrites.Add(expression);
    }

    private void AddAll(IReadOnlyCollection<ResolvedState> states, AccessKind kind, SyntaxNode site)
    {
        foreach (var state in states)
            _mutations.Add(new StateAccess(state.Name, kind, site, state.Deep));
    }

    private static SyntaxNodeCall? CollectionCall(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } =>
            new SyntaxNodeCall(member.Expression, member.Name.Identifier.Text),
        InvocationExpressionSyntax
        {
            Expression: MemberBindingExpressionSyntax member,
            Parent: ConditionalAccessExpressionSyntax conditional
        } =>
            new SyntaxNodeCall(conditional.Expression, member.Name.Identifier.Text),
        _ => null,
    };

    private bool IsMutation(SyntaxNodeCall call) => _knownMutation?.Invoke(call)
        ?? (KnownMutations.IsMutation(call.Name)
            && (_mutableCollections is null || ResolveChain(call.Receiver).Any(state => _mutableCollections.Contains(state.Name))));

    private IReadOnlyCollection<ResolvedState> ResolveChain(ExpressionSyntax start)
    {
        for (ExpressionSyntax? expr = start; expr != null; expr = ReceiverOf(expr))
        {
            if (_resolve(expr) is { Count: > 0 } names) return names;
        }
        return [];
    }

    private static ExpressionSyntax? ReceiverOf(ExpressionSyntax expr) => expr switch
    {
        MemberAccessExpressionSyntax ma => ma.Expression,
        ElementAccessExpressionSyntax ea => ea.Expression,
        ConditionalAccessExpressionSyntax ca => ca.Expression,
        ParenthesizedExpressionSyntax p => p.Expression,
        _ => null,
    };
}
