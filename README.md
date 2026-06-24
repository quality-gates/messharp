# messharp

**MessSharp** (**messharp**) is a [PHP Mess Detector](https://phpmd.org) (phpmd) port for C#: it
is written in C# *and* analyzes C# source code, applying phpmd's rule catalog,
ruleset format, message templates, CLI surface, and report renderers — adapted
faithfully to C# semantics. It is the sibling of
[messgo](https://github.com/quality-gates/messgo), the same port for Go.

Where phpmd parses PHP via pdepend, messharp parses C# via
[Roslyn](https://github.com/dotnet/roslyn) (`Microsoft.CodeAnalysis.CSharp`,
syntax-only — files are analyzed standalone, no compilation required). By
default it uses idiomatic C# principles (the bundled `csharp` ruleset), but a
fuller set of checks that more closely emulates standard phpmd rules can be
optionally enabled.

## Getting started

All C# tooling in this repo runs through Docker — no host .NET install needed.

### 1. Build it

```bash
scripts/dotnet.sh build -c Release
```

Or build the runtime image:

```bash
docker build -t messharp .
```

### 2. Run it on your code

The simplest way to start is to point messharp at a directory using the bundled
`csharp` ruleset, with plain `text` output, skipping test files:

```bash
scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp --ignore-tests
```

Or with the runtime image, mounting the code to analyze:

```bash
docker run --rm -v "$PWD":/code messharp /code text csharp --ignore-tests
```

That's the whole pattern. The command is always:

```bash
messharp <paths> <format> <ruleset[,...]> [options]
```

* **paths** — comma-separated files or directories. Directories are walked;
  `bin/`, `obj/`, `node_modules/`, and `.git/` are skipped.
* **format** — `text`, `xml`, `json`, `html`, `ansi`, `github`, `gitlab`,
  `checkstyle`, or `sarif`.
* **ruleset** — one or more rulesets (see [Rulesets](#rulesets)). Start with
  `csharp`.

### 3. Read the output

`text` format prints one violation per line as `file:line  Rule  message`:

```
src/MessSharp/Cli/Cli.cs:131  CyclomaticComplexity  The method Parse() has a Cyclomatic Complexity of 12. The configured cyclomatic complexity threshold is 10.
```

### 4. Check the exit code

Exit codes match phpmd exactly:

| Code | Meaning |
| :--: | :--- |
| **0** | Clean — no violations |
| **1** | Error (e.g. bad arguments, parse failure) |
| **2** | Violations found |

This makes messharp drop straight into a build script or CI step: a non-zero
exit fails the job.

## More usage examples

```bash
messharp ./src text codesize                                    # one ruleset
messharp ./src,./tests json naming,unusedcode                   # multiple paths and rulesets
messharp Program.cs xml codesize,design,cleancode --minimumpriority 2
messharp ./src text codesize,design --only CyclomaticComplexity,GlobalVariable
messharp ./src text csharp --disable LongVariable               # everything in csharp except one rule
```

`--only` (alias `--enable`) and `--disable` filter by **rule name** within the
ruleset(s) you load. Rule names are the ones shown in output (e.g.
`CyclomaticComplexity`, `ElseExpression`). Note `--only` selects from rules
already in the chosen ruleset — it won't pull in a rule the ruleset doesn't
include.

### Options

| Option | Effect |
| :--- | :--- |
| `--minimumpriority <n>` | Only run rules with priority ≤ n. |
| `--maximumpriority <n>` | Only run rules with priority ≥ n. |
| `--reportfile <file>` | Write the report to a file instead of stdout. |
| `--suffixes <list>` | File extensions to scan (default: `cs`). |
| `--exclude <list>` | Path substrings to exclude. |
| `--enable`, `--only <list>` | Run **only** these rules (comma-separated rule names). |
| `--disable <list>` | Skip these rules (comma-separated rule names). |
| `--ignore-tests` | Skip `*Test.cs`/`*Tests.cs` files and `*Tests/`/`*.Tests/` directories. |
| `--strict` | Also report suppressed violations. |
| `--color` | Colorize text output. |
| `--verbose`, `-v` | Verbose diagnostics. |
| `--ignore-errors-on-exit` | Exit `0` even if parse errors occurred. |
| `--ignore-violations-on-exit` | Exit `0` even if violations were found. |
| `--version` | Print version. |
| `--help`, `-h` | Show help. |

## Rulesets

Pass rulesets by name (comma-separated), or pass a path to your own
phpmd-format ruleset XML file.

| Ruleset | What it checks |
| :--- | :--- |
| **`csharp`** | **Recommended default.** Pulls in all rulesets below, but tunes the rules whose PHP defaults misfire on idiomatic C#. |
| `codesize` | CyclomaticComplexity, NPathComplexity, ExcessiveMethodLength, ExcessiveClassLength, ExcessiveParameterList, ExcessivePublicCount, TooManyFields, TooManyMethods, TooManyPublicMethods, ExcessiveClassComplexity |
| `naming` | ShortClassName, LongClassName, ShortVariable, LongVariable, ShortMethodName, ConstantNamingConventions, BooleanGetMethodName |
| `unusedcode` | UnusedPrivateField, UnusedLocalVariable, UnusedPrivateMethod, UnusedFormalParameter |
| `cleancode` | BooleanArgumentFlag, ElseExpression, StaticAccess, IfStatementAssignment, DuplicatedArrayKey |
| `design` | ExitExpression, GotoStatement, CountInLoopExpression, DevelopmentCodeFragment, EmptyCatchBlock, CouplingBetweenObjects, GlobalVariable, LackOfCohesionOfMethods |
| `controversial` | CamelCaseClassName, CamelCaseMethodName, CamelCasePropertyName, CamelCaseParameterName, CamelCaseVariableName — adapted to C# conventions (PascalCase types/members, camelCase locals/params) |
| `opinionated` | **Opt-in.** The rules the `csharp` ruleset deliberately drops because they fight common C# practice. |

Rules with a direct C# analog reproduce phpmd's behavior and message templates
exactly; rules that are intrinsically PHP-specific are either adapted to the
nearest C# idiom or omitted (the C# compiler already enforces several of
them — e.g. `ConstructorWithNameAsEnclosingClass` is a compile error).

Notable adaptations:

* `ConstantNamingConventions` checks **PascalCase** (the C# convention) rather
  than UPPERCASE; set its `convention` property to `upper` for phpmd behavior.
* `GlobalVariable` is **mutation-aware**: it reports only mutable static
  fields that are actually mutated. `static readonly` and `const` members stay
  silent. Set `report-immutable=true` to also surface un-mutated mutable
  statics.
* `LackOfCohesionOfMethods` computes the **LCOM4** cohesion metric per class:
  methods are linked when they use a common field or call one another; the
  metric is the number of disconnected method groups. Stateless helpers and
  trivial getters/setters are ignored so plain data carriers stay quiet.

### Custom rulesets

Ruleset XML supports phpmd's `<rule ref="...">` form, `<exclude name="..."/>`
children, and single-rule property/priority overrides — compose your own tuned
ruleset the same way phpmd does, then pass its path as the ruleset argument.

## Use it in CI (GitHub Actions)

messharp runs on itself in CI; see `.github/workflows/ci.yml` in this repo for
the exact job. The CI self-analysis currently runs the stricter
`csharp,codesize,design` ruleset combination. Because messharp exits `2` when it
finds violations, the self-analysis step fails the job automatically — no extra
scripting needed.

## Running the tests

```bash
scripts/dotnet.sh test
```

The suite includes metric tests pinned to numbers produced by **real phpmd
2.15.0** (cyclomatic complexity 12, NPath 324 on a reference method), plus
per-ruleset behavioral tests, renderer tests, and CLI/exit-code tests.

## Contributing & SOLID Refactoring

For setup, workflows, and detailed guidelines, please see [CONTRIBUTING.md](CONTRIBUTING.md).

All modifications and refactorings must follow **SOLID** design principles and avoid 'cheats' to bypass complexity gates. Use C# equivalents of PHP traits to horizontally decompose code and reduce class CCN:
- **Extension Methods** for shared helper logic.
- **Default Interface Implementations** for horizontal code sharing.
- **Composition** for extracting focused helper classes.
