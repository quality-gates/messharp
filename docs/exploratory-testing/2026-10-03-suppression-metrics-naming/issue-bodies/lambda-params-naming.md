## Summary

`ShortVariable` and `LongVariable` never check lambda parameters. phpmd 2.15.0 reports short and long closure/arrow-function parameters. MessSharp is silent for the C# equivalents. `LongVariable` is in the default `csharp` ruleset (maximum 35), so overlong lambda parameter names escape the default run too.

## Reproduction

Commit: `5c7c8dc` (`main`), Release build.

`LambdaParams.cs`:

```csharp
using System;

public class LambdaParams
{
    public int Run(int[] items)
    {
        Func<int, int> twice = q => q * 2;
        Func<int, int> echo = thisIsAVeryLongLambdaParameterName => thisIsAVeryLongLambdaParameterName;
        return twice(1) + echo(2) + items.Length;
    }
}
```

```console
scripts/dotnet.sh src/MessSharp/bin/Release/net8.0/messharp.dll LambdaParams.cs text naming --only ShortVariable,LongVariable
```

Actual (replayed twice): no output, exit 0.

## Expected

```
LambdaParams.cs:7  ShortVariable  Avoid variables with short names like q. ...
LambdaParams.cs:8  LongVariable   Avoid excessively long variable names like thisIsAVeryLongLambdaParameterName. ...
```

Exit 2.

## Reference (phpmd 2.15.0 phar, PHP 8.3)

```php
$sum = array_filter($items, fn($j) => $j > 0);
$add = function ($k, $l) { return $k + $l; };
$f = fn($thisIsAVeryLongLambdaParameterName) => $thisIsAVeryLongLambdaParameterName;
```

```
s.php:11   ShortVariable  Avoid variables with short names like $j. ...
s.php:12   ShortVariable  Avoid variables with short names like $k. ...
s.php:12   ShortVariable  Avoid variables with short names like $l. ...
lv.php:5   LongVariable   Avoid excessively long variable names like $thisIsAVeryLongLambdaParameterName. ...
```

## Likely cause

`ShortVariableRule.CollectLocals` (and the matching `LongVariable` collector) yields method parameters plus local declarations, deconstructions, patterns, and foreach variables. It does not visit `ParameterSyntax` under `SimpleLambdaExpressionSyntax`, `ParenthesizedLambdaExpressionSyntax`, or `AnonymousMethodExpressionSyntax`. Local function parameters and LINQ query range variables (`from m in items`) are not checked either. phpmd has no direct equivalent for those, so whether to include them is a separate decision.

Evidence: `docs/exploratory-testing/2026-10-03-suppression-metrics-naming/` (`fixtures/LambdaParams.cs`, `fixtures/shortnames-reference.php`, `fixtures/longnames-reference.php`, `out/j3-replay.txt`, `out/phpmd-2.15.0-reference.txt`).
