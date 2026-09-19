## Summary

`CamelCasePropertyName` requires public fields and auto-properties to be **PascalCase**, but its message still says "is not named in camelCase". For a property that is already camelCase, the finding tells the user to do what they have already done.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `627bd26b5cf96b4e1cf6e7911dad57707c6e2383`

`docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/propcase/Props.cs`:

```csharp
public class Props
{
    public int retryCount { get; set; }
    public int RetryLimit { get; set; }
}
```

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/propcase text controversial
```

Actual result (exit 2):

```
Props.cs:5  CamelCasePropertyName  The property retryCount is not named in camelCase.
```

`rulesets/controversial.xml` defines `message="The property {0} is not named in camelCase."`. The rule's own doc comment in `CamelCaseRules.cs` says "phpmd rule name kept; message adapted accordingly", but the message was not adapted. By contrast, `CamelCaseMethodName` says "is not named in PascalCase."

## Expected

The message should state the convention that was violated. That is PascalCase for public or auto-properties and camelCase for private fields. Alternatively, use a neutral message that names both conventions.

## Impact

The finding points users towards the wrong fix. This rule is enabled in the default `csharp` ruleset.

## Evidence

`docs/exploratory-testing/2026-09-19-modern-csharp/` (REPORT.md, fixture `propcase/`).
