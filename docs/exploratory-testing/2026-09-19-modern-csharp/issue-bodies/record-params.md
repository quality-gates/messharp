## Summary

The recommended `csharp` ruleset reports `CamelCaseParameterName` for every positional record parameter. Positional record parameters become public properties, and the .NET naming guidelines say to use PascalCase for them. As a result, any codebase that uses `record Point(int X, int Y)` fails the default gate.

This is a regression. A build of `7249c72^`, the commit before the primary-constructor model change for #116, reports the same fixture as clean (exit 0).

## Reproduction

Repository: `quality-gates/messharp`
Commit: `627bd26b5cf96b4e1cf6e7911dad57707c6e2383`

`docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/record/Point.cs`:

```csharp
namespace Shop.Records;

public record Point(int X, int Y);
```

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/record text csharp
```

Actual result (reproduced twice, exit 2):

```
Point.cs:3  CamelCaseParameterName  The parameter X is not named in camelCase.
Point.cs:3  CamelCaseParameterName  The parameter Y is not named in camelCase.
```

Control: the same file in the same run does not flag the camelCase parameters of an ordinary constructor. At `7249c72^`, a realistic fixture with `record OrderLine(string Sku, int Quantity, decimal UnitPrice)` produced no findings (exit 0). On current main, it produces five `CamelCaseParameterName` findings.

## Expected

Positional parameters of `record`, `record class` and `record struct` declarations should not be reported as non-camelCase when they are PascalCase. Primary-constructor parameters on plain classes and structs are ordinary parameters and should still be checked as camelCase.

## Impact

Any codebase that uses positional records gets false positives from the default `csharp` ruleset. Users must exclude `CamelCaseParameterName` entirely to avoid them.

## Evidence

`docs/exploratory-testing/2026-09-19-modern-csharp/` (REPORT.md, fixtures `record/` and `app/`).
