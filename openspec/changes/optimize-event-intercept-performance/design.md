## Context
RewriteEvent and ReplaceEvent resolve parameters and construct events via reflection on every intercept. This is safe but slower than standard effects that use cached handler dispatch.

## Goals / Non-Goals
- Goals:
  - Reduce per-intercept reflection overhead to approach normal effect execution speed.
  - Keep semantics identical (no behavioral change).
  - Maintain thread safety under concurrent games.
  - Enable reuse of cached event parameter metadata across runtime and editor-facing services.
- Non-Goals:
  - No changes to DSL syntax or effect signatures.
  - No behavioral changes to Before/On/After ordering.

## Decisions
- Decision: Build a cache that maps EventType to an intercept metadata descriptor containing:
  - Rewrite setters (compiled delegates) keyed by parameter name and display name.
  - Replacement constructor delegates with parameter binding metadata.
- Decision: Cache value coercers by target type to avoid repeated conversion overhead.
- Decision: Keep a reflection fallback for unsupported cases, with logging to surface cache misses.
- Decision: Intercept ability discovery may use an indexed subset to reduce candidate scanning.
- Decision: Expose a shared event metadata cache (parameters, setters, coercers) for use by event metadata services, DSL accessors, and effect parameter binding.

## Risks / Trade-offs
- Cache warm-up cost on first use of an event type.
- Incorrect parameter binding could cause hard-to-debug runtime failures; keep fallback and logging.

## Performance Notes
- First-use cache warm-up may still incur reflection cost; steady-state should approach standard effect overhead.
- Cache misses SHOULD fall back to the existing reflection path for safety.

## Migration Plan
- Introduce cache types and unit tests for binding correctness.
- Wire ReplaceEvent/RewriteEvent to use cached binders with fallback.
- Add lightweight diagnostics to confirm cache hit rates during tests.

## Open Questions
- Do we need explicit cache invalidation, or is static caching sufficient?
