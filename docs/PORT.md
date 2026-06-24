# MessSharp porting spec

**messharp** is a port of [PHP Mess Detector](https://phpmd.org) (phpmd) to C#:
written in C# *and* analyzing C# source, applying phpmd's rule catalog,
ruleset XML format, message templates, CLI surface, exit codes, and report
renderers. The reference implementation for architecture and behavior is
**messgo** (https://github.com/quality-gates/messgo), a sibling port for Go —
a checkout lives at `/tmp/messgo` during development. Where messgo wraps
`go/ast`, messharp wraps **Roslyn** (`Microsoft.CodeAnalysis.CSharp`,
syntax-only — no semantic model, no compilation; we analyze files
standalone exactly like phpmd/messgo do).

## Hard constraints

- **All `dotnet` commands run in Docker** via `scripts/dotnet.sh` (never a
  host dotnet). Example: `scripts/dotnet.sh test`.
- Exit codes match phpmd exactly: **0** clean, **1** error, **2** violations.
- CLI surface is identical to messgo's:
  `messharp <paths> <format> <ruleset[,...]> [options]` with the same options
  (`--minimumpriority`, `--maximumpriority`, `--reportfile`, `--suffixes`
  (default `cs`), `--exclude`, `--only`/`--enable`, `--disable`,
  `--ignore-tests`, `--strict`, `--color`, `--verbose`,
  `--ignore-errors-on-exit`, `--ignore-violations-on-exit`, `--version`,
  `--help`). Directory walking skips `bin/`, `obj/`, `.git/`,
  `node_modules/`. `--ignore-tests` skips files ending in `Test.cs`/
  `Tests.cs` and files under a `*.Tests/` or `*Tests/` directory.
- phpmd message templates are reproduced verbatim (with `{0}`-style
  placeholders), adapted only where C# semantics demand different wording.
- Violations sort by file, then begin line.

## Solution layout

```
MessSharp.sln
src/MessSharp/MessSharp.csproj    net8.0 console app; PackageReference Microsoft.CodeAnalysis.CSharp
  Program.cs                      thin Main -> Cli.Run(args), returns exit code
  Cli/Cli.cs                      arg parsing/validation/orchestration (mirror internal/cli)
  Model/                          phpmd-style artifacts built from Roslyn syntax
  Metrics/Metrics.cs              cyclomatic complexity, NPath, LOC
  Rule/                           IRule, Violation, RuleContext, Properties, Engine, message rendering
  Rules/CleanCode|CodeSize|Controversial|Design|Naming|UnusedCode/
  Rules/Registry.cs               name -> rule list registry (see below)
  RuleSet/                        phpmd ruleset XML loader, refs, excludes, overrides
  Report/                         Report + renderers: text, xml, json, html, ansi, github, gitlab, checkstyle, sarif
  Runner/Runner.cs                file discovery + pipeline
  Util/
tests/MessSharp.Tests/MessSharp.Tests.csproj   xunit
rulesets/*.xml                    bundled rulesets, copied to output (Content/CopyToOutputDirectory)
testdata/                         crafted .cs fixture files per ruleset
docs/PORT.md                      this file
```

Namespace = folder, e.g. `MessSharp.Rules.CodeSize`.

## Core contracts (mirror messgo's `internal/rule` and `internal/model`)

```csharp
public interface IRule {
    string Name { get; }          // e.g. "CyclomaticComplexity"
    string Message { get; }       // phpmd template with {0} placeholders
    int Priority { get; }
    string SetName { get; }       // e.g. "codesize"
    string ExternalUrl { get; }
    string Description { get; }
    string Since { get; }
}
// Artifact-aware interfaces, like messgo's ClassRule/MethodRule/etc.:
public interface IClassRule : IRule     { void Apply(RuleContext ctx, ClassModel cls); }
public interface IInterfaceRule : IRule { void Apply(RuleContext ctx, InterfaceModel iface); }
public interface IMethodRule : IRule    { void Apply(RuleContext ctx, MethodModel method); }
public interface IFunctionRule : IRule  { void Apply(RuleContext ctx, MethodModel function); }
public interface IFileRule : IRule      { void Apply(RuleContext ctx); }
```

`RuleContext` carries the `SourceFile`, the rule's effective `Properties`
(from ruleset XML, with `Int/Float/Bool/Str` accessors and defaults), and
`Report(...)` helpers that render the message template and append a
`Violation` (fields: Rule, File, BeginLine, EndLine, Description, Class,
Method, Function, Package(=namespace), Priority, RuleSetName) — port
messgo's `rule.go` faithfully, including `RenderMessage` ({N} substitution,
integral floats printed without decimal point) and `CompileRegex` (phpmd
`"(pattern)i"` property encoding → .NET Regex).

### Model (Roslyn → phpmd artifacts)

`SourceFile` holds Path, SyntaxTree/root, source text, Namespace, and lists
of `ClassModel`, `InterfaceModel`, methods. Mapping:

| phpmd/messgo | C# (Roslyn syntax node) |
| :--- | :--- |
| Class | `class`, `struct`, `record` declarations (NodeType "class") |
| Interface | `interface` declarations |
| Method | methods, constructors, local functions are *not* methods (treat as part of body); property accessors are not methods |
| Field (property in PHP) | field declarations *and* auto-properties |
| Parameter | method/ctor parameters |
| Function | C# has no free functions: top-level statements form an implicit Main; `IFunctionRule` exists for parity but rarely fires |

Brief descriptions:
Models keep a reference to their Roslyn node so rules can walk syntax.
`ClassModel` records base types (`Embeds` analog), constants
(`const` members), Exported = public, Line/EndLine from the tree's
`GetLineSpan` (1-based).

### Metrics

Port `internal/metrics/metrics.go` semantics to Roslyn: cyclomatic
complexity (+1 per `if`, `case` label (not default), `for`, `foreach`,
`while`, `do`, `catch`, `&&`, `||`, `??`, ternary `?:`), NPath complexity
per phpmd's algorithm, LOC with optional whitespace/comment exclusion.
Keep the test pinned to real phpmd 2.15.0 numbers: port messgo's reference
function (`metrics_test.go`) to an equivalent C# fixture — cyclomatic 12,
NPath 324.

### Engine, ruleset loader, runner, renderers

Port `internal/rule/engine.go`, `internal/ruleset/ruleset.go`,
`internal/runner/runner.go`, `internal/report/*.go` from messgo as directly
as possible: same dispatch (per-class, per-method, per-file), same ruleset
XML semantics (`<rule ref>` whole-set and single-rule forms,
`<exclude name>`, property/priority overrides, `--minimumpriority`/
`--maximumpriority` filters), same renderer output shapes (the XML/JSON/
HTML/sarif/checkstyle layouts match phpmd's).

### Rule registry (conflict-free parallel work)

`Rules/Registry.cs` maps ruleset name → `IReadOnlyList<IRule>` by delegating
to one static class per group, e.g. `CodeSizeRules.All`. Each group's static
class lives in that group's folder; agents implementing a group edit **only
their own folder** and the matching `rulesets/<name>.xml` and their own test
file `tests/MessSharp.Tests/<Group>RulesTests.cs`.

## Rule catalog (per group)

Same catalog as messgo, adapted to C# semantics:

- **codesize**: CyclomaticComplexity, NPathComplexity, ExcessiveMethodLength,
  ExcessiveClassLength, ExcessiveParameterList, ExcessivePublicCount,
  TooManyFields, TooManyMethods, TooManyPublicMethods,
  ExcessiveClassComplexity. Direct ports; same thresholds/properties.
- **naming**: ShortClassName, LongClassName, ShortVariable (skip `for`-loop
  counters like phpmd), LongVariable, ShortMethodName,
  ConstantNamingConventions (**adapted**: C# constants are PascalCase, so
  default checks PascalCase; property `convention` = `pascal`(default)|`upper`),
  BooleanGetMethodName (GetX returning `bool` → IsX/HasX),
  ConstructorWithNameAsEnclosingClass (**omitted** — a C# compile error;
  document the omission).
- **unusedcode**: UnusedPrivateField, UnusedLocalVariable,
  UnusedPrivateMethod, UnusedFormalParameter. Single-file, syntax-based
  like messgo (name-reference matching).
- **cleancode**: BooleanArgumentFlag, ElseExpression, IfStatementAssignment,
  DuplicatedArrayKey (→ duplicate keys in dictionary/collection
  initializers), StaticAccess (phpmd rule, meaningful in C#: static method
  calls on other classes, with `exceptions` property; `Math`, etc. are
  natural defaults to discuss in the xml).
- **design**: ExitExpression (→ `Environment.Exit`/`Environment.FailFast`),
  GotoStatement (C# has `goto`), CountInLoopExpression (→ `.Count()`/
  `.Count`/`.Length` in loop conditions), DevelopmentCodeFragment
  (→ `Console.Write*`, `Debug.WriteLine`, `Debugger.Break/Launch`),
  EmptyCatchBlock, CouplingBetweenObjects (distinct type names referenced),
  GlobalVariable (**adapted**: mutable `public static` fields / mutable
  static state; mirror messgo's mutation-aware behavior within the file,
  `report-immutable` property; `static readonly` + `const` stay silent),
  LackOfCohesionOfMethods (LCOM4 exactly per messgo's README: shared
  fields/receiver calls link methods, stateless methods and trivial
  getters/setters ignored, getter call counts as field use).
- **controversial**: CamelCaseClassName, CamelCaseMethodName,
  CamelCasePropertyName, CamelCaseParameterName, CamelCaseVariableName —
  **adapted to C# conventions**: PascalCase for classes/methods/properties;
  camelCase for parameters/locals; private fields may be `_camelCase`
  (property `allowUnderscorePrefix`, default true).

### Meta-rulesets

- `rulesets/csharp.xml` — recommended default; pulls in all six and tunes
  rules that misfire on idiomatic C# (decide the final exclusion list by
  running self-analysis, as messgo did for `go`). Expected exclusions:
  CleanCode/ElseExpression, CleanCode/BooleanArgumentFlag,
  CleanCode/StaticAccess, UnusedCode/UnusedFormalParameter (interface
  implementations), plus a raised LongVariable maximum.
- `rulesets/opinionated.xml` — the rules `csharp` drops, for stricter runs.

## Testing posture (mirror messgo)

- Behavioral tests per ruleset using crafted C# fixture sources, asserting
  which rules fire (`MustHave`/`MustNotHave` helpers) — port the structure
  of `internal/rules/rules_test.go`.
- Metric tests pinned to real phpmd 2.15.0 numbers.
- CLI tests: argument validation, exit codes, format/ruleset selection.
- Everything green via `scripts/dotnet.sh test`.

## Self-analysis

The shipped binary must run clean on its own source:
`messharp ./src text csharp --ignore-tests` → exit 0 (tune `csharp.xml`
and/or refactor messharp itself until true, the same way messgo did).
