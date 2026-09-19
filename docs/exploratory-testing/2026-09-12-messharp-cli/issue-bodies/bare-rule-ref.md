## Summary

The documented custom-ruleset form `<rule ref="LongVariable">...</rule>` cannot be loaded by the CLI. The README and `docs/rules.md` both present this form, so a user following the documentation gets an error before analysis starts.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `a9c5d3dc8902f3dde0449a5caa2ed723b737d5d8`
Build: Release build via `scripts/dotnet.sh build -c Release`

Fixture: `docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/team-policy.xml`

Run from the repository root:

```console
scripts/dotnet.sh run --project src/MessSharp --no-build -c Release -- docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/Clean.cs text docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/team-policy.xml
```

Actual result, reproduced twice:

```
exit 1
error: Cannot resolve ref: LongVariable
```

A control ruleset using the qualified reference `naming/LongVariable` loads successfully and exits 0 on the same clean fixture.

## Expected

The documented bare rule reference should resolve to the built-in `LongVariable` rule and allow the custom priority/property configuration to be applied.

## Impact

Users copying the documented custom ruleset example cannot run their policy. They must discover the qualified form on their own.

## Evidence

The complete exploratory run and fixture are under `docs/exploratory-testing/2026-09-12-messharp-cli/`.
