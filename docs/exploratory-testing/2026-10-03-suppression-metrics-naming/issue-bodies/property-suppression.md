## Summary

`[SuppressMessage]` and `@SuppressWarnings` on a property do not suppress violations reported inside that property's accessor bodies. The same attribute on a method works. An attribute placed directly on the accessor (`[SuppressMessage(...)] get { ... }`) is also ignored.

User impact: a team cannot record an intentional exception on a property. This includes the `CamelCaseMethodName` finding that the default `csharp` ruleset reports for every block-bodied accessor (see #185). The only remaining options are `--disable` or a ruleset `<exclude>`, and both turn the rule off for the whole run.

## Reproduction

Commit: `5c7c8dc` (`main`), Release build via `scripts/dotnet.sh build -c Release`.

`SuppressPropertyMin.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

public class SuppressPropertyMin
{
    [SuppressMessage("PHPMD", "ShortVariable")]
    public int SuppressedMethod() { int a = 1; return a; }

    [SuppressMessage("PHPMD", "ShortVariable")]
    public int SuppressedProperty { get { int b = 1; return b; } }
}
```

```console
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll SuppressPropertyMin.cs text naming --only ShortVariable
```

Actual (replayed twice, exit 2):

```
SuppressPropertyMin.cs:9  ShortVariable  Avoid variables with short names like b. Configured minimum length is 3.
```

Control: the identical attribute on `SuppressedMethod` (line 6) suppresses `a`. With `--strict`, both `a` and `b` are listed, which shows that only the method-level suppression was applied.

Same result for:
- `/** @SuppressWarnings(PHPMD.ShortVariable) */` above the property
- `[SuppressMessage("PHPMD", "ShortVariable")]` on the `get` accessor itself
- `[SuppressMessage("PHPMD", "CamelCaseMethodName")]` on a property under the `csharp` ruleset: `get_Count` is still reported

An indexer with the same attribute is not reported, but that is because indexer bodies are not analysed at all (#185), not because the suppression works.

## Expected

A suppression on a property applies to violations inside its accessors, in the same way that a suppression on a method applies to its body. A suppression on an accessor applies to that accessor.

## Likely cause

`PropertyAccessorModelBuilder` sets `MethodModel.Node` to the `AccessorDeclarationSyntax`. `SuppressionMatcher.GetAttributeLists` only reads attributes from `MemberDeclarationSyntax` (and the compilation unit). `AccessorDeclarationSyntax` is not a `MemberDeclarationSyntax`, so it returns nothing. `MatchesComments` reads the accessor's leading trivia, not the property's. The enclosing `PropertyDeclarationSyntax` is never checked.

Evidence: `docs/exploratory-testing/2026-10-03-suppression-metrics-naming/` (fixtures `SuppressPropertyMin.cs`, `SuppressProperty.cs`, `SuppressAccessorName.cs`; outputs `out/j1-*`).
