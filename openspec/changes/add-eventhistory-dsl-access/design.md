## Context
- Event history is stored via IEventHistoryService, but DSL only sees the current triggering event.
- Designers want to compute complex counters without bespoke per-card state.

## Goals / Non-Goals
- Goals:
  - Provide a read-only EventHistory interface on GameContext for DSL access.
  - Support common scopes (match, current turn, last turn, recent N events).
  - Enforce visibility rules for the requesting player.
- Non-Goals:
  - Persist event history beyond existing service storage policy.
  - Add write/mutation APIs for events through DSL.

## Decisions
- Decision: Add a small query model (scope, limit, visibility) and expose it via GameContext.EventHistory.
- Decision: Implement a DSL function `events(scope, filterExpr?, limit?)` that returns a list of IGameEvent.
- Decision: `events(...)` returns all events in scope by default; `limit` is optional and not capped.
- Decision: No visibility whitelist is applied; all event types are available to DSL.
- Alternatives considered:
  - Global static event history registry (rejected: lacks player context and visibility controls).
  - Hard-coded counters per use case (rejected: maintenance overhead).

## Risks / Trade-offs
- Event history size may impact performance; mitigate with scope and limit defaults.
- Predicate evaluation may be expensive; use cached DSL compilation and short-circuit on empty results.

## Migration Plan
- Add new interfaces and enums without breaking existing APIs.
- The optional `limit` parameter can be used by designers to bound queries when needed.

## Open Questions
- None.
