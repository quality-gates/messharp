## Summary

A custom ruleset cannot override a rule that was already loaded through a composite ruleset. When `csharp` includes `LongVariable` with its bundled threshold of 35, a later `naming/LongVariable` entry with `maximum=50` is discarded instead of overriding the earlier rule.

This breaks the documented custom-ruleset property/priority override pattern.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `a9c5d3dc8902f3dde0449a5caa2ed723b737d5d8`

Fixture: `docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/LongNamed.cs` contains a private field whose identifier is 38 characters long.

Ruleset under test: `docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/team-policy-qualified.xml`, which loads `csharp` and then adds:

```xml
<rule ref="naming/LongVariable">
  <priority>2</priority>
  <properties>
    <property name="maximum" value="50" />
  </properties>
</rule>
```

Run:

```console
scripts/dotnet.sh run --project src/MessSharp --no-build -c Release -- docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/LongNamed.cs text docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/team-policy-qualified.xml --only LongVariable
```

Actual result, reproduced twice:

```
exit 2
... LongVariable ... Keep variable name length under 35.
```

Control: the same `naming/LongVariable` entry by itself, in `team-policy-only-qualified.xml`, exits 0 with no output and therefore honors the threshold of 50.

## Expected

The later single-rule entry should replace or override the rule inherited from `csharp`, producing no violation for a 38-character name when `maximum=50`.

## Likely cause

Rules are deduplicated by name with the first instance winning after the composite ruleset has already supplied `LongVariable`.

## Evidence

The complete exploratory run and fixtures are under `docs/exploratory-testing/2026-09-12-messharp-cli/`.
