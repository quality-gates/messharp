## Summary

`UnusedPrivateMethod` and `UnusedPrivateField` evaluate each file separately. A private member that is declared in one part of a `partial` class and used only in another file is reported as unused. The same split inside a single file is handled correctly. Both rules are enabled in the default `csharp` ruleset.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `627bd26b5cf96b4e1cf6e7911dad57707c6e2383`

`Gauge.Part1.cs`:

```csharp
namespace Shop.Gauges;

public partial class Gauge
{
    private int reading;
}
```

`Gauge.Part2.cs`:

```csharp
namespace Shop.Gauges;

public partial class Gauge
{
    public int Read() => reading;

    public void Set(int value) => reading = value;
}
```

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/partial-field text csharp
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/partial text csharp
```

Actual result (each reproduced twice, exit 2):

```
partial-field/Gauge.Part1.cs:5  UnusedPrivateField   Avoid unused private fields such as 'reading'.
partial/Widget.Part2.cs:5       UnusedPrivateMethod  Avoid unused private methods such as 'Scale'.
```

(`Widget.Part2.cs` declares `private int Scale(int value)`, which `Widget.Part1.cs` calls.)

Control: `docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/partial-samefile/Dial.cs` puts two `partial class Dial` parts in one file with the same cross-part usage. It reports clean (exit 0).

## Expected

Usage of a private member should be resolved across every part of a partial type within the analysed paths. That includes parts in different files, such as a hand-written file plus a `*.Designer.cs` file or a generated part.

## Impact

Code that splits a type across files gets false positives from the default ruleset. Common examples are WinForms/WPF designer files, source-generator partials and large types organised by concern.

## Evidence

`docs/exploratory-testing/2026-09-19-modern-csharp/` (REPORT.md, fixtures `partial/`, `partial-field/`, `partial-samefile/`).
