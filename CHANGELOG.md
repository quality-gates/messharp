# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.2.5] - 2026-09-03

### Added

- Stable macOS Homebrew releases with self-contained Intel and Apple Silicon archives, checksums, immutable GitHub releases, and protected tap publication.
- Maintainer instructions for stable releases, tap publication, formula candidates, signing, and recovery.
- Committed git hooks mirroring CI locally: `githooks/pre-commit` (license check, `dotnet format`, warnings-as-errors build, unit tests, self-analysis — whole-tree, hard-failing) and `githooks/pre-push` (Stryker mutation testing scoped to the diff against `origin/main` via `--since`). Plain `sh` scripts, no hook-framework dependency; opt in once with `git config core.hooksPath githooks` (documented as a "Definition of Ready" step in `AGENTS.md`). Stryker is pinned in a new dotnet tool manifest (`.config/dotnet-tools.json`).

### Fixed

- Recursively resolve nested ruleset references in `RuleSet.Loader` (e.g. `<rule ref="csharp"/>` in custom rulesets) with circular reference protection (#15).
- Prevent `UnusedFormalParameter` false positives for C# `out` parameters assigned in method bodies (#16).
- Allow digits in constant names when `ConstantNamingConventions` is configured with `convention="upper"` (e.g. `HTTP_200_OK`, `SHA256`) (#17).
- Prevent 32-bit signed integer overflow in `NPathMetrics` via saturating arithmetic (#18).
- Fix `LinesOfCodeMetrics.EffectiveLinesOfCode` scanner to skip comment delimiters inside string and character literals (#19).
- Enable code rules and metrics (`CyclomaticComplexity`, `NPathComplexity`, `ExcessiveClassComplexity`, `ExitExpression`, `DevelopmentCodeFragment`, `DuplicatedArrayKey`, `IfStatementAssignment`, `ShortVariable`, `LongVariable`, `Lcom4Calculator`) to analyze expression-bodied methods and constructors via `MethodModel.EffectiveBody` (#20).
- Match namespace-qualified calls (`System.Environment.Exit`, `System.Console.WriteLine`, etc.) in `ExitExpressionRule` and `DevelopmentCodeFragmentRule` (#21).
- Prevent duplicate key false positives in `DuplicatedArrayKeyRule` when literal keys have different types (e.g. `0` vs `"0"`, `null` vs `""`) by including the literal syntax kind in the lookup key (#22).
- Wire up the CLI `--color` option to select `AnsiRenderer` when text report format is requested (#23).
- macOS release archives now retain a native ad hoc code signature, so current macOS security policy permits Homebrew-installed executables to start.

### Changed

- Release validation and immutable publication now use the SHA-pinned shared Homebrew tap actions.
- Built-in ruleset loading now uses the `BuiltInRuleSetReader` seam.
- `scripts/dotnet.sh` now persists the container's `/root/.dotnet` in a named volume (so restored local tools like Stryker survive between runs) and passes host proxy settings (`HTTPS_PROXY`, `NO_PROXY`, `SSL_CERT_FILE`) into the container when set, so restores work behind egress proxies.
- The mutation-testing workflow installs Stryker with `dotnet tool restore` (pinned via the tool manifest) instead of a floating global `dotnet tool install -g`, which the new local manifest would otherwise shadow.

- Agent skills configuration: `docs/agents/issue-tracker.md` (GitHub Issues) and `docs/agents/domain.md` (single-context domain doc layout), referenced from a new `## Agent skills` section in `AGENTS.md`.
- Vendored the 21 engineering/productivity skills from [`mattpocock/skills`](https://github.com/mattpocock/skills) (MIT) into `.claude/skills/`, with provenance and license text in `.claude/skills/THIRD_PARTY_NOTICES.md`.
- `docs/agents/triage-labels.md`, mapping the `triage` skill's five canonical roles to this repo's label vocabulary (defaults kept as-is).

## [0.2.2] - 2026-06-24

### Added

- Coverage-guided fuzzing harness for discovering crashes and edge cases. Added dedicated fuzz project (`fuzz/MessSharp.Fuzz`), corpus seeds, AFL++ runner script (`scripts/fuzz.sh`), and findings storage. Targets C# source code and ruleset XML parsing independently.

## [0.2.1] - 2026-06-24

### Fixed

- Corrected README quick-start wording so it no longer claims the default `csharp` example is the exact CI self-analysis command, and documented the stricter `csharp,codesize,design` CI ruleset combination.

### Changed

- Refactored `Runner` into an interface-driven pipeline (`IRunner`, `IFileDiscoverer`, `ISourceFileParser`), decoupling CLI orchestration and pipeline discovery from the physical file system.
- Added in-memory unit tests in `CliTests.cs` to verify option parsing, exit codes, and errors using virtual filesystem/parser fakes.

### Added

- Initial port of PHP Mess Detector to C#, mirroring messgo's architecture:
  Roslyn-based model builder, phpmd-pinned metrics (cyclomatic complexity,
  NPath, LOC), rule engine, phpmd-format ruleset XML loader with refs/
  excludes/overrides, file-discovery runner, and phpmd-compatible CLI with
  exit codes 0/1/2.
- Rule catalog across six rulesets: codesize (10), naming (7), unusedcode (4),
  cleancode (5), design (8), controversial (5) — adapted to C# semantics.
- Report renderers: text, xml, json, html, ansi, github, gitlab, checkstyle,
  sarif.
- Bundled `csharp` meta-ruleset (tuned default) and `opinionated` ruleset.
- Docker-only toolchain (`scripts/dotnet.sh`, runtime `Dockerfile`) and
  GitHub Actions CI with self-analysis.
