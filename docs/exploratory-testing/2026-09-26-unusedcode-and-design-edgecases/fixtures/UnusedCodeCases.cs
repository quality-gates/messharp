namespace ExploratoryTesting.UnusedCode;

public class UnusedCodeCases
{
    // --- UnusedFormalParameter cases ---

    // Out parameter assigned via simple assignment (should be clean)
    public void OutSimple(out int x)
    {
        x = 42;
    }

    // Out parameter assigned via tuple deconstruction
    public void OutTuple(out int x, out int y)
    {
        (x, y) = (10, 20);
    }

    // Out parameter passed as out to helper method (should be clean)
    public void OutChained(out int x)
    {
        Helper(out x);
    }

    private static void Helper(out int val) => val = 99;

    // Unused parameter (should be reported)
    public void UnusedParam(int deadParam)
    {
    }

    // Discard parameter (should NOT be reported)
    public void DiscardParam(int _)
    {
    }

    // --- UnusedLocalVariable cases ---

    // Local variable declared, passed as out argument, but never read afterwards
    public void LocalOutDeclaredOnly()
    {
        int deadOut;
        Helper(out deadOut);
    }

    // Out var local never read afterwards
    public void LocalOutVarOnly()
    {
        Helper(out var deadVar);
    }

    // Deconstruction local: one used, one unused
    public int LocalDeconstructionUnused()
    {
        var (usedLocal, deadLocal) = (1, 2);
        return usedLocal;
    }

    // Pattern matching is-pattern local: unused
    public void PatternMatchingUnused(object obj)
    {
        if (obj is string deadPatternVar)
        {
            Console.WriteLine("Matched");
        }
    }

    // --- UnusedPrivateField cases ---

    private int _deadField;
    private int _deadAssignedOnly;
    private int _deadTupleAssignedOnly;
    private int _usedInNameof;
    private int _usedInInterpolation;

    public void TouchFields()
    {
        _deadAssignedOnly = 1;
        (_deadTupleAssignedOnly, _) = (2, 3);
        _ = nameof(_usedInNameof);
        Console.WriteLine($"Val: {_usedInInterpolation}");
    }
}
