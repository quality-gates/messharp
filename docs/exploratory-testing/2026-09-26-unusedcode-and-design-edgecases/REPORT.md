# MessSharp exploratory testing: unused code, design, and naming edge cases

Date: 2026-09-26  
Repository: `quality-gates/messharp`  
Base: `699b0ea` (`origin/main`, tag: `v0.2.18`)  
Mode: unattended CLI run through the supported Docker wrapper

## Scope and starting state

This exploratory pass exercises modern C# language features (tuple deconstruction, generic type qualification, expression bodies, property accessors, operator overloads, primary constructors, and out-argument assignments) against the `unusedcode`, `design`, `naming`, `cleancode`, and `controversial` rulesets. It evaluates whether rule behavior and mutation tracking match documented semantics and idiomatic C# conventions.

Before the CLI was exercised, the baseline checks passed:

- `scripts/dotnet.sh build -c Release`: 0 warnings, 0 errors.
- `scripts/dotnet.sh test -c Release --no-build`: 655 passed, 0 failed.

All test runs used [ms.sh](ms.sh), which executes the Release-built `messharp.dll` through `scripts/dotnet.sh`. Fixtures are located in [fixtures/](fixtures/), and captured outputs are in [out/](out/).

---

## Journey 1: Parameter and local lifecycle in modern C#

**Goal:** `unusedcode` rules correctly distinguish used vs. unused formal parameters, local variables, and private fields under modern C# assignment constructs (tuple deconstruction, out-arguments, discards, and pattern matching).

Fixture: [fixtures/UnusedCodeCases.cs](fixtures/UnusedCodeCases.cs), [fixtures/ReproOutTuple.cs](fixtures/ReproOutTuple.cs).  
Output: [out/j1-unusedcode.txt](out/j1-unusedcode.txt), [out/j1-repro-outtuple.txt](out/j1-repro-outtuple.txt).

### Findings:

1. **`UnusedFormalParameter` false positive on tuple deconstruction:**
   - In `OutTuple(out int x, out int y)`, `(x, y) = (10, 20);` assigns both `out` parameters as required by the C# compiler (CS0177).
   - `UnusedFormalParameterRule` falsely reported both `x` and `y` as unused parameters (`Avoid unused parameters such as 'x'`).
   - Differential control: `OutScalar(out int x, out int y)` with scalar assignments `x = 1; y = 2;` stayed completely clean (0 findings).
   - **Confirmed: Bug 1** (filed as [#188](https://github.com/quality-gates/messharp/issues/188)).

2. **`UnusedLocalVariable` on out-arguments:**
   - `Helper(out var deadVar);` was correctly flagged on line 49 (`Avoid unused local variables such as 'deadVar'`).
   - `int deadOut; Helper(out deadOut);` was **not** reported. `WriteIdentCollector` only tracks `AssignmentExpressionSyntax` targets as pure writes, so `out deadOut` in an `ArgumentSyntax` is treated as an identifier read.

3. **Pattern matching, deconstructions, and fields:**
   - Deconstruction local variable `var (usedLocal, deadLocal) = (1, 2);` correctly reported `deadLocal` while keeping `usedLocal` clean.
   - Direct `is` declaration-pattern `if (obj is string deadPatternVar)` correctly reported `deadPatternVar`.
   - Field analysis in `UnusedPrivateField` correctly identified write-only fields (`_deadAssignedOnly`, `_deadTupleAssignedOnly`) and recognized `nameof(...)` and string interpolation `$"Val: {_usedInInterpolation}"` as reads.

---

## Journey 2: Mutation-aware static state and design rules

**Goal:** `GlobalVariableRule` accurately identifies mutated static state, and design rules (`EmptyCatchBlock`, `CountInLoopExpression`) inspect method bodies correctly.

Fixture: [fixtures/DesignCases.cs](fixtures/DesignCases.cs), [fixtures/ReproGlobalVariable.cs](fixtures/ReproGlobalVariable.cs).  
Output: [out/j2-design.txt](out/j2-design.txt), [out/j2-repro-globalvariable.txt](out/j2-repro-globalvariable.txt).

### Findings:

1. **`GlobalVariableRule` false negative on generic-qualified and tuple-deconstructed mutations:**
   - In generic class `GenericCache<T>`, the static field `Counter` is mutated via `GenericCache<T>.Counter++;`. `GlobalVariableRule` failed to detect this mutation because `ExtractSimpleOrQualifiedName` requires `ma.Expression is IdentifierNameSyntax`, whereas `GenericCache<T>` is a `GenericNameSyntax`.
   - In `StaticTupleCases`, static fields `StateA` and `StateB` are mutated via `(StateA, StateB) = (1, 2);`. `GlobalVariableRule` failed to detect this mutation because `assign.Left` is a `TupleExpressionSyntax`.
   - Under the default `report-immutable=false` setting, both `Counter`, `StateA`, and `StateB` escaped detection completely.
   - Differential controls: `NonGenericCacheControl.NormalCounter` (qualified with a non-generic class name) and `ScalarStaticControl` (scalar assignments) were both correctly reported.
   - **Confirmed: Bug 2** (filed as [#189](https://github.com/quality-gates/messharp/issues/189)).

2. **Design rules on block bodies:**
   - `EmptyCatchBlockRule` correctly reported empty catches (`NormalEmptyCatch`) and comment-only catches (`CommentOnlyCatch`).
   - `CountInLoopExpressionRule` correctly flagged `.Count` in `for (int i = 0; i < _list.Count; i++)`.

---

## Journey 3: Method and naming conventions across member kinds

**Goal:** `naming`, `cleancode`, and `controversial` rules respect C# member syntax (enums, dictionary initializers, property accessors, operators).

Fixture: [fixtures/CleanCodeAndNamingCases.cs](fixtures/CleanCodeAndNamingCases.cs), [fixtures/SpecialMethods.cs](fixtures/SpecialMethods.cs), [fixtures/BoolPropClass.cs](fixtures/BoolPropClass.cs).  
Output: [out/j3-cleancode-naming.txt](out/j3-cleancode-naming.txt).

### Findings:

1. **Enum constants and dictionary keys:**
   - `ConstantNamingConventions` correctly flagged `INACTIVE_STATUS` and `pending_status` in an enum while accepting `ActiveStatus`.
   - `DuplicatedArrayKey` detected duplicate string literal keys `["duplicate_key"]` and negative number keys `[-1]`.

2. **Synthetic member names in naming rules (already tracked):**
   - In `SpecialMethods.cs`, `CamelCaseMethodName` flagged `get_MyProp`, `set_MyProp`, `operator +`, and `~SpecialMethods`.
   - In `BoolPropClass.cs`, `BooleanGetMethodName` flagged `get_IsValid` on a boolean property already named `IsValid`.
   - This occurs because `MethodModelBuilder` and `PropertyAccessorModelBuilder` create `MethodModel` entries for non-method executable members without a member-kind discriminator.
   - **Status:** This gap is already comprehensively documented and tracked in issue [#185](https://github.com/quality-gates/messharp/issues/185). Per `docs/agents/issue-tracker.md`, duplicate issue creation was skipped.

---

## Confirmed bugs

| ID | Issue | Area | Replays | Status |
| :--- | :--- | :--- | :--: | :--- |
| 1 | [#188](https://github.com/quality-gates/messharp/issues/188) | `UnusedFormalParameterRule` falsely reports `out` parameters assigned via tuple deconstruction | 3 + scalar control | Filed (`bug, ready-for-agent`) |
| 2 | [#189](https://github.com/quality-gates/messharp/issues/189) | `GlobalVariableRule` misses mutations qualified by generic class name or assigned via tuple deconstruction | 3 + non-generic & scalar controls | Filed (`bug, ready-for-agent`) |

### Bug 1: Out-parameter tuple deconstruction false positive

- **Impact:** Any method that satisfies its `out` parameter contract by assigning them in a tuple (`(x, y) = ...`) produces false positive violations under the `unusedcode` ruleset.
- **Replay:** [fixtures/ReproOutTuple.cs](fixtures/ReproOutTuple.cs), `ms.sh fixtures/ReproOutTuple.cs text unusedcode --only UnusedFormalParameter`.
- **Expected:** Exit code 0, 0 violations.
- **Actual:** Exit code 2, 2 violations ([out/j1-repro-outtuple.txt](out/j1-repro-outtuple.txt)).
- **Root cause:** In `src/MessSharp/Rules/UnusedCode/BodyAnalysis.cs`, `IdentWrites` does not traverse `TupleExpressionSyntax` on the left-hand side of assignments. When `UnusedFormalParameterRule` checks `writes.Contains(p.Name)` for `p.IsOut`, the deconstructed parameters are missing from `writes`.

### Bug 2: GlobalVariableRule generic and tuple mutation misses

- **Impact:** Teams relying on `GlobalVariableRule` to detect mutable static state miss active mutations if the class is generic and accessed via `ClassName<T>.Field` or if multiple statics are updated via `(A, B) = ...`.
- **Replay:** [fixtures/ReproGlobalVariable.cs](fixtures/ReproGlobalVariable.cs), `ms.sh fixtures/ReproGlobalVariable.cs text design --only GlobalVariable`.
- **Expected:** All 5 mutated static fields reported.
- **Actual:** Only the 3 non-generic / scalar controls are reported; generic-qualified and tuple-deconstructed mutations are skipped ([out/j2-repro-globalvariable.txt](out/j2-repro-globalvariable.txt)).
- **Root cause:** In `src/MessSharp/Rules/Design/GlobalVariableRule.cs`, `ExtractSimpleOrQualifiedName` accepts only `IdentifierNameSyntax` for the qualifier (missing `GenericNameSyntax`), and `ExtractMutationTarget` passes `assign.Left` directly without decomposing `TupleExpressionSyntax`.

---

## Report formats and CLI pipeline verification

All CLI report formats were verified against the reproducer fixture:

- **SARIF:** [out/formats-sarif.json](out/formats-sarif.json) produced valid SARIF 2.1.0 with rule driver metadata and exact file/line spans.
- **GitHub:** [out/formats-github.txt](out/formats-github.txt) emitted valid `::warning file=...::` workflow commands.
- **XML:** [out/formats-xml.xml](out/formats-xml.xml) produced well-formed PMD-compatible XML.
- **Exit codes:** Exit code 2 on violations, exit code 0 on clean runs, preserved across all formats.

---

## Observations not filed

- **`UnusedLocalVariable` with pre-declared `out` arguments:** `int x; Helper(out x);` is treated as a read of `x` by `BodyAnalysis.IdentReads` because `WriteIdentCollector` only registers `AssignmentExpressionSyntax` targets. If `x` is never subsequently read, no unused variable violation is reported. Conversely, `Helper(out var x);` is reported as unused.
- **Member kind classification for naming rules:** Accessor methods (`get_X`, `set_X`) and operators (`operator +`) are checked by `CamelCaseMethodName` and `BooleanGetMethodName`. This is already tracked under [#185](https://github.com/quality-gates/messharp/issues/185).
