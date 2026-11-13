namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Whether an effect is known to happen when the analysis reaches it. A Definite effect replaces what was there (a
/// strong update); a Possible one only adds to it (a weak update), because it may not run, may run on only some of
/// several objects, or may already have run. Once an effect is Possible, anything it leads to is Possible too.
/// </summary>
internal readonly record struct Certainty
{
    public static readonly Certainty Definite = default;
    public static readonly Certainty Possible = new(true);

    private Certainty(bool possible) => IsPossible = possible;

    public bool IsPossible { get; }

    public bool IsDefinite => !IsPossible;

    /// <summary>Possible when either side is: one doubtful step makes everything after it doubtful.</summary>
    public Certainty OrIf(bool doubtful) => new(doubtful || IsPossible);
}
