# Change: Update Decision Event Commit Flow

## Why
Decision effects (Choose/Random) currently resolve outside the event commit pipeline, making final results harder to persist and replay consistently. We need decision outcomes to be stored as event outputs while keeping branch abilities executed within the same transaction.

## What Changes
- Introduce `ChooseEvent` and `RandomEvent` and persist their outputs on commit.
- Pre-resolve cancellable Choose/Random in Phase 1; commit-phase Choose is non-cancellable.
- Move decision resolution into effect execution with event emission and commit-time output finalization.
- Keep branch abilities executing in the same transaction, but decouple decision outputs from EffectCompletedEvent.

## Impact
- Affected capability: event-decision (new incremental spec).
- Affected code: GameActionTransactionService, Ability.CreateEffectsAsync, RandomEffectHandler, ChooseEffectHandler, EffectExecutor, EventType, JsonGameEventRawEventMapper, SignalEvents.
