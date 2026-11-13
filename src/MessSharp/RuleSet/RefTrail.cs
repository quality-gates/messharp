namespace MessSharp.RuleSet;

/// <summary>
/// The rulesets a ref is being resolved through, from the outermost down to the current one. Going into a ruleset makes
/// a new, longer trail and leaves the old one as it was, so sibling refs never see each other's visits; going into one
/// that is already on the trail is a cycle.
/// </summary>
internal sealed class RefTrail
{
    public static readonly RefTrail Start = new(null, null);

    private readonly RefTrail? _outer;
    private readonly string? _ruleset;

    private RefTrail(RefTrail? outer, string? ruleset)
    {
        _outer = outer;
        _ruleset = ruleset;
    }

    /// <summary>The trail one ruleset deeper, or null when <paramref name="ruleset"/> is already on this trail.</summary>
    public RefTrail? Enter(string ruleset) => Contains(ruleset) ? null : new RefTrail(this, ruleset);

    private bool Contains(string ruleset)
    {
        for (var step = this; step is not null; step = step._outer)
            if (string.Equals(step._ruleset, ruleset, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
