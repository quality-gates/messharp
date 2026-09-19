## Summary

`ExcessiveParameterList` does not analyze parameters declared on a C# primary constructor. The equivalent ordinary constructor is reported correctly, so this is a syntax/model coverage gap rather than a threshold or CLI issue.

## Reproduction

Repository: `quality-gates/messharp`
Commit: `a9c5d3dc8902f3dde0449a5caa2ed723b737d5d8`

Fixture: `docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/PrimaryConstructor.cs` is a valid C# primary-constructor declaration with 11 parameters.

```console
scripts/dotnet.sh run --project src/MessSharp --no-build -c Release -- docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/PrimaryConstructor.cs text codesize --only ExcessiveParameterList
```

Actual result, reproduced twice:

```
exit 0
(no output)
```

Differential control: `OrdinaryConstructor.cs` has the same 11 parameters in a normal constructor:

```console
scripts/dotnet.sh run --project src/MessSharp --no-build -c Release -- docs/exploratory-testing/2026-09-12-messharp-cli/fixtures/OrdinaryConstructor.cs text codesize --only ExcessiveParameterList
```

The control exits 2 and reports:

```
The constructor OrdinaryConstructor has 11 parameters. Consider reducing the number of parameters to less than 10.
```

## Expected

Primary-constructor parameters should count toward `ExcessiveParameterList`, because a primary constructor is the type's constructor signature and the rule applies to constructors.

## Likely cause

The syntax-to-model layer builds constructors only from `ConstructorDeclarationSyntax` members and does not create a method/constructor model for `TypeDeclarationSyntax.ParameterList`.

## Evidence

The complete exploratory run and fixtures are under `docs/exploratory-testing/2026-09-12-messharp-cli/`.
