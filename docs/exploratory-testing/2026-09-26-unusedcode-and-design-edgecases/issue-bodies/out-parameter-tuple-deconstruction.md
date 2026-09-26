## Summary

`UnusedFormalParameterRule` flags `out` parameters as unused when they are assigned using tuple deconstruction assignment (`(x, y) = (1, 2);`).

In C#, assigning multiple `out` parameters via tuple deconstruction is a standard idiom. The C# compiler requires all `out` parameters to be assigned before return (CS0177); assigning them via tuple deconstruction satisfies this requirement. However, `UnusedFormalParameterRule` fails to recognize tuple deconstruction assignments as parameter writes and reports false positive violations for each parameter.

## Reproduction

Repository: `quality-gates/messharp`  
Commit: `699b0ea`

Fixture `ReproOutTuple.cs`:

```csharp
public class ReproOutTuple
{
    public void OutTuple(out int x, out int y)
    {
        (x, y) = (1, 2);
    }

    public void OutScalar(out int x, out int y)
    {
        x = 1;
        y = 2;
    }
}
```

Command:

```console
scripts/dotnet.sh run --project src/MessSharp -c Release --no-build -- ReproOutTuple.cs text unusedcode --only UnusedFormalParameter
```

Actual result (reproduced multiple times, exit code 2):

```
ReproOutTuple.cs:3  UnusedFormalParameter  Avoid unused parameters such as 'x'.
ReproOutTuple.cs:3  UnusedFormalParameter  Avoid unused parameters such as 'y'.
```

Differential control: `OutScalar` (line 8), where `x` and `y` are assigned via separate scalar assignments (`x = 1; y = 2;`), produces zero findings and exits 0.

## Expected

No violations reported; exit code 0. Both `x` and `y` are assigned before return via `(x, y) = (1, 2);` and should be recognized as written `out` parameters.

## Root Cause

In `src/MessSharp/Rules/UnusedCode/BodyAnalysis.cs`:

```csharp
internal static HashSet<string> IdentWrites(SyntaxNode body)
{
    var writes = new HashSet<string>(StringComparer.Ordinal);
    foreach (var node in body.DescendantNodesAndSelf())
    {
        if (node is AssignmentExpressionSyntax aes && aes.Left is IdentifierNameSyntax lhsId)
            writes.Add(lhsId.Identifier.Text);
        else if (node is ArgumentSyntax arg && arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword) && arg.Expression is IdentifierNameSyntax outId)
            writes.Add(outId.Identifier.Text);
    }
    return writes;
}
```

When an assignment target is a tuple deconstruction (`aes.Left is TupleExpressionSyntax tuple`), `IdentWrites` does not recurse into `tuple.Arguments`. While #140 updated `WriteIdentCollector` to handle tuples on assignment LHS, `BodyAnalysis.IdentWrites` was not updated.

In `UnusedFormalParameterRule.cs`:
1. `reads` comes from `BodyAnalysis.IdentReads(body)`. `WriteIdentCollector` correctly recognizes `x` and `y` as write targets, so they are not in `reads`.
2. `UnusedFormalParameterRule` checks `if (p.IsOut)`, which triggers `writes ??= BodyAnalysis.IdentWrites(body);`.
3. Because `IdentWrites` does not traverse tuple deconstruction, `writes.Contains("x")` and `writes.Contains("y")` return `false`.
4. The rule reports false positives for both parameters.

## Suggested Fix

In `BodyAnalysis.IdentWrites`, when `aes.Left is TupleExpressionSyntax tuple`, recursively collect identifier names from the tuple's arguments, matching the logic in `WriteIdentCollector.CollectTupleWriteIdents`.
