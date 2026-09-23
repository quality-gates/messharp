**Title:** Expression-bodied destructors are invisible to all body-based rules

Follow-up to #167, which made destructors visible to rules. `MethodModel.EffectiveBody` (`src/MessSharp/Model/Models.cs`) maps expression bodies for methods, constructors, operators, conversion operators, local functions and accessors, but has no `DestructorDeclarationSyntax` case. An arrow-bodied destructor therefore has no body for any rule that uses `EffectiveBody` (DevelopmentCodeFragment, CyclomaticComplexity, NPath, StaticAccess, UnusedCode body analysis, and others).

## Replay

`Dtor.cs`:

```csharp
class Dtor
{
    ~Dtor() => System.Console.WriteLine("arrow");
}
class DtorBlock
{
    ~DtorBlock() { System.Console.WriteLine("block"); }
}
```

```console
scripts/dotnet.sh run --project src/MessSharp -- Dtor.cs text design
```

## Expected

A `DevelopmentCodeFragment` finding for both destructors (lines 3 and 7).

## Actual

```
Dtor.cs:7  DevelopmentCodeFragment  The method ~DtorBlock() calls the typical debug function System.Console.WriteLine() which is mostly only used during development.
```

Line 3 (the arrow-bodied destructor) is not reported. Replayed twice on 24f4a02 plus the unreleased explicitness branch; `Models.cs` is unchanged from `origin/main`.

## Suggested fix

Add `DestructorDeclarationSyntax d => d.ExpressionBody?.Expression` to `EffectiveBody`, with a regression test beside the #167 tests.
