## Context
Delayed event handling is currently encoded in event classes via IDelayedEvent, which makes delay behavior class-scoped. Gameplay requires per-instance delay modes (e.g., some draws need QueueDrained, others ParentEffectsComplete).

## Goals / Non-Goals
- Goals:
  - Make delay configuration per event instance with safe defaults.
  - Preserve existing ordering behavior for current delayed events.
  - Keep the transaction engine as the single source of delay enforcement.
- Non-Goals:
  - Redesigning the event processing pipeline.
  - Changing replay/recording formats beyond what is required for delay configuration.

## Decisions
- Decision: Introduce DelayConfig on IDelayedEvent and use it in the transaction engine.
  - Reason: Enables instance-scoped delay while preserving existing delayed-event model.
- Decision: Default delay is disabled unless explicitly configured.
  - Reason: Avoids accidental reordering of unrelated events.

## Risks / Trade-offs
- Risk: Missed migration of an existing delayed event changes ordering.
  - Mitigation: Audit all current IDelayedEvent implementations and their creation sites.
- Risk: Delay config mutation after enqueue causes non-deterministic order.
  - Mitigation: Freeze delay configuration at enqueue time or document allowed timing.

## Migration Plan
1. Add DelayConfig and update IDelayedEvent.
2. Update GameActionTransactionService to read DelayConfig.
3. Migrate existing delayed events and creation sites.
4. Add ordering verification (tests/logs) for key flows.

## Open Questions
- Should delay configuration be serialized for replay/debugging bundles?
- Do we need a strict immutability policy after enqueue, or an enforcement mechanism?
