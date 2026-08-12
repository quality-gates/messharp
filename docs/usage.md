# Usage

Command shape:

```console
messharp <paths> <format> <ruleset[,ruleset...]> [options]
```

- **paths** — comma-separated files or directories. Directories are walked;
  `bin/`, `obj/`, `node_modules/`, and `.git/` are skipped.
- **format** — `text`, `xml`, `json`, `html`, `ansi`, `github`, `gitlab`,
  `checkstyle`, or `sarif`.
- **ruleset** — one or more built-in names or paths to phpmd-format ruleset XML.

`text` format prints one finding per line as `file:line  Rule  message`.

## Examples

```console
messharp ./src text codesize
messharp ./src,./tests json naming,unusedcode
messharp Program.cs xml codesize,design,cleancode --minimumpriority 2
messharp ./src text codesize,design --only CyclomaticComplexity,GlobalVariable
messharp ./src text csharp --disable LongVariable
messharp ./src sarif csharp --ignore-tests --reportfile reports/messharp.sarif
```

`--only` (alias `--enable`) and `--disable` filter by **rule name** within the
loaded ruleset(s). `--only` cannot pull in a rule the ruleset does not include.

## Options

| Option | Effect |
| :--- | :--- |
| `--minimumpriority <n>` | Only run rules with priority ≤ n. |
| `--maximumpriority <n>` | Only run rules with priority ≥ n. |
| `--reportfile <file>` | Write the report to a file instead of stdout. |
| `--suffixes <list>` | File extensions to scan (default: `cs`). |
| `--exclude <list>` | Path substrings to exclude. |
| `--enable`, `--only <list>` | Run only these rules (comma-separated names). |
| `--disable <list>` | Skip these rules (comma-separated names). |
| `--ignore-tests` | Skip `*Test.cs` / `*Tests.cs` files and `*Tests/` / `*.Tests/` directories. |
| `--strict` | Also report suppressed violations when suppressions exist. |
| `--color` | Colorize text output. |
| `--verbose`, `-v` | Verbose diagnostics. |
| `--ignore-errors-on-exit` | Exit `0` even if parse errors occurred. |
| `--ignore-violations-on-exit` | Exit `0` even if violations were found. |
| `--version` | Print version. |
| `--help`, `-h` | Show help. |

## Exit codes

| Code | Meaning |
| :--: | :--- |
| **0** | Clean — no violations |
| **1** | Error (bad arguments, parse failure, …) |
| **2** | Violations found |

## Install variants

Repo tooling runs through Docker so a host .NET SDK is optional:

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp --ignore-tests
```

Runtime image:

```console
docker build -t messharp .
docker run --rm -v "$PWD":/code messharp /code text csharp --ignore-tests
```

With a local SDK, build and run the project under `src/MessSharp` the usual
`dotnet` way.

## Reports

Formats: `text`, `xml`, `json`, `html`, `ansi`, `github`, `gitlab`,
`checkstyle`, `sarif`. Use `--reportfile` to write the full report to disk.
