# Exploratory testing of discovery, suppression, and reports

- Date: 2026-11-09.
- Base: `86bd54fa4d9a225d2708e992b45690bda4a31cd4` (`origin/main`, after release 0.2.23).
- Interface: Release CLI in `mcr.microsoft.com/dotnet/sdk:8.0`, image ID `sha256:341aa6e8bbaceb1194732328ca27c15bfa9c531687db83a66af3c0c1833f5534`.
- Starting state: synthetic [fixtures](fixtures/) in a fresh worktree. No application data or existing reports were modified.

The root filesystem had 64 MB available and the first locked restore failed with `No space left on device`. A fresh worktree on `/dev/shm`, linked from `.worktrees/`, kept the caches and build outputs in the worktree. The container used local bind mounts instead of the wrapper's shared volumes, a two-CPU limit, and a 2 GB memory limit. No application code was instrumented. The second `restore --locked-mode` and Release build succeeded. All 1,131 tests passed. Self-analysis using `csharp,codesize,design` exited 0. The CLI reported `messharp 0.0.0-dev`.

## Results

One bug was confirmed and filed as [#262](https://github.com/quality-gates/messharp/issues/262). The pass also checked the explicit test-file case in [#69](https://github.com/quality-gates/messharp/issues/69). It did not attempt to resolve the suppression fix in [#226](https://github.com/quality-gates/messharp/issues/226) or exercise the active scope-resolution work in [#220](https://github.com/quality-gates/messharp/issues/220) and [PR #225](https://github.com/quality-gates/messharp/pull/225).

### Discover production files without tests

The goal was to scan both a production file and a test directory, then exclude the test file with `--ignore-tests`. The ordinary scan reported `ShortVariable` at line 5 of each file and exited 2. The [filtered scan](outputs/discovery-ignore.stdout) retained only `Widget.cs` and exited 2. Passing the test file explicitly with `--ignore-tests` produced no output and exited 0, consistent with the documented filter and the regression covered by #69. The [ordinary output](outputs/discovery.stdout) shows both findings.

The first fixture matched both the test-file and test-directory heuristics, so it did not isolate directory filtering. A review prompted a second fixture, [Helper.cs](fixtures/Scope/TeamTests/Helper.cs), whose name does not match the test-file heuristic. Scanning its parent `Scope` with `--ignore-tests` skipped `TeamTests` and exited 0. Scanning `Scope/TeamTests` or the helper file directly reported `ShortVariable` on line 5 and exited 2. The same three outcomes recurred on replay. The [parent](outputs/parent.exit), [directory](outputs/direct.stdout), [file](outputs/file.stdout), and [replay](outputs/replay-direct.stdout) outputs record the comparison.

The documented `--ignore-tests` option says it skips `*Tests/` directories. A user explicitly scanning a test project or one of its helpers can instead receive findings from that test directory and a failing exit code. Issue #262 records the discrepancy; no product fix was made in this pass.

### Suppress an intentional finding and audit it

The goal was to hide an intentional short variable without hiding an unreviewed one. The [ordinary scan](outputs/suppression.stdout) reported only `y` on line 14. The [strict scan](outputs/suppression-strict.stdout) reported `x` on line 8 as well, and both scans exited 2. A separate fixture containing only the suppressed variable exited 0 normally and exited 2 under `--strict`, with the finding visible in [strict output](outputs/only-suppressed-strict.stdout).

### Publish machine-readable reports

The goal was to produce CI annotations and a Checkstyle report matching the strict scan. The [GitHub output](outputs/github.stdout) contained warnings for lines 8 and 14. The [Checkstyle file](outputs/report.checkstyle) contained the same two lines and parsed as XML with Python's standard library. Both runs exited 2. With only a suppressed finding and no `--strict`, GitHub output was empty, the [Checkstyle file](outputs/clean.checkstyle) parsed with no errors, and both runs exited 0. The report files were reopened after the commands finished. No external Checkstyle schema or GitHub Actions runner was used.

## Limitations

The pass covered `ShortVariable`, not the full ruleset catalog. It did not cover nested suppressions, symlinks, non-ASCII paths, malformed source, or every report renderer. The first restore failure was an environment problem, not a product finding. The test-directory inconsistency was reproduced from the same fixture and is tracked in #262. No other candidate failure remained unresolved after the memory-backed retry.

## Replay

Run from the repository root after a locked restore and Release build. The paths are relative to the root mounted at `/src` by `scripts/dotnet.sh`. The last two commands write to a disposable `.explore-replay` directory rather than replacing the captured evidence.

```console
scripts/dotnet.sh restore --locked-mode
scripts/dotnet.sh build -c Release --no-restore
D=docs/exploratory-testing/2026-11-09-discovery-suppression-reports
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Widget.cs,$D/fixtures/WidgetTests" text naming --only ShortVariable
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Widget.cs,$D/fixtures/WidgetTests" text naming --only ShortVariable --ignore-tests
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/WidgetTests/WidgetTests.cs" text naming --only ShortVariable --ignore-tests
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Scope" text naming --only ShortVariable --ignore-tests
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Scope/TeamTests" text naming --only ShortVariable --ignore-tests
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Scope/TeamTests/Helper.cs" text naming --only ShortVariable --ignore-tests
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Suppress.cs" text naming --only ShortVariable
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Suppress.cs" text naming --only ShortVariable --strict
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/OnlySuppressed.cs" text naming --only ShortVariable
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/OnlySuppressed.cs" text naming --only ShortVariable --strict
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Suppress.cs" github naming --only ShortVariable --strict
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/OnlySuppressed.cs" github naming --only ShortVariable
mkdir -p .explore-replay
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/Suppress.cs" checkstyle naming --only ShortVariable --strict --reportfile .explore-replay/report.checkstyle
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll "$D/fixtures/OnlySuppressed.cs" checkstyle naming --only ShortVariable --reportfile .explore-replay/clean.checkstyle
```

The captured `.exit`, `.stdout`, and `.stderr` files record each run's status and output. Commands that find violations exit 2; the clean variations exit 0.
