# MessSharp exploratory testing: suppression, modern-C# metrics, and variable naming

Date: 2026-10-03  
Repository: `quality-gates/messharp`  
Base: `5c7c8dc` (`origin/main`)  
Mode: unattended CLI run through the supported Docker wrapper

## Scope and starting state

This pass followed three user journeys:

1. Suppress one intentional exception, as the README describes.
2. Read complexity metrics for modern C# constructs.
3. Apply the variable naming rules to every construct that declares a variable.

Baseline checks before the pass:

- `scripts/dotnet.sh build -c Release`: 0 warnings, 0 errors.
- `scripts/dotnet.sh test -c Release --no-build`: 719 passed, 0 failed.

All runs used [ms.sh](ms.sh), which runs the Release-built `messharp.dll` through `scripts/dotnet.sh`. Fixtures are in [fixtures/](fixtures/) and captured outputs are in [out/](out/).

Reference comparisons used the phpmd 2.15.0 release phar under `thecodingmachine/php:8.3-v5-slim-cli` (`--cpus=2 --memory=2g`). The PHP fixtures are in `fixtures/*-reference.php` and the output is in [out/phpmd-2.15.0-reference.txt](out/phpmd-2.15.0-reference.txt).

## Confirmed bugs

| # | Bug | Issue |
| :-- | :--- | :--- |
| 1 | `[SuppressMessage]` / `@SuppressWarnings` on a property, or on its accessor, does not suppress violations inside the accessor | [#226](https://github.com/quality-gates/messharp/issues/226) |
| 2 | `ShortVariable` and `LongVariable` skip lambda parameters, which phpmd 2.15.0 reports | [#227](https://github.com/quality-gates/messharp/issues/227) |

Confirmed but already tracked as evidence in open issue [#185](https://github.com/quality-gates/messharp/issues/185), so they were not filed again:

| Bug | Evidence |
| :--- | :--- |
| The default `csharp` ruleset reports `CamelCaseMethodName` for every block-bodied property accessor (`get_Count`, `set_Count`). `BooleanGetMethodName` (naming) reports `get_Ready()` for a `bool` property. | [out/j1-accessor-name.txt](out/j1-accessor-name.txt), [out/j1-bool-accessor.txt](out/j1-bool-accessor.txt) |
| No rule sees indexer bodies or event `add`/`remove` bodies. An indexer getter with CCN 13 is not reported; the same body as a method is. | [out/j1-fat-indexer.txt](out/j1-fat-indexer.txt), [out/j1-member-kinds.txt](out/j1-member-kinds.txt) |

Bug 1 makes the accessor-name false positive worse. A user who adds `[SuppressMessage("PHPMD", "CamelCaseMethodName")]` to the property still sees `get_Count` reported ([out/j1-suppress-accessor-name.txt](out/j1-suppress-accessor-name.txt)).

---

## Journey 1: Suppress one intentional exception

**Goal:** keep a rule enabled but silence one known finding on one member, as `README.md` § "Suppress one intentional exception" describes, and see it again with `--strict`.

Fixtures: [Suppress.cs](fixtures/Suppress.cs), [SuppressProperty.cs](fixtures/SuppressProperty.cs), [SuppressPropertyMin.cs](fixtures/SuppressPropertyMin.cs), [team.xml](fixtures/team.xml).

Observations:

- Method, constructor, operator, struct and record suppressions all work (`[SuppressMessage("PHPMD", "ShortVariable")]` and `/** @SuppressWarnings(PHPMD.ShortVariable) */`) ([out/j1-suppress.txt](out/j1-suppress.txt)).
- Suppression is ignored on a property, on a property accessor, and on a local function (lines 17, 28 and 34 in `Suppress.cs`). The property and accessor cases are **Bug 1** (#226). The local-function case is unresolved, because phpmd has no equivalent.
- Replay of `SuppressPropertyMin.cs` from a clean build, run twice: `b` on line 9 is reported both times, and the identical method-level suppression on line 6 works ([out/j1-suppress-property-min.txt](out/j1-suppress-property-min.txt)).
- `--strict` works: both suppressed and unsuppressed findings appear ([out/j1-strict.txt](out/j1-strict.txt)).
- The ruleset variation `<rule ref="csharp"><exclude name="ShortVariable"/></rule>` loads and behaves as documented ([out/j1-team-exclude.txt](out/j1-team-exclude.txt)). This run also surfaced the `get_SuppressedProperty` `CamelCaseMethodName` finding (#185).
- Rejected candidate: `@SuppressWarnings` comment matching uses substring containment. No bundled rule name is a substring of another, so a suppression for one rule cannot silence a different rule.

## Journey 2: Complexity metrics on modern C#

**Goal:** `CyclomaticComplexity` and `NPathComplexity` give sensible values for `??`, `??=`, `when` guards, switch expressions, catch filters, lambdas and local functions, consistent with the documented decision points (`CyclomaticMetrics.cs`, `docs/PORT.md`).

Fixtures: [ModernMetrics.cs](fixtures/ModernMetrics.cs) with [metrics-level1.xml](fixtures/metrics-level1.xml) (report everything), plus [ManyProps.cs](fixtures/ManyProps.cs).  
Output: [out/j2-modern-metrics.txt](out/j2-modern-metrics.txt), [out/j2-many-props.txt](out/j2-many-props.txt).

Observations:

- Switch statements and switch expressions give matching values (CCN 3, NPath 3). The discard arm is not counted.
- Lambda and local-function bodies count toward the enclosing method's CCN.
- **Unresolved (not filed):** `x = x ?? y` adds 1 to CCN, but `x ??= y` adds 0. MessSharp lists `??` as a decision point on purpose (a messgo port decision in `docs/PORT.md`), and `??=` is not listed. phpmd 2.15.0 counts neither (`coalesce()` and `coalesceAssign()` both have CCN 1). Whether `??=` should follow `??` is a design decision for the maintainers.
- **Unresolved (not filed):** `when` guards on `case` labels and catch filters add nothing. A guard is a branch, but phpmd has no equivalent, so there is no reference behaviour to compare against.
- `NPathComplexity` does not count `??`, but CCN does. This is consistent with phpmd, which counts neither.
- A class with 15 block-bodied properties does not trigger `TooManyMethods` or `TooManyPublicMethods`. The synthetic `get_`/`set_` names match the default `ignorepattern`.

## Journey 3: Variable naming across declaration forms

**Goal:** `ShortVariable` and `LongVariable` check every C# construct that introduces a variable, and match phpmd where phpmd has an equivalent.

Fixtures: [ShortNames.cs](fixtures/ShortNames.cs), [LongNames.cs](fixtures/LongNames.cs), [LambdaParams.cs](fixtures/LambdaParams.cs), [ForeachShort.cs](fixtures/ForeachShort.cs), and the PHP references.  
Output: [out/j3-short-names.txt](out/j3-short-names.txt), [out/j3-long-names.txt](out/j3-long-names.txt), [out/j3-replay.txt](out/j3-replay.txt).

Observations:

- These are reported: plain locals, foreach variables, deconstruction, `is` patterns, `out var`, `using var`, and method parameters. For-loop initialisers and catch variables are exempt, as in phpmd.
- Lambda parameters (`j`, `k`, `l`), LINQ range variables (`m`) and local-function parameters (`n`) are never checked. phpmd 2.15.0 reports both short and long closure parameters. This is **Bug 2** (#227), replayed twice.
- **Unresolved (not filed):** phpmd 2.15.0 does **not** report a short `foreach` variable (`$b`), but it **does** report a long one. MessSharp reports both. The short-foreach behaviour was added on purpose in #86 without a phpmd comparison. Whether to follow phpmd here is a maintainer decision.

## Usability observations

- The README shows only `--disable` and ruleset `<exclude>` for exceptions. Inline suppression (`[SuppressMessage("PHPMD", "<Rule>")]`, `@SuppressWarnings(PHPMD.<Rule>)`) works on methods and types but is not documented. Users of `--strict` have to discover the syntax from the source. *Suggestion:* document both forms and the supported targets.
- JSON violations from `ShortVariable` have empty `class` and `method` fields ([out/j1-suppress-property.json](out/j1-suppress-property.json)). Consumers cannot tell which member a finding belongs to without the line number.

## Limitations

- The phpmd comparisons ran PHP-shaped equivalents. Where C# has no PHP counterpart (local functions, LINQ, `when` guards, accessors), there is no reference and the candidate is left unresolved.
- Only the `text` and `json` renderers were used.

## Replay

```console
scripts/dotnet.sh build -c Release
D=docs/exploratory-testing/2026-10-03-suppression-metrics-naming
$D/ms.sh $D/fixtures/SuppressPropertyMin.cs text naming --only ShortVariable   # Bug 1
$D/ms.sh $D/fixtures/LambdaParams.cs text naming --only ShortVariable,LongVariable   # Bug 2
$D/ms.sh $D/fixtures/AccessorName.cs text csharp   # #185 accessor names
$D/ms.sh $D/fixtures/FatIndexer.cs text codesize --only CyclomaticComplexity   # #185 indexers
```
