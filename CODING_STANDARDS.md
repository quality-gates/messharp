# Coding standards

## Tests

- Strongly prefer integration tests and end-to-end tests over unit tests.
- Strongly prefer exercising real system behaviour over "the tests pass so it must work."
- Only mock third-party services we cannot control. Do not mock code we own.
- For this codebase, the default proof is: run the real CLI/analyzer on real (or fixture) source and assert findings, exit codes, and report output.

## Comments and docs

- Code comments use ASD-STE100 Simplified Technical English.
- Ground terms in `CONTEXT.md` domain language when that file exists. Do not invent synonyms for glossary terms.
- Do not write comments that only repeat what the code already makes clear.
- Do not put brittle references in README or comments (versions, line numbers, temporary paths, "as of today" claims) when those details are allowed to change.

## Common footguns

- Tautological tests (asserting the mock was called the way the test just configured it).
- Mocks of modules/services we own.
- "Green suite" treated as proof the product works for a user.
- Narrating comments and README drift magnets.
- Cheating complexity or quality gates with denser syntax, hidden branching, or indirection that does not reduce real complexity.

## C#

- Toolchain: all `dotnet` invokes go through `scripts/dotnet.sh` (Docker). Do not rely on a host SDK.
- Target the project TFM (`net8.0`) with nullable reference types enabled. Do not add null-suppression (`!`) to paper over model holes.
- Keep the phpmd-faithful shape: Roslyn **syntax-only** analysis, ruleset XML, exit codes `0` clean / `1` error / `2` violations.
- Honor domain seams from `CONTEXT.md`: runner, file discoverer, source file parser, ruleset loader, rule engine, reporters. New behaviour belongs behind those seams, not as ad-hoc static helpers scattered through `Cli/`.
- Prefer composition: extract a cohesive collaborator type when a class or method exceeds complexity limits.
- Do not cheat Cyclomatic Complexity or NPath limits (compressed expressions, nested ternaries, "helper" dumps).
- Extension methods: only for genuine shared stateless operations on a clear host type — not as a trash drawer for complexity escape.
- Default interface methods: only for real interface behaviour shared by implementers — not trait-like dumping grounds.
- Parse C# with Roslyn (`Microsoft.CodeAnalysis.CSharp`) via existing parser/model types. Do not add a second parser.
- Tests (xUnit) assert behaviour with `mustHave` / `mustNotHave` style fixtures and, where relevant, metric values against reference expectations. Prefer end-to-end and ruleset fixture tests over isolated pure-unit puzzles.
- Keep messharp’s own analyzed surface under its configured complexity limits when you touch `src/`.
- Match existing naming: `PascalCase` types/methods, interfaces prefixed with `I` at real seams.
