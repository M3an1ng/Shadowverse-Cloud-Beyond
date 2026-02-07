## Context
Chained abilities are triggered in ProcessEffectAsync immediately after ExecuteEffect returns, before effect-generated events are processed and committed. Some events (e.g., DrawEvent in search mode) only finalize targets during Commit, so chained abilities can observe incomplete data.

## Goals / Non-Goals
- Goals:
  - Trigger chained abilities only after all generated events are processed and their Commit has completed.
  - Keep the change minimal and localized to effect execution ordering.
  - Treat generated-event commit failures as effect failures for chained-ability gating.
- Non-Goals:
  - Redesign delayed-event or queue-drained mechanisms.
  - Change how EffectCompletedEvent outputs are computed beyond gating chained abilities.

## Decisions
- Reorder ProcessEffectAsync to:
  1) Execute effect and collect generated events.
  2) Process generated events (including Commit).
  3) Determine final effect outcome using handler success + generated-event processing result.
  4) Apply FailureHandling and run chained abilities based on the final outcome.
- FailureHandling is evaluated after event processing:
  - AbortAbility: return failure.
  - SkipEffect: do not run chained abilities.
  - CompleteWithFailure: run chained abilities even on failure.
- Assumption: effect execution does not generate delayed events that defer Commit beyond the immediate processing loop.

## Risks / Trade-offs
- EffectCompletedEvent.ParentEffectSucceeded and outputs are computed at execution time and will not reflect commit failures.
- Chained abilities will now run after any abilities triggered by EffectCompletedEvent when it is processed as a regular event.
- If delayed events are introduced in effect execution later, chained abilities may still run before those delays complete.

## Migration Plan
- Update ProcessEffectAsync ordering and FailureHandling flow.
- Add tests for chained timing after commit and failure gating.

## Open Questions
- Should EffectCompletedEvent outputs be recomputed to reflect commit failures in the future?
