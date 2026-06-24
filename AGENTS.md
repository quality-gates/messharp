# messharp

A PHP Mess Detector (phpmd) port for C# (**MessSharp**). Parses C# source using Roslyn (`Microsoft.CodeAnalysis.CSharp`, syntax-only) and applies rules faithful to C# semantics.

## Build & test — Docker only

All `dotnet` commands run inside Docker via the wrapper. **Never use a host dotnet install.**

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

## Running messharp on itself (Self-Analysis)

To run messharp on itself locally:

```bash
scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp --ignore-tests
```

Exit code matches phpmd: **0** clean · **1** error · **2** violations found.

## Shipping workflow

Follow these steps in order when landing a change:

1. **Build and test locally** — `scripts/dotnet.sh build` and `scripts/dotnet.sh test`.
2. **Run self-analysis** — run the CLI on `./src` using the `csharp` ruleset.
3. **Manual smoke test** — run the tool against a real file or testdata file.
4. **Update docs if needed** — update `README.md`.
5. **Update CHANGELOG.md** — add an entry under `[Unreleased]` describing what changed (Added / Fixed / Changed).
6. **Commit and push** — land changes via PR.
7. **Watch CI** — wait for Actions to go green.
8. **Merge to main** — then push.
9. **Tag and release** — tag the release and publish.

## Conventions

- Exit codes match phpmd exactly (0 success, 1 error, 2 violations).
- **Edit files one at a time using Read then Edit.** Avoid bulk string-replacement tools across multiple directories.
- Keep complexity metrics (Cyclomatic Complexity, NPath) of messharp's own functions below their configured limits.
- **SOLID Refactoring Only:** Do not 'cheat' to bypass complexity limits (such as cramming expressions or hiding logic in hacks). Refactor cleanly using **SOLID** principles. Share behavior horizontally or decompose classes using C# equivalents of PHP traits:
  - **Extension Methods** (for helper methods).
  - **Default Interface Implementations** (C#'s trait-like horizontal code sharing).
  - **Composition** (carving out separate cohesive classes).
- **Git worktrees go in `.worktrees/`** (gitignored). Create new worktrees there, e.g. `git worktree add .worktrees/my-feature`.

## Testing posture

Rules are verified using C# fixtures in the `tests/MessSharp.Tests/` directory.

**Assert on behavior:**
- Assert on which rules fire (using `mustHave` and `mustNotHave`).
- Ensure metrics values correspond to expected outputs of reference tools (like real phpmd).
