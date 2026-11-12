## Handoff

Stopped at the user's request. Draft PR #271 contains the current work on branch `fix/250-iterator-consumers`, head `71f7528`. **Do not merge yet.** The previous head `b68ed29` failed independent review; this new head has not been independently reviewed.

### What changed

The patch transfers deferred iterator effects at `Contains`, spread, `List<T>(sequence)`, and `AddRange`. It also handles static `Enumerable.Contains`, implicit arrays, and several alias cases. Earlier reviewers found incorrect guesses about custom extension overloads. The latest WIP replaces the binary guess with `Exact`, `Incompatible`, and `Uncertain` argument compatibility. When the analyzer cannot establish the selected sequence consumer, it reports `UnknownEffect` rather than inventing or dropping effects.

This is a conservative tradeoff. An ambiguous call can report `UnknownEffect` even when the compiler selects a pure method. The issue's original fallback permits unknown effects for unresolved binding, but the reviewer must check the pure-control requirement and actual user-visible behavior.

### Verification

- `githooks/pre-commit` passed on `71f7528`: formatting, warnings-as-errors build, **1,274 tests**, and self-analysis.
- A compiler-backed local matrix reproduced five failures on `b68ed29`. On the WIP head all action cases report either the real effect or `UnknownEffect`, and no case invents an input or output. The matrix allows `UnknownEffect` for ambiguous pure cases.
- The latest generic-receiver focused tests passed after the interrupted run was retried. **No independent review of `71f7528` has run.** CI on this new head was not watched to completion.

### Resume

1. Review `CallResolver`, `ExtensionArgumentTypes`, `PrimitiveConversions`, `SequenceConsumption`, and `ExplicitnessExecutionTests`. Check that `Exact` really means the custom extension is selected, including namespace visibility, generic receiver types, constants, `params`, and overload ranking. Check that `InvocationEdges` cannot transfer an incompatible custom body.
2. Run the four original action/pure consumer cases and the retained compiler-backed `nint`, `params`, enum, and constant-conversion cases. In this workspace the reusable script is `.audit/250-binding-matrix.py`; its fixtures and earlier independent reports are under `.audit/250-review-evidence/` and `.audit/250-verifier-round5-report.md`. Those workspace audit files are local, not part of the pushed branch. The new regression tests are committed in `tests/MessSharp.Tests/ExplicitnessExecutionTests.cs`.
3. Have one independent verifier review the exact head and run the real CLI. Wait for all GitHub checks. If the patch or `main` changes, repeat verification before merging through `land-the-plane`.

The project has a prior explicit restriction against retrying the XML scan of `./tests/MessSharp.Tests` or achieving the same outcome by another command. Do not repeat that scan.
