## 1. Phase 1 - Contract MVP
- [x] 1.1 Implement DSL function `byType(source, eventType)` for generic `IEnumerable<IGameEvent>` filtering.
- [x] 1.2 Register `byType` in function registry and expose function metadata (including event type argument options).
- [x] 1.3 Verify member-call lowering path supports `source.byType("X").query(...)` with no parser changes.
- [x] 1.4 Validate `context.eventHistory.events` works as canonical source for `byType`.
- [x] 1.5 Implement editor extraction of `eventTypeHint` from deterministic `byType` arguments (literal/enum token).

## 2. Phase 2 - Genericization and UX Hardening
- [x] 2.1 Propagate `eventTypeHint` across all pipeline scopes (`query`, `orderBy`, `take`, `distinct`, `select`).
- [x] 2.2 Standardize `self` option resolution: `IGameEvent` base fields + hint-driven event parameters.
- [x] 2.3 Define precedence between byType hint and trigger-event context (explicit byType hint wins in branch).
- [x] 2.4 Add fallback UX when byType argument is non-deterministic (clear hint and show base `IGameEvent` only).

## 3. Phase 3 - Verification and Optional Performance Hardening
- [x] 3.1 Add backend tests:
- `context.eventHistory.events.byType("Damage")`
- non-EventHistory `IEnumerable<IGameEvent>.byType("Destroy")`
- chained `query(self.<event-param>)` after byType
- [x] 3.2 Add frontend tests:
- byType-driven strong typing and parameter suggestions
- hint propagation across pipeline nodes
- unresolved hint fallback behavior
- [ ] 3.3 Profile hot paths and decide whether internal indexing/caching is needed without changing DSL contract.
