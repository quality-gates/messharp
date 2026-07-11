# Domain Docs

**Layout: single-context.**

- `CONTEXT.md` at the repo root captures the core vocabulary and seams for C# mess detection (runner, file discoverer, source parser, ruleset loader).
- `docs/adr/` holds Architecture Decision Records. The directory doesn't exist yet — create it when the first ADR is written.

## Consumer rules

- Skills that need domain vocabulary or seam names (e.g. `to-spec`) should read `CONTEXT.md` first, rather than re-deriving terminology from the source tree.
- Skills that need to know *why* a past architectural decision was made should look in `docs/adr/` for a matching ADR before re-litigating it.
- If `CONTEXT.md` doesn't cover a term or seam a skill needs, treat that as a signal to update `CONTEXT.md` rather than guessing.
