# MessSharp exploratory testing: explicitness rulesets

Date: 2026-09-23  
Repository: `quality-gates/messharp`  
Base: `24f4a02` (`origin/main`) plus the uncommitted `explicitness` diff on `feat/explicitness-ruleset`  
Mode: unattended CLI run through the supported Docker wrapper

## Scope and starting state

This pass tests the new opt-in `explicitness` and `explicitness-strict` rulesets as a C# team would meet them. The team runs them on a service, publishes the results, tunes a policy, and checks that findings match what the rules promise in `docs/rules.md`.

Before the CLI was exercised, the baseline checks passed:

- `scripts/dotnet.sh build -c Release`: 0 errors.
- `scripts/dotnet.sh test -c Release --no-build`: 644 passed, 0 failed.

All runs used [ms.sh](ms.sh), which runs the Release-built `messharp.dll` through `scripts/dotnet.sh`. Fixtures are in [fixtures/](fixtures/), and outputs are in [out/](out/).

## Journey 1: find implicit inputs and outputs in a service

**Goal:** `explicitness` flags the actions in a realistic class, leaves its calculations alone, and works with every report format and with suppression.

Fixture: [fixtures/app/Billing.cs](fixtures/app/Billing.cs).

- The ruleset reported 7 findings, and the process exited 2. Reads and writes of a mutable static, `DateTime.Now`, `Console.WriteLine`, a change to a `static readonly` list, `File.WriteAllText` and an `out` write were all reported. The pure `Gross()` and the `const` read were clean ([out/j1-text.txt](out/j1-text.txt)).
- `AddLine(List<decimal> lines, decimal amount) => lines.Add(amount);` was **not** reported, although the same call in a block body is. **Confirmed: bug A.**
- JSON, XML, SARIF, Checkstyle and GitHub output all exited 2. JSON and SARIF parsed, XML and Checkstyle were well-formed, and every format carried 7 results with the `docs/rules.md#explicitness` help URL.
- Variation: `[SuppressMessage("PHPMD", "ImplicitInput")]` hid `Stamp()`, and `--strict` brought it back.
- JSON `class` and `method` are empty for these findings. Rejected as a bug: existing line-anchored method rules (`ElseExpression`, `DevelopmentCodeFragment`) report the same way through `ctx.Report`.

## Journey 2: opt into the strict rules and tune a policy

**Goal:** a team can add, combine and trim the rulesets as `docs/rules.md` and the README describe.

Output: [out/j2.txt](out/j2.txt), [out/j2-refs.txt](out/j2-refs.txt), [out/j2-instance-only.txt](out/j2-instance-only.txt).

- `explicitness-strict` added `ImplicitInstanceInput` for `Apply()`. `explicitness,explicitness-strict` gave the same 8 findings with no duplicates.
- `EXPLICITNESS` (case-insensitive), `rulesets/explicitness.xml` and `csharp,explicitness --minimumpriority 3` all worked.
- [policy.xml](fixtures/policy.xml) used `<exclude name="ImplicitOutput"/>` and raised `ImplicitInstanceInput` to priority 1. Both took effect, and `--minimumpriority 1` kept only the priority-1 finding.
- [policy-instance-only.xml](fixtures/policy-instance-only.xml) excluded the two base rules from `explicitness-strict`, which pulls them in through a nested ref. Only the instance rule remained.
- Variation: `ref="explicitness-strict.xml"` failed with `Cannot resolve ref`. Rejected: `ref="cleancode.xml"` fails the same way, so this is the loader's existing convention, not part of this diff.

## Journey 3: rule accuracy on tricky code

**Goal:** the findings match the promises and limitations in `docs/rules.md`.

Fixtures: [fixtures/accuracy/](fixtures/accuracy/) (partial class across two files, 26 cases), [fixtures/strict/Cart.cs](fixtures/strict/Cart.cs), [fixtures/arrow-scope/Scope.cs](fixtures/arrow-scope/Scope.cs).

Correct:

- A static declared in the other file of a partial class.
- Shadowing by a local, a lambda parameter or a pattern.
- A get-only static, a `static readonly` lock object and `nameof`.
- `Interlocked.Increment(ref _hits)`.
- Array-element and `?.` changes to arguments.
- Tuple writes to statics.
- Seeded and unseeded `Random`.
- The class-qualified `Counter._hits`.
- In strict mode: accessors, primary-constructor parameters, `Color Color`, constructors and statics exempt, and object-initializer keys on a new instance.

Findings:

- `Cache<T>._value` inside `Cache<T>` was not reported as a read or a write, but `Counter._hits` inside `Counter` was. **Confirmed: bug B.**
- `void A16(List<int> o) => o?.Add(1);`, `void Reset() => this._items.Clear();` (strict), and the arrow-bodied async `Task`, `set`, constructor and local-function members were not reported. These are **bug A** again.
- An arrow-bodied destructor was invisible to every rule, including `DevelopmentCodeFragment` ([fixtures/dtor/](fixtures/dtor/), [out/dtor.txt](out/dtor.txt)). **Confirmed, but pre-existing (bug D).** `Models.cs` is unchanged from `origin/main`. This is a gap left by #167.
- Not bugs, per the documented limitations:
  - An outer class's static read from a nested class: only the class's own state is tracked.
  - `xs.ForEach(...)` reported as changing `xs`: the discarded-call heuristic.
- **C, resolved as a limitation:** `xs.ForEach(x => sink.Add(x))` does not report `sink`. Whether a lambda's value is discarded depends on its delegate type (`Action` or `Func`), which syntax-only analysis cannot see. `docs/rules.md` now says so.

## Confirmed bugs

| ID | Area | In this diff? | Replays | Status |
| :--- | :--- | :--: | :--: | :--- |
| A | Discarded call in an expression body of a member that returns nothing is not a change | yes | 2 + block-body and `bool`-return controls | fixed in branch |
| B | Static access qualified by the class's generic name (`Cache<T>._value`) is missed | yes | 2 + bare and non-generic controls | fixed in branch |
| D | Expression-bodied destructors are invisible to all body-based rules | no (pre-existing) | 1 + block-body control | filed: [#180](https://github.com/quality-gates/messharp/issues/180) ([body](issue-bodies/D-arrow-bodied-destructor.md)) |

A and B are in unreleased, unpushed code, so they were fixed directly in `feat/explicitness-ruleset` and not filed.

### A: void expression bodies

- **Impact:** `void Add(int x) => items.Add(x);` is as much an action as its block-body form, but it passed the gate. This affects both rules that report changes (`ImplicitOutput`, `ImplicitInstanceOutput`).
- **Replay:** [fixtures/arrow/Arrow.cs](fixtures/arrow/Arrow.cs), `ms.sh docs/exploratory-testing/2026-09-23-explicitness-rulesets/fixtures/arrow text explicitness`.
- **Expected:** lines 6 and 8 report `changes argument 'xs'` / `changes the object in static member '_log'`, as lines 5 and 7 do.
- **Actual:** only the block bodies are reported ([out/arrow.txt](out/arrow.txt)).

### B: generic class qualifier

- **Impact:** in a generic class, the qualified form hides a mutable-static read or write from both rules.
- **Replay:** [fixtures/generic/Cache.cs](fixtures/generic/Cache.cs).
- **Expected:** `Qualified()` reports a read and `QualifiedWrite()` reports a write of `_value`.
- **Actual:** only `Bare()` is reported ([out/generic.txt](out/generic.txt)).

## Diagnosis (`/diagnosing-bugs`)

1. **Loop:** [loop.sh](loop.sh) does an incremental Release build, runs the CLI on the two minimal fixtures and diffs the output against [expected-loop.txt](expected-loop.txt). It took 6.7 s. It went RED on exactly the 4 missing findings, and `Kept()` (a returned value) was the control that must stay clean.
2. **Minimised:** `static void M(List<int> xs) => xs.Add(1);` and `class C<T> { static int _v; static int M() => C<T>._v; }`. Every element is load-bearing: the block body, a `bool` return and a non-generic qualifier each turn it green.
3. **Hypotheses, ranked:**
   - A1: `DiscardedCallReceiver` matches only `ExpressionStatementSyntax`.
   - A2: `EffectiveBody` is the bare expression, whose parent is the `ArrowExpressionClause`.
   - A3: the parameter resolver fails for arrow bodies.
   - A4: the rule skips arrow bodies. Already falsified by the arrow-bodied A9 and A13.
   - B1: the qualifier check accepts only `IdentifierNameSyntax`.
   - B2: `ClassModel.Name` is not the bare name.
   - B3: `C<T>._v` parses as a `QualifiedName`.
4. **Instrumented** with `[DEBUG-ex7q]` ([out/probe.txt](out/probe.txt)):
   - `statements=0`, `body=InvocationExpression parent=ArrowExpressionClause`: A1 and A2 confirmed.
   - `P() => a[0] = 1` is reported: A3 falsified.
   - `className=C qualifier=GenericName parent=SimpleMemberAccessExpression`: B1 confirmed, B2 and B3 falsified.
5. **Fix and regression tests.** The correct seam is `Engine.Analyze` over parsed source with the bundled ruleset, in `ExplicitnessRulesTests`. The 7 new cases failed first, then passed after the fix. A value-returning control stayed green.
   - A: new `ExpressionBodies.DiscardsValue` covers `void`, `async` + `Task`/`ValueTask`, constructors, and non-`get` accessors. `StateAccessCollector.DiscardedExpression` treats such a body like an expression statement.
   - B: the static qualifier check accepts any `SimpleNameSyntax` (identifier or generic name) with the class's name.
6. **Cleanup:**
   - The loop is GREEN.
   - Re-running the original fixtures adds only the expected findings: `AddLine`, `A16`, `Get`, `Reset` and the arrow-scope members.
   - `grep DEBUG-` finds nothing.
   - The full pre-commit hook passes (format, Release build with 0 warnings, 652 tests, self-analysis).

Suggested commit message line for the correct hypotheses: *"Discarded-call detection only matched expression statements, so the expression body of a member returning nothing was never treated as discarded (A1+A2); the static-member qualifier check accepted only IdentifierName, so `Cache<T>.x` (GenericName) was missed (B1)."*

## Observations not filed

- A generic qualifier with different type arguments (`Cache<int>._v` inside `Cache<T>`) is also counted. That is still the class's own mutable static, only a different instantiation. A non-generic `Foo` that uses a same-named generic `Foo<T>` as a qualifier would be a false positive, but that combination is unusual.
- SARIF `tool.driver.rules[].properties` has no ruleset name. This is not specific to this diff.

## Limitations

- The fixtures are syntax-only, not a multi-project solution.
- The docs were updated to cover A and C. CHANGELOG needed no change, because the feature's `[Unreleased]` "Added" entry covers the fixed behaviour.
