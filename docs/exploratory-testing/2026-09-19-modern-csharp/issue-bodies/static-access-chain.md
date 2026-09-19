## Summary

`StaticAccess` reports instance method calls on a member-access chain as static access to a class. For `cart.Items.Add(1)`, `Items` is a property of the local `cart`, but the rule reports "static access to class 'Items'". The rule treats the last identifier of any `a.B.M()` receiver as a class name when it starts with an upper-case letter. That behaviour matches namespace-qualified calls (#32), but it also matches ordinary instance property chains.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `627bd26b5cf96b4e1cf6e7911dad57707c6e2383`

`docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/static/Chains.cs`:

```csharp
public class Cart { public List<int> Items { get; } = new(); }

public class Checkout
{
    public int Count(Cart cart) => cart.Items.Count();
    public void Add(Cart cart) => cart.Items.Add(1);
    public int Control(int value) => System.Math.Abs(value);
}
```

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/static text cleancode --only StaticAccess
```

Actual result (reproduced twice, exit 2):

```
Chains.cs:13  StaticAccess  Avoid using static access to class 'Items' in method 'Count'.
Chains.cs:15  StaticAccess  Avoid using static access to class 'Items' in method 'Add'.
Chains.cs:17  StaticAccess  Avoid using static access to class 'Math' in method 'Control'.
```

The `Math` finding is the correct control. The two `Items` findings are false positives.

## Expected

Only calls whose receiver can be a type name should be reported. A chain rooted in a lower-case identifier (a local, parameter or field), `this` or `base` is instance access. `System.Math.Abs` should still be reported.

## Impact

`StaticAccess` is excluded from `csharp`, but it is active in `cleancode` and `opinionated`. For users who enable it, most `obj.Property.Method()` calls become noise.

## Evidence

`docs/exploratory-testing/2026-09-19-modern-csharp/` (REPORT.md, fixtures `static/`, `app/`).
