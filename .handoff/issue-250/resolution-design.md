# Issue 250: consumer binding after failed reviews

## Premise tested

The previous implementation assumed a partial list of implicit conversions was enough to decide whether a same-file extension is the selected consumer. `.audit/250-binding-matrix.py` disproves that assumption. Compiler-confirmed `nint`, `params`, enum, and constant-expression cases select a different operation from the one inferred by the patch.

## Decision

Treat syntax-only extension compatibility as three states. `Exact` means that each supplied argument has a provably identical primitive type and no `params` expansion is involved. `Incompatible` means that the argument and parameter have distinct, incompatible primitive families. `Uncertain` covers all other cases, including non-identical numeric types, constants, native integers, unknown types, and `params`.

A sequence consumer may transfer a known iterator effect only when a standard operation is established. A definitely selected custom extension keeps its existing call edge. When a custom extension might apply but its selection is uncertain, do not attach either the custom effect or the iterator effect. Report `UnknownEffect` at the consumer instead. This follows #250's explicit fallback for unresolved binding and avoids false certainty.

Known standard operations with no possible same-file competing extension remain unchanged, so the issue's ordinary action and pure controls stay precise. Explicit standard list instance operations and explicitly qualified `System.Linq.Enumerable.Contains` use their existing type checks. The public analyzer remains syntax-only.

## Tradeoffs and limits

An uncertain custom overload may produce `UnknownEffect` even where the compiler can choose a pure standard or pure custom method. This is deliberate: guessing the selected body caused both silent action cases and invented inputs or outputs. The original issue permits unknown effects when binding cannot be resolved. Improving precision for constants, `params`, and overload ranking requires a separate, evidenced binding model. Do not treat a passing compiler build or the current test suite as proof that such a model is complete.

## Checkpoint

1. Run the compiler-backed matrix before the change. It must reproduce the reported missing or invented effects.
2. Implement the three-state comparison and use it consistently in both call edges and sequence-consumer edges.
3. Re-run the matrix, original action/pure CLI cases, existing effect-transfer tests, and the full repository gate.
4. Obtain one independent review of the exact head. Do not merge if it finds a user-visible regression or a failed gate.
