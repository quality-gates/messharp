# messharp

Catch maintainability problems in C# before they calcify: oversized methods and
types, tangled dependencies, dead private code, muddy naming, and other mess
that reviews keep rediscovering.

`messharp` is a local CLI. It parses C# with Roslyn syntax-only analysis, never
builds or runs your project, and needs no project dependencies installed.
Target framework `net8.0`.

## Quick start

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp --ignore-tests
```

That scans `src` with the recommended low-noise policy and prints findings on
stdout. Exit `0` is clean, `2` means findings, `1` means the tool or a source
file failed.

Common next steps:

```console
scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp,opinionated --ignore-tests
scripts/dotnet.sh run --project src/MessSharp -- ./src sarif csharp --ignore-tests --reportfile reports/messharp.sarif
scripts/dotnet.sh run --project src/MessSharp -- ./src github csharp --ignore-tests
```

Full command syntax, options, and discovery: [docs/usage.md](docs/usage.md).
What each ruleset and rule checks: [docs/rules.md](docs/rules.md).

## Install

Repo tooling runs through Docker wrappers so a host .NET SDK is optional:

```console
scripts/dotnet.sh build -c Release
scripts/dotnet.sh run --project src/MessSharp -- --version
```

Runtime image:

```console
docker build -t messharp .
docker run --rm -v "$PWD":/code messharp /code text csharp --ignore-tests
```

## Tune the gate

Start with `csharp`. Add `opinionated` when you want the stricter checks the
recommended set leaves out. Point at a custom XML ruleset when thresholds or
membership need to live in the repo:

```xml
<ruleset name="team policy">
  <rule ref="csharp">
    <exclude name="DevelopmentCodeFragment" />
  </rule>
  <rule ref="LongVariable">
    <priority>2</priority>
    <properties>
      <property name="maximum" value="50" />
    </properties>
  </rule>
</ruleset>
```

```console
scripts/dotnet.sh run --project src/MessSharp -- ./src text path/to/team-policy.xml --ignore-tests
```

## Suppress one intentional exception

Drop a rule for the whole run with `--disable`, skip paths with `--exclude`, or
encode the exception in a team ruleset:

```xml
<rule ref="csharp">
  <exclude name="RuleName" />
</rule>
```

`--strict` keeps suppressed findings visible in the report when suppressions
are present.

## Drop it into CI

```yaml
# GitHub Actions
- run: scripts/dotnet.sh build -c Release
- run: scripts/dotnet.sh run --project src/MessSharp --no-build -c Release -- ./src github csharp --ignore-tests
```

```yaml
# GitLab Code Quality
script: scripts/dotnet.sh run --project src/MessSharp -- ./src gitlab csharp --reportfile gl-code-quality-report.json
artifacts:
  reports:
    codequality: gl-code-quality-report.json
```

This repository also self-checks after building. A finding fails the job with
exit code `2`.

## Maintainers

Command reference: [docs/usage.md](docs/usage.md). Rulesets: [docs/rules.md](docs/rules.md).
Contributing and SOLID guidance: [CONTRIBUTING.md](CONTRIBUTING.md).

Development checks:

```console
scripts/dotnet.sh test
```
