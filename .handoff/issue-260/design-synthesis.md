# Issue 260 design selection

## Goal

Report caller-owned mutations through intermediate local-function calls without treating private clones as caller-owned. Preserve capture wording, control flow, and callback timing.

## Grounding

The CLI reproduction and instrumented transfer showed that `Outer` has no capture slot for `c`; the `Poke` effect maps to empty private roots and disappears. Adding a real use of `c` to `Outer` carries the effect through to `Run`. See `260-how.md` and the instrumentation transcript at `/Users/jonathanbaldie/.hermes/profiles/pstack/cache/delegation/live/deleg_9de2e854/task-0.log`.

## Candidates

- A computes transitive capture requirements before building effect summaries.
- B forwards declaration-keyed free-storage effects through the existing summary fixed point, resolving origins where their declaring frame is available.

Both need to distinguish captures from explicit arguments, retain private/shared storage origins, and avoid conflating shadowed declarations. A second call-edge discovery pass is a risk for A because existing edge construction depends on summaries. B avoids that pass but its proposed migration of name-keyed storage is broader than the demonstrated issue.

## Selection

Use B as the design direction for a spike and review it against the existing code before committing to a full migration. Forwarded mutation effects are real effects and may report on an intermediate callable. A requirement with no executed effect must not create a capture read or mutation.

Graft A's invariance test with and without an incidental `c` read, its multi-hop and private-copy controls, and its explicit distinction between direct and forwarded captures. Do not assume the cross-judge's suggested reserved-string bridge is safe. The existing string roots have special meanings, and a bridge that collides or requires permanent parallel representations is worse than a typed value. Prove any narrower representation on actual code and cover its consumers, or reject it.

The Claude Opus cross-review is in `260-cross-judge.json`. It favored B but could not inspect the instrumentation log or verify the proposed declaration resolver and closure timing. The review is not proof that either design is implementable as written.

## Verification gates

The existing red regression must go green. Exercise the real CLI fixture, direct call, incidental mention and private clone. Add coverage for deeper calls and uninvoked mutators. Probe shadowing, recursion, callback and deferred cases with valid C# before expanding scope. Run the full repository checks through Docker and independently review the exact final diff. Preserve unrelated existing behavior and do not bypass hooks.
