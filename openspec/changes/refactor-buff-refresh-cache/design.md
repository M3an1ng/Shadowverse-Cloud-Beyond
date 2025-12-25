## Context
The Buff system currently evaluates ApplicabilityCondition during derived-state reads (stats/keywords). With high Buff counts and complex DSL conditions, this produces repeated evaluations and becomes a scalability bottleneck.

## Goals / Non-Goals
- Goals:
  - Reduce repeated ApplicabilityCondition evaluation during reads.
  - Keep correctness without dependency tracking.
  - Support duration-based expiry at turn boundaries.
- Non-Goals:
  - Full dependency graph tracking for DSL conditions.
  - Immediate rewrite of all Buff creation paths.

## Decisions
- Decision: Introduce revision-based derived-state caching per card.
  - Reason: Avoid per-read DSL evaluation while keeping correctness via coarse invalidation.
- Decision: Define refresh points at transaction end and turn boundaries.
  - Reason: Guarantees a stable window for cached derived state and clear expiry timing.
- Decision: Evaluate ApplicabilityCondition only during refresh cycles.
  - Reason: Makes performance predictable under high Buff counts.

## Risks / Trade-offs
- Cache memory overhead increases with cards and cached derived data.
- Coarse invalidation can trigger more recomputation than necessary.
- Missing a refresh point could leave stale derived state (needs tests).

## Migration Plan
1. Add revision counters and cache primitives.
2. Add refresh pipeline at transaction end and turn boundaries.
3. Update BuffService to use cached derived state.
4. Add tests for expiry and refresh correctness.

## Open Questions
- Should refresh be global per transaction or scoped to affected cards only?
- Do we need a debug toggle to trace refresh cycles in logs?
