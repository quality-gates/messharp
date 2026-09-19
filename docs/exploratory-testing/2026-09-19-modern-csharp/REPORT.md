# MessSharp exploratory testing: modern C# and CI outputs

Date: 2026-09-19  
Repository: `quality-gates/messharp`  
Commit: `627bd26b5cf96b4e1cf6e7911dad57707c6e2383` (`main`, fast-forwarded from `origin/main` before the run)  
Mode: unattended (AFK) CLI run through the supported Docker wrapper

## Scope and starting state

The [2026-09-12 pass](../2026-09-12-messharp-cli/REPORT.md) covered CLI plumbing and ruleset configuration. This pass tests what a C# team sees when it points the recommended `csharp` ruleset at modern code, publishes the results to CI, and tunes the policy.

Before the CLI was exercised, the baseline checks passed:

- `scripts/dotnet.sh build -c Release`: 0 warnings, 0 errors.
- `scripts/dotnet.sh test -c Release --no-build`: 555 passed, 0 failed.
- Self-analysis (`./src text csharp --ignore-tests`): exit 0.

All runs used [ms.sh](ms.sh), which runs the Release-built `messharp.dll` through `scripts/dotnet.sh`. Fixtures are in [fixtures/](fixtures/), and renderer outputs are in [out/](out/). No product code was changed.

## Journey 1: analyse a modern C# service with the default ruleset

**Goal:** a typical .NET 8 service can be analysed with `csharp`. Idiomatic code produces no false positives, and findings are actionable.

Fixture: [fixtures/app/](fixtures/app/). It uses file-scoped namespaces, positional records, top-level statements, local functions, `await foreach`, `IAsyncEnumerable`, switch expressions, `Interlocked.Increment(ref field)` and `foreach` deconstruction.

- The `csharp` ruleset reported 5 `CamelCaseParameterName` findings, one for each PascalCase positional record parameter (`Sku`, `Quantity`, `UnitPrice`, `Id`, `Lines`). A build of the commit before `7249c72`, the #116 primary-constructor change, reported the same fixture as clean (exit 0). **Confirmed as a regression: [#148](https://github.com/quality-gates/messharp/issues/148).**
- All component rulesets (`codesize,naming,unusedcode,cleancode,design,controversial`) also reported `StaticAccess ... class 'Lines'` for `order.Lines.Sum(...)`. That is an instance property chain on a local variable. The minimal fixture [static/Chains.cs](fixtures/static/Chains.cs) reproduces it with a correct `System.Math.Abs` control. **Confirmed: [#150](https://github.com/quality-gates/messharp/issues/150).**
- Variation: the class was split across files as a `partial` type. A private method or field declared in one file and used only in the other was reported as unused (`UnusedPrivateMethod`, `UnusedPrivateField`). The same split within one file ([partial-samefile/](fixtures/partial-samefile/)) was clean. **Confirmed: [#149](https://github.com/quality-gates/messharp/issues/149).**
- Usage patterns reported correctly as clean: method groups (`Select(Format)`, `Func<> f = Format`), `using var`, `with` expressions, property patterns with a designation, `out _`, fields passed by `ref`, top-level statements with a static local function.
- Cyclomatic complexity for a 10-arm switch expression matched the equivalent `if` chain (10 in both cases). NPath treated the switch expression as a sum, following phpmd's `switch` semantics.

## Journey 2: publish results to CI in every format

**Goal:** each report format is valid for its consumer when paths contain spaces, `&` and non-ASCII characters.

Fixture: [fixtures/ci/My Project & Co/Café Box.cs](<fixtures/ci/My Project & Co/Café Box.cs>), with a generic `Box<T>` and a snake_case class.

- All 8 non-text formats exited 2. JSON and GitLab parsed. The XML and Checkstyle outputs were well-formed and escaped `&`. HTML escaped `&`. The 4 GitLab fingerprints were unique. The GitHub workflow commands carried the path verbatim.
- SARIF failed validation against the OASIS 2.1.0 schema with format checking: 4 × `artifactLocation.uri ... is not a 'uri-reference'`. The same source at a plain path ([out/ci-plain.sarif](out/ci-plain.sarif)) had 0 errors. Validator: [validate_sarif.py](validate_sarif.py). **Confirmed: [#151](https://github.com/quality-gates/messharp/issues/151).**
- Variation: a mix of a malformed file and a violating file with `--reportfile` exited 1. It wrote schema-valid SARIF with parse errors as `level: error` results.

## Journey 3: tune the policy and suppress intentional exceptions

**Goal:** a team can work around findings through a custom ruleset, priorities and in-code suppressions.

- A [team-policy.xml](fixtures/team-policy.xml) that excluded `CamelCaseParameterName` from `csharp`, added `UnusedFormalParameter` and overrode `LongVariable` (priority 1, maximum 10) behaved as documented. `--minimumpriority 1` kept only the priority-1 findings, and the JSON priorities were 1. This is a working workaround for #148.
- `[SuppressMessage("PHPMD", "...")]` worked on properties and methods, and so did a `@SuppressWarnings(PHPMD.X)` doc comment. `--strict` brought the suppressed findings back ([suppress/](fixtures/suppress/)).
- `CamelCasePropertyName` reported the valid camelCase property `retryCount` with the message "is not named in camelCase". The rule requires PascalCase for public or auto-properties, so the message tells the user to do what they have already done. **Confirmed: [#152](https://github.com/quality-gates/messharp/issues/152).**

## Confirmed bugs

| Issue | Rule / area | Default `csharp`? | Replays |
| :--- | :--- | :--: | :--: |
| [#148](https://github.com/quality-gates/messharp/issues/148) | CamelCaseParameterName flags positional record parameters (regression) | yes | 2 + pre-#116 control |
| [#149](https://github.com/quality-gates/messharp/issues/149) | UnusedPrivateMethod/Field ignore other files of a partial class | yes | 2 each + same-file control |
| [#150](https://github.com/quality-gates/messharp/issues/150) | StaticAccess treats `obj.Prop.Method()` as static access | no (cleancode, opinionated) | 2 + `System.Math` control |
| [#151](https://github.com/quality-gates/messharp/issues/151) | SARIF `uri` not percent-encoded | n/a | schema-validated + plain-path control |
| [#152](https://github.com/quality-gates/messharp/issues/152) | CamelCasePropertyName message names the wrong convention | yes | 1 (deterministic message text) |

The replay steps, expected and actual output and the impact of each bug are in the issue, and the exact filed text is in [issue-bodies/](issue-bodies/).

## Observations not filed

- **UnusedFormalParameter on `override` methods.** In `cleancode`/`opinionated`, an unused parameter of an `override` method is reported, although the signature is fixed by the base type. `csharp` drops this rule on purpose, and the reason given covers this case. Suggestion: skip `override` members, as phpmd skips `@inheritDoc`.
- **SARIF `$schema` URL returns 404.** `https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json` no longer resolves. The schema's own `$id` still uses that URL, so this was treated as cosmetic. The validation used `https://json.schemastore.org/sarif-2.1.0.json`.
- **SARIF parse errors are results with `ruleId: ""`.** The output is schema-valid, but some consumers group by rule. `invocations[].toolExecutionNotifications` would be a more idiomatic place for them. This is a suggestion only.
- **Local functions are not naming-checked.** `void local_helper()` is not reported by `CamelCaseMethodName`. phpmd has no equivalent construct, so the expected behaviour is not established.

## Limitations

- The pass used syntax-only fixtures, not a real multi-project solution. Roslyn semantic behaviour is out of scope by design.
- The #148 regression check built `7249c72^` in a temporary worktree (removed afterwards) with an equivalent Docker invocation, because the wrapper mounts the main checkout.
- SARIF validation used a throwaway Python venv (`jsonschema`, `rfc3987`) in `/tmp`, which was deleted after the run. GitHub code scanning ingestion was not exercised.
