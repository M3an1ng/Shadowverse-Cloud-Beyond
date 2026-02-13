## Context
- Event history is committed continuously and exposed through `context.eventHistory.events`.
- Current editor narrowing relies on AST-pattern inference (for example `self.EventType == "Destroy"`), which is brittle under expression rewrites and nested logic.
- Requirement is broader than EventHistory root: any `IEnumerable<IGameEvent>` should support typed narrowing and parameter-level filtering.

## Goals / Non-Goals
- Goals:
- Define one deterministic narrowing entrypoint for all event enumerables: `byType`.
- Keep user syntax natural and pipeline-friendly.
- Make frontend typing deterministic by explicit function argument, not expression inference.
- Preserve full-event accessibility with no DSL-level visibility whitelist and no `countByType` dependency.
- Non-Goals:
- Add new grammar or parser productions.
- Introduce dictionary-like `ByType`/`CountByType` runtime properties.
- Add scoped-history helper functions (`events(scope, ...)`) in this change.

## End-to-End Architecture
- Runtime chain:
- Parser lowers `source.byType("Damage")` to `byType(source, "Damage")`.
- Function registry resolves `byType`.
- Function filters source enumerable by resolved event type and returns `IEnumerable<IGameEvent>`.
- Existing pipeline nodes (`query/orderBy/take/distinct/select`) consume filtered output unchanged.
- Editor chain:
- Metadata exposes `byType` as callable on event collections.
- AST pass extracts `eventTypeHint` from second argument when it is deterministic (literal/enum token).
- Hint propagates through pipeline scope into `self` context.
- Option resolver maps `eventTypeHint` to event-parameter metadata and augments `self` candidate properties.

## Decisions
- Decision: `byType(source, eventType)` is the only typed-narrowing primitive in this change.
- `source`: any value coercible to `IEnumerable<IGameEvent>`.
- `eventType`: enum value or case-insensitive enum-name string.
- Return: `IEnumerable<IGameEvent>` (runtime shape unchanged).
- Decision: Deterministic frontend typing only from explicit `byType` argument.
- No best-effort inference from query predicates.
- Decision: Unknown/unresolvable `eventType` degrades safely.
- Runtime returns empty enumerable for that call.
- Editor falls back to base `IGameEvent` property set.
- Decision: Performance optimization is internal-only.
- `byType` may read source directly.
- Optional indexing/cache is allowed as an internal optimization but cannot change DSL contract.

## Phased Rollout
- Phase 1: Contract MVP
- Add/registry `byType`, expose metadata, and enable literal-based hint extraction.
- Ensure `context.eventHistory.events.byType("X").query(...)` works end to end.
- Phase 2: Genericization hardening
- Validate all entry points returning `IEnumerable<IGameEvent>` can call `byType`.
- Standardize pipeline hint propagation for `query/orderBy/take/distinct/select`.
- Phase 3: Optional performance hardening
- Add internal hot-path optimization (if profiling proves needed), keeping behavior unchanged.

## Phase 1 Blockers and Mitigations
- Blocker: Event type naming drift between backend enum and frontend labels.
- Mitigation: UI byType picker must use backend metadata keys; label/translation is display-only.
- Blocker: Pipeline scope may drop `eventTypeHint` after intermediate nodes.
- Mitigation: carry hint in normalized pipeline context object and reattach in each stage resolver.
- Blocker: Existing trigger-event narrowing path may conflict with byType hint precedence.
- Mitigation: precedence rule: explicit byType hint wins in that branch; fallback to trigger-event context otherwise.
- Blocker: Non-literal byType argument cannot be statically narrowed.
- Mitigation: editor fallback to base `IGameEvent` options and clear hint badge/state.

## Alternatives Considered
- Predicate inference (`self.EventType == ...`) as primary narrowing.
- Rejected: unstable, expression-form-dependent, hard to debug.
- EventHistory-only index properties (`ByType`, `CountByType`).
- Rejected: cannot cover all generic `IEnumerable<IGameEvent>` sources and fragments API surface.

## Risks / Trade-offs
- Runtime `byType` is O(N) on source size.
- Trade-off accepted for correctness/universality; optimize internally only if profiling requires.
- String-based event-type argument is error-prone when manually typed.
- Trade-off mitigated by enum-driven UI pickers and metadata-backed completion.

## Validation Strategy
- Backend tests:
- `context.eventHistory.events.byType("Damage")` returns only damage events.
- `IEnumerable<IGameEvent>` from non-EventHistory source can use `byType`.
- Chained query can read event-specific params after `byType`.
- Frontend tests:
- byType literal sets deterministic `self` parameter options.
- hint propagation survives pipeline stage transitions.
- unresolved byType argument falls back cleanly to base `IGameEvent` options.

## Open Questions
- Should byType argument editor default to enum token insertion or quoted string insertion?
