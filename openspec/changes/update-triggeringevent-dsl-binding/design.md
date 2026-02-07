## Context
The editor shows TriggeringEvent parameters globally and the GameEventBase fields can appear in UI suggestions.

## Goals / Non-Goals
- Goals: TriggeringEvent parameters are only visible within the TriggeringEvent context; parameters are filtered by event type; base fields are hidden.
- Non-Goals: Do not change runtime TriggeringEvent evaluation or replay/serialization flows.

## Decisions
- Decision: Use event metadata to drive TriggeringEvent parameter lists and expose them only via `context.triggeringEvent`.
- Alternatives considered: Keep global menu exposure or keep GameEventBase fields visible (both increase noise and misuse).

## Risks / Trade-offs
- Risk: Existing editor flows that rely on the global menu need migration. Mitigate with regression tests and UI validation.

## Migration Plan
1. Update backend metadata exposure.
2. Update frontend options binding.
3. Add regression tests.

## Open Questions
- Should base fields remain accessible via handwritten DSL even if the UI hides them?
