# Contributing to MessSharp

Thank you for your interest in contributing to **MessSharp**! This project is part of the `quality-gates` organization and enforces strict quality gates (unit testing, mutation testing, formatting, and security audits). 

To ensure you can contribute effectively without breaking any quality gates, we rely on agentic AI skills and conventions.

---

## 🛠️ Required AI Agent Skills

If you are using an AI coding assistant (such as Claude Code or another agentic assistant), you **must** install and use the following skills by Matt Pocock.

### 1. General Developer Skills (`/tdd` and `/diagnosing-bugs`)
These skills guide the agent through structured development and debugging workflows.

To install:
```bash
npx -y skills add mattpocock/skills
```

*   **When to use `/tdd`:** Use this skill when implementing new features, rule checks, metric calculations, or when writing regression tests first for bug fixes. It enforces a strict vertical-slice Red-Green-Refactor loop.
*   **When to use `/diagnosing-bugs`:** Use this skill whenever a test fails, a local build warning is raised, a CI run fails, or a smoke test encounters unexpected behavior. It forces the agent to build a tight, reproducible feedback loop before attempting to diagnose or write a fix.

### 2. Git Safety Guardrails
To prevent AI agents from accidentally performing destructive Git operations (such as force pushing, hard resets, or deleting branches), you must install the Git safety guardrails:

To install:
```bash
npx skills add https://github.com/mattpocock/skills --skill git-guardrails-claude-code
```

*   **When to use:** This skill runs automatically as a `PreToolUse` hook to block dangerous commands (like `git push`, `git reset --hard`, `git clean -f`, or `git branch -D`) before they are executed by the assistant.

---

## 🤖 AI Agent Conventions (`AGENTS.md` / `GEMINI.md`)

Before starting any task, coding assistants **must** read and adhere to the project conventions documented in **[AGENTS.md](file:///Users/jonathanbaldie/Code/quality-gates/messharp/AGENTS.md)** (also mirrored in **[GEMINI.md](file:///Users/jonathanbaldie/Code/quality-gates/messharp/GEMINI.md)**).

These files detail:
- **Build & Test Workflows:** Standard wrapper script execution (`scripts/dotnet.sh`).
- **Key Directories:** Code layout and package scopes.
- **Shipping Workflow:** Steps required in order before pushes/merges.
- **Conventions:** Strict requirements for file editing, complexity thresholds, and git worktrees.

---

## 🧱 SOLID Refactoring & Complexity Rules

When modifying or refactoring code to meet quality gate metrics:
*   **No Cheats:** Do not bypass cyclomatic complexity (CCN) or NPath limits using cheats (e.g. compressing syntax, inline hacks).
*   **SOLID Design:** Refactor using clean **SOLID** design principles.
*   **Horizontal Code Sharing:** Do not bypass complexity limits by moving logic into extension methods, default interface implementations, or trait-like helper APIs. Prefer clean decomposition instead:
    - **Composition** (extracting focused, decoupled helper classes).
    - **Extension Methods** only when they keep shared static logic explicit and cohesive.
    - **Default Interface Implementations** only for genuine interface behavior, not as trait-like dumping grounds.

---

## 🚀 Quality Gate Checklist

Before submitting a Pull Request, ensure that:
1. All unit tests pass: `scripts/dotnet.sh test`
2. Self-analysis is clean: `scripts/dotnet.sh run --project src/MessSharp -- ./src text csharp --ignore-tests`
3. Code formatting conforms: `dotnet format --verify-no-changes`
4. Scoped mutation score meets the gate: Stryker.NET mutation testing (`--mutate "Metrics/**/*.cs"`) must score **>= 75% MSI** (Covered-MSI >= 80%).
