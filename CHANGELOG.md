# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
