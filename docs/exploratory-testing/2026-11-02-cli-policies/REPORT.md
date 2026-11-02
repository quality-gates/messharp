# Exploratory testing of CLI policies and reports

- Date: 2026-11-02.
- Base: `8fda3b22c7760bdb55bfa45009df876213758938` (`origin/main`).
- Interface: Release CLI through `scripts/dotnet.sh` in Docker.

## Starting state

The pass used an isolated worktree and synthetic C# files in [fixtures](fixtures/). The Docker SDK was `mcr.microsoft.com/dotnet/sdk:8.0`, digest `sha256:6ba9644da07bb5b560a402acec9ca38711a0762969f1b61da261df5c9f6f85da`. A temporary Docker command shim replaced the wrapper's shared NuGet and .NET volumes with worktree-local bind mounts and labeled each container. No application behavior was instrumented.

`restore --locked-mode` succeeded. The Release build had no warnings or errors. The Release test run passed 1,126 tests. Self-analysis with `csharp,codesize,design` exited 0. The CLI reported `messharp 0.0.0-dev`.

## Results

No new bug was confirmed. No issue was filed. The pass did not attempt to revalidate the explicitness findings already tracked in [#237](https://github.com/quality-gates/messharp/issues/237) through [#253](https://github.com/quality-gates/messharp/issues/253). Open [#185](https://github.com/quality-gates/messharp/issues/185) tracks executable-member coverage, which was outside this pass.

### Find and correct a naming violation

The goal was to find a short variable and get a clean exit after correcting it. `Example.cs` produced a `ShortVariable` finding on line 5 and exited 2. `Clean.cs` used `result` instead of `x`, produced no finding, and exited 0. The [captured text](out/finding.stdout) identifies the original file and line. Both runs were repeated with the same results.

### Tune a team policy

The goal was to retain a naming policy while excluding an intentional exception. Scanning `Example.cs` with `--only ShortVariable --disable ShortVariable` exited 0. The [team ruleset](fixtures/team.xml) excluded `ShortVariable` from `naming` and also exited 0 on the same file. The default naming scan with `--only ShortVariable` still found the violation. On [PolicyControl.cs](fixtures/PolicyControl.cs), the default naming ruleset reported both `ShortClassName` and `ShortVariable`. The team ruleset reported `ShortClassName` alone and exited 2. The [control output](out/policy-control.stdout) shows that another rule remained active.

### Publish a report and handle invalid source

The goal was to write a machine-readable finding and preserve parse diagnostics. The JSON and SARIF runs exited 2 and wrote [JSON](out/report.json) and [SARIF](out/report.sarif) reports. Both identify `ShortVariable` at line 5. SARIF declares version 2.1.0 and contains one result. Python's JSON parser reopened both files; no external SARIF schema validation was attempted.

The malformed [Broken.cs](fixtures/Broken.cs) exited 1 and wrote two diagnostics to [error.json](out/error.json). A separate replay exited 1 with the same diagnostic codes and line in [replay-error.json](out/replay-error.json). With `--ignore-errors-on-exit`, the same diagnostics remained in [ignored-error.json](out/ignored-error.json) and the exit was 0.

A violation-only scan with `--ignore-violations-on-exit` printed the finding and exited 0. A mixed scan of `Example.cs` and `Broken.cs` preserved one violation and two parse diagnostics in [mixed.json](out/mixed.json), and exited 1. The `--ignore-violations-on-exit` variation still exited 1. The `--ignore-errors-on-exit` variation exited 2. Both variations retained the finding and diagnostics in their reports. This confirms that ignoring one category does not hide the other category's exit status.

## Limitations and observations

This pass covered text, JSON, and SARIF only. It did not exercise other report formats, suppression attributes, large projects, or platform-specific paths. JSON names the file and line for `ShortVariable` but leaves the class and method fields empty. The [2026-10-03 report](../2026-10-03-suppression-metrics-naming/REPORT.md) already recorded that usability observation, so it was not filed as a new bug.

The malformed-source fixture is intentionally invalid. The expected diagnostics are parse errors, not a failure of the test setup.

## Replay

Run from the repository root after a locked restore and Release build. Each command after `--` invokes the published CLI interface. Use a disposable output directory for the last command.

```console
scripts/dotnet.sh restore --locked-mode
scripts/dotnet.sh build -c Release --no-restore
D=docs/exploratory-testing/2026-11-02-cli-policies/fixtures
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs" text naming --only ShortVariable
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Clean.cs" text naming --only ShortVariable
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs" text naming --only ShortVariable --disable ShortVariable
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs" text "$D/team.xml"
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/PolicyControl.cs" text naming
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/PolicyControl.cs" text "$D/team.xml"
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs" text naming --only ShortVariable --ignore-violations-on-exit
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs" sarif naming --only ShortVariable --reportfile /src/replay.sarif
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Broken.cs" json naming --only ShortVariable --reportfile /src/error-replay.json
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Broken.cs" json naming --only ShortVariable --ignore-errors-on-exit --reportfile /src/ignored-error-replay.json
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs,$D/Broken.cs" json naming --only ShortVariable --reportfile /src/mixed-replay.json
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs,$D/Broken.cs" json naming --only ShortVariable --ignore-violations-on-exit --reportfile /src/mixed-ignore-violations-replay.json
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/Example.cs,$D/Broken.cs" json naming --only ShortVariable --ignore-errors-on-exit --reportfile /src/mixed-ignore-errors-replay.json
```

The report commands write files in the mounted worktree. Remove the replay files after inspection. See the captured `.exit` files for the expected exit codes.
