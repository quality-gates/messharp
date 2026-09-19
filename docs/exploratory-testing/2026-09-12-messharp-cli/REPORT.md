# MessSharp exploratory-testing run

Date: 2026-09-12  
Repository: `quality-gates/messharp`  
Commit: `a9c5d3dc8902f3dde0449a5caa2ed723b737d5d8`  
Branch: `main`

## Scope and starting state

This was an unattended CLI run using the supported Docker wrapper. The working tree was clean before the run. No product files were changed; fixtures, reports, and this report are retained under this directory.

Baseline checks completed before exercising the CLI:

- `scripts/dotnet.sh build -c Release` — passed, 0 warnings and 0 errors.
- `scripts/dotnet.sh test -c Release --no-build` — passed, 480 tests, 0 failures.
- `scripts/dotnet.sh run --project src/MessSharp --no-build -c Release -- ./src text csharp --ignore-tests` — exit 0, no output.

## Journey 1: analyze clean, violating, and malformed input

Fixtures: `Clean.cs`, `Violations.cs`, and `Broken.cs`.

- A clean source file produced no findings and exit 0.
- A deliberately violating source produced seven findings and exit 2. The JSON evidence is in [violations.json](reports/violations.json).
- A malformed source produced two Roslyn diagnostics and exit 1. The structured evidence is in [broken.json](reports/broken.json).
- `--ignore-errors-on-exit` preserved the two diagnostics while returning exit 0.
- `--only CyclomaticComplexity`, `--disable CyclomaticComplexity`, and `--ignore-violations-on-exit` behaved as documented.

## Journey 2: reports and custom policy configuration

The existing report directory worked. The violating fixture rendered non-empty output for XML, HTML, GitHub, GitLab, Checkstyle, SARIF, ANSI, and JSON; each command returned exit 2. The outputs are retained in [reports](reports/).

Three configuration defects were confirmed with controls and replays:

1. The documented bare `<rule ref="LongVariable">` form fails with `Cannot resolve ref: LongVariable` and exit 1. The qualified `naming/LongVariable` control loads successfully. Filed as [#114](https://github.com/quality-gates/messharp/issues/114).
2. A `naming/LongVariable` override with `maximum=50` works alone, but is ignored after `csharp` has already supplied the same rule; a 38-character field is still reported under threshold 35. Filed as [#115](https://github.com/quality-gates/messharp/issues/115).
3. Writing to a report path whose parent directory is absent fails with exit 1 and creates neither directory nor file. Writing to an existing report directory succeeds. Filed as [#117](https://github.com/quality-gates/messharp/issues/117).

Each of the three observations above was replayed twice from the same unchanged fixture state.

## Journey 3: discovery, suppressions, and modern C# syntax

- A `Tests/ExampleTests.cs` directory produced a BooleanGetMethodName finding normally and was skipped with `--ignore-tests`.
- A `SuppressMessage` attribute suppressed the finding normally and `--strict` re-reported it.
- A valid C# primary constructor with 11 parameters produced no `ExcessiveParameterList` finding, while an equivalent ordinary constructor produced the expected violation. Both sides of the differential were replayed twice. Filed as [#116](https://github.com/quality-gates/messharp/issues/116).
- `--suffixes txt` and `--exclude` correctly omitted the selected source file.

## Unresolved observations and limitations

- `bool?`, `System.Boolean?`, and `System.Nullable<bool>` getters named `Get...` were not reported by `BooleanGetMethodName`, while a non-nullable `bool` getter was. This was not filed because it overlaps the existing boolean-type issue [#89](https://github.com/quality-gates/messharp/issues/89), and the rule documentation currently says “returning bool,” leaving nullable return semantics ambiguous.
- `--ignore-tests` also skips `Contest.cs`, because its filename ends in `Test.cs`. This is consistent with the documented suffix pattern, so it was retained as a usability observation rather than filed as a confirmed defect.
- No interactive UI, package-install, or project-compilation workflow was in scope; MessSharp is syntax-only by design.

## Evidence inventory

- Fixtures: [fixtures](fixtures/)
- Structured and renderer outputs: [reports](reports/)
- Exact issue bodies submitted via the `gh` fallback: [issue-bodies](issue-bodies/)

No fixes were applied during the exploratory run.
