# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
