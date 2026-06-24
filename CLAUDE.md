# messharp

A PHP Mess Detector (phpmd) port for C# (**MessSharp**). Parses C# source
using Roslyn (`Microsoft.CodeAnalysis.CSharp`, syntax-only) and applies rules
faithful to C# semantics. Reference implementation: messgo
(https://github.com/quality-gates/messgo). Porting spec: `docs/PORT.md`.

## Build & test — Docker only

All `dotnet` commands run inside Docker via the wrapper. **Never use a host
dotnet install.**

```bash
scripts/dotnet.sh build
scripts/dotnet.sh test
scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp --ignore-tests
```

## Key directories

| Path | What it does |
| :--- | :--- |
| `src/MessSharp/Cli/` | CLI parsing, validation, orchestration |
| `src/MessSharp/Model/` | Roslyn syntax → phpmd-style artifacts (Class, Interface, Method, Field, Parameter) |
| `src/MessSharp/Metrics/` | Cyclomatic complexity, NPath, LOC |
| `src/MessSharp/Rule/` | Rule interfaces, violations, context, engine |
| `src/MessSharp/Rules/` | Rule implementations by ruleset (cleancode, codesize, controversial, design, naming, unusedcode) |
| `src/MessSharp/RuleSet/` | phpmd ruleset XML loader, filters, overrides |
| `src/MessSharp/Report/` | Renderers (text, xml, json, html, ansi, github, gitlab, checkstyle, sarif) |
| `src/MessSharp/Runner/` | File discovery and pipeline |
| `rulesets/` | Bundled ruleset XML files |
| `testdata/` | C# fixture sources for rule tests |

## Conventions

- Exit codes match phpmd exactly: 0 success, 1 error, 2 violations.
- CLI surface: `messharp <paths> <format> <ruleset[,...]> [options]`.
- Edit files one at a time using Read then Edit; no bulk replacements.
- Keep messharp's own functions under its configured complexity limits —
  self-analysis (`scripts/dotnet.sh run --project src/MessSharp -- ./src text
  csharp --ignore-tests`) must exit 0.
- Git worktrees go in `.worktrees/` (gitignored).

## Testing posture

- Assert on behavior: which rules fire on crafted fixtures (MustHave /
  MustNotHave), not implementation details.
- Metric values are pinned to real phpmd 2.15.0 outputs (cyclomatic 12,
  NPath 324 on the reference fixture).
