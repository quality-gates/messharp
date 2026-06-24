# MessSharp Domain Model Context

This document captures the core vocabulary and seams used during the execution of C# mess detection.

## Terminology

### Runner (`IRunner`)
The orchestration module that drives file discovery, Roslyn syntax parsing, rule analysis, and report accumulation.

### File Discoverer (`IFileDiscoverer`)
The seam that abstracts physical directory traversal and filter matches from the runner pipeline.
* **PhysicalFileDiscoverer**: Production implementation that walks the disk, applying suffix matching and exclusions.
* **FakeFileDiscoverer**: Test implementation utilizing in-memory path lists.

### Source File Parser (`ISourceFileParser`)
The seam that abstracts source code loading and parsing into intermediate AST models.
* **RoslynSourceFileParser**: Production implementation that reads code files from disk and calls the parser builder.
* **FakeSourceFileParser**: Test implementation parsing in-memory strings directly.

### Ruleset Loader (`Loader`)
The module that deserializes and processes XML definitions into ready-to-run rule collections.
