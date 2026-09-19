## Summary

The documented `--reportfile` workflow fails when the report's parent directory does not already exist. The usage documentation gives `reports/messharp.sarif` as an example, but a fresh checkout has no `reports/` directory and the CLI does not create it.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `a9c5d3dc8902f3dde0449a5caa2ed723b737d5d8`

Starting from the repository root, with the parent path absent:

```console
scripts/dotnet.sh run --project src/MessSharp --no-build -c Release -- docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/Clean.cs json csharp --reportfile docs/exploratory-testing/2026-09-12-messharp-cli/reports/missing-parent/scan.json
```

Actual result, reproduced twice:

```
exit 1
error: Could not find a part of the path '/src/docs/exploratory-testing/2026-09-12-messharp-cli/reports/missing-parent/scan.json'.
```

No report file or parent directory is created.

Control: the same command targeting the existing `reports/` directory succeeds with exit 0 and writes valid JSON.

## Expected

The CLI should create missing parent directories before writing the requested report, or the documentation should clearly require users to create them.

## Impact

A standard CI invocation fails on a clean checkout when its report artifact is placed under a new output directory.

## Evidence

The complete exploratory run and fixtures are under `docs/exploratory-testing/2026-09-12-messharp-cli/`.
