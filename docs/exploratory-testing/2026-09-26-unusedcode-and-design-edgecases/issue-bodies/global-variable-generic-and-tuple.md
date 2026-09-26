## Summary

`GlobalVariableRule` is mutation-aware: it reports mutable static fields only when they are actually mutated in the class. However, it fails to detect mutations when:
1. The field access is qualified with the class's generic type name (e.g. `GenericCache<T>.Counter++` or `GenericCache<T>.Counter = 1;`).
2. The fields are mutated via tuple deconstruction assignment (e.g. `(StateA, StateB) = (1, 2);`).

In both cases, mutable static fields that are actively modified escape detection and are silently ignored under the default `report-immutable=false` setting.

## Reproduction

Repository: `quality-gates/messharp`  
Commit: `699b0ea`

Fixture `ReproGlobalVariable.cs`:

```csharp
public class GenericCacheRepro<T>
{
    public static int GenericCounter;

    public void IncrQualified()
    {
        GenericCacheRepro<T>.GenericCounter++;
    }
}

public class NonGenericCacheControl
{
    public static int NormalCounter;

    public void IncrQualified()
    {
        NonGenericCacheControl.NormalCounter++;
    }
}

public class TupleStaticRepro
{
    public static int StateA;
    public static int StateB;

    public void ResetTuple()
    {
        (StateA, StateB) = (1, 2);
    }
}

public class ScalarStaticControl
{
    public static int StateA;
    public static int StateB;

    public void ResetScalar()
    {
        StateA = 1;
        StateB = 2;
    }
}
```

Command:

```console
scripts/dotnet.sh run --project src/MessSharp -c Release --no-build -- ReproGlobalVariable.cs text design --only GlobalVariable
```

Actual result (reproduced multiple times, exit code 2):

```
ReproGlobalVariable.cs:13  GlobalVariable  Avoid using static mutable state: NormalCounter.
ReproGlobalVariable.cs:34  GlobalVariable  Avoid using static mutable state: StateA.
ReproGlobalVariable.cs:35  GlobalVariable  Avoid using static mutable state: StateB.
```

`GenericCounter` (line 3) and `TupleStaticRepro.StateA`/`StateB` (lines 23, 24) are completely missing from the report.

Differential controls:
- `NonGenericCacheControl.NormalCounter` (line 13) is detected when qualified by a non-generic class name.
- `ScalarStaticControl` (lines 34, 35) are detected when assigned via scalar assignment.

## Expected

All 5 mutable static fields should be reported, because each is mutated in its declaring class:
- `GenericCounter` on line 3
- `NormalCounter` on line 13
- `TupleStaticRepro.StateA` on line 23
- `TupleStaticRepro.StateB` on line 24
- `ScalarStaticControl.StateA` on line 34
- `ScalarStaticControl.StateB` on line 35

## Root Cause

In `src/MessSharp/Rules/Design/GlobalVariableRule.cs`:

1. Generic class qualifier:
In `ExtractSimpleOrQualifiedName`:
```csharp
if (expr is MemberAccessExpressionSyntax ma &&
    ma.Expression is IdentifierNameSyntax cls2 &&
    cls2.Identifier.Text == className)
    return ma.Name.Identifier.Text;
```
For a generic class `GenericCacheRepro<T>`, `ma.Expression` is a `GenericNameSyntax` (a `SimpleNameSyntax`), not an `IdentifierNameSyntax`. The condition fails and returns `null`. This mirrors the generic qualifier bug previously identified and fixed in the explicitness ruleset (Bug B in the 2026-09-23 report).

2. Tuple deconstruction:
In `ExtractMutationTarget`:
```csharp
if (node is AssignmentExpressionSyntax assign)
    return ExtractSimpleOrQualifiedName(assign.Left, className);
```
When `assign.Left` is a `TupleExpressionSyntax`, `ExtractSimpleOrQualifiedName` returns `null` because it only handles `IdentifierNameSyntax` and `MemberAccessExpressionSyntax`. Target identifiers inside the tuple are never extracted.

## Suggested Fix

1. In `ExtractSimpleOrQualifiedName`, allow `ma.Expression` to be any `SimpleNameSyntax` (both `IdentifierNameSyntax` and `GenericNameSyntax`) where `simpleName.Identifier.Text == className`.
2. In `ExtractMutationTarget` / `FindMutatedFieldNames`, recursively extract mutation targets when `assign.Left` is a `TupleExpressionSyntax`.
