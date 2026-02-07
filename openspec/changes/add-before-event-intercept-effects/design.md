## Context
We already support TriggerTiming.Before, but it only executes regular effects before commit. There is no mechanism to rewrite or replace the triggering event.

## Goals
- Allow deterministic interception of any event before commit.
- Support two intercept effect types: parameter rewrite and event replacement.
- Ensure no direct state mutation occurs during the Before phase.
- Prevent infinite interception loops while still allowing re-evaluation after a rewrite.

## Non-Goals
- No arbitrary state mutation in Before intercepts.
- No full-blown "stack" or priority system beyond deterministic ordering.
- No global refactor of On/After timings.

## Decisions
- Decision: Intercepts apply to any event type, but only rewritable parameters can be modified.
  - Rationale: Meets mechanics needs without maintaining a fragile allowlist.
- Decision: Intercept effects are limited to rewriting event parameters or replacing the event.
  - Rationale: Keeps Before phase side-effect free and debuggable.
- Decision: Parameter rewrites MUST target properties annotated with `GameEventParameter` where `Scope` is `Input` or `Both`.
  - Rationale: Prevents mutation of derived/output-only values and keeps rewrites safe.
- Decision: If a parameter is not rewritable (missing annotation, no backing field), the rewrite fails gracefully and defers to `OnFailure`.
  - Rationale: Avoids silent corruption while keeping intercepts safe.
- Decision: Only TriggerTiming.Before can produce intercept output, and the original event is not committed when replaced.
  - Rationale: Ensures On/After triggers only see the final event and preserves existing timing semantics.
- Decision: After any rewrite/replace, Before intercepts are re-evaluated until no changes occur.
  - Rationale: Eligibility can depend on event parameters/type; a rewrite can change which abilities should fire.
- Decision: Enforce a maximum intercept iteration count (e.g., 8) with deterministic conflict rules.
  - Rationale: Prevents infinite loops caused by oscillating rewrites or competing replacements.
  - Conflict rule: parameter rewrites apply in execution order with last-write-wins; event replacement uses first-write-wins per iteration.
- Decision: Before intercepts execute immediately as each eligible ability is discovered, rather than batching all candidates.
  - Rationale: Ensures rewrites take effect as soon as possible and prevents On/After from observing stale event state.
- Decision: Each ability can intercept a given event ID at most once per intercept cycle.
  - Rationale: Prevents repeated self-triggering while still allowing new abilities to become eligible after a rewrite.
- Decision: Leave-field vanish is implemented via card-configured Before abilities (not a system ability/keyword) that replace Destroy/Return/Tuck with Vanish.
  - Rationale: Keeps the mechanic data-driven, reusable, and avoids special-case keywords.

## Mechanics
- Intercepts only run during the Before phase and do not mutate game state.
- Parameter rewrites operate on annotated input parameters and use type-safe conversion before applying.
- A replacement immediately yields a new event for the next iteration; the original event is not committed.
- Parameter rewrites are applied in order within the same iteration.
- Intercepts execute one-by-one with immediate re-evaluation after each rewrite/replace.
- A per-event execution record tracks which abilities have already intercepted the current event ID.

## Risks / Trade-offs
- Generic rewrites require reflective access and may fail for events without safe backing fields.
- Loop cap may allow conflicting intercepts to settle arbitrarily at the final iteration.

## Migration Plan
- Expand intercept pipeline to all event types with safe rewrite validation.
- Remove LeaveVanish keyword/system ability; configure cards with a Before intercept ability that executes ReplaceEvent.
- Add tests for rewrite, replace, loop protection, and configurable leave-field interception.

## Open Questions
- What is the appropriate max iteration count per transaction?
- Should replacement be allowed to short-circuit remaining intercepts in the current iteration?
