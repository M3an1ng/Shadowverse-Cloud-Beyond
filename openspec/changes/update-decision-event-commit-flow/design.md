## Context
Decision outcomes for Choose/Random are resolved before effect execution and injected into EffectCompletedEvent outputs. The requirement is to persist decision outputs via dedicated events, while still executing branch abilities in the same transaction, and to constrain cancellation to the pre-commit phase.

## Goals / Non-Goals
- Goals:
  - Emit `ChooseEvent`/`RandomEvent` whose outputs are finalized during commit.
  - Execute branch abilities within the same transaction as the parent effect.
  - Keep EffectCompletedEvent limited to chained ability information.
  - Allow Choose cancellation only during the cancellable (Phase 1) window.
- Non-Goals:
  - Rework general event processing or ability trigger architecture.
  - Introduce cross-transaction decision execution.

## Decisions
- Decision: Add `ChooseEvent` and `RandomEvent` that implement ICommittableEvent. Commit populates output parameters used for persistence/replay.
  - Rationale: Matches the current mutable-event pattern (e.g., DrawEvent) and ensures final outputs are stored after commit.
- Decision: Pre-resolve cancellable choices in Phase 1 and cache their selections for reuse in commit.
  - Rationale: Cancellable choices must complete before any irreversible effects run.
- Decision: Move decision resolution into effect execution (transaction scope) and emit decision events before executing branch abilities.
  - Rationale: Ensures decision outputs are committed and branch abilities remain in the same transaction.
- Decision: Remove decision outputs from EffectCompletedEvent outputs.
  - Rationale: EffectCompletedEvent should only capture chained ability info per requirement.
- Decision: Commit-phase Choose is non-cancellable; cancellation requests are rejected and a valid selection is required.
  - Rationale: Prevents inconsistent rollback after other effects have executed.

## Risks / Trade-offs
- Risk: DSL or downstream logic may rely on EffectCompletedEvent outputs for decision data.
  - Mitigation: Expose decision outputs via decision events (and update DSL access if required).
- Risk: Additional event types increase replay/serialization surface.
  - Mitigation: Add mapper coverage and unit tests for raw event mapping.

## Migration Plan
- Introduce `ChooseEvent`/`RandomEvent` and mapping.
- Pre-resolve cancellable decision selections in Phase 1 and reuse during commit.
- Update Random/Choose handlers and transaction flow to emit decision events and execute branch abilities in-line.
- Remove decision output merging into EffectCompletedEvent.
- Update any DSL or consumers that expect decision outputs on EffectCompletedEvent.

## Open Questions
- Should a cancelled Phase 1 decision emit a decision event, or should cancellation short-circuit without persistence?
