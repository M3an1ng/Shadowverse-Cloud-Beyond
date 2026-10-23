## Why

Turn-boundary state is currently cleaned at inconsistent points: player counters such as combo are reset inside `TurnEndEvent.Commit`, while end-of-turn buffs expire later in transaction cleanup. This causes turn-end abilities that read the current turn state to see different values during condition checks and target/effect resolution, producing bugs such as combo-based end-turn effects failing after combo has already been reset.

This change is needed now because turn-boundary semantics are becoming shared by counters, buffs, temporary action states, and DSL-triggered abilities; keeping each system's cleanup timing separate makes future card effects fragile and hard to reason about.

## What Changes

- Introduce a unified turn-boundary cleanup capability that defines when end-of-turn and start-of-turn state cleanup occurs relative to trigger processing, effect execution, and turn transition.
- Move end-of-turn player state cleanup, including combo reset and PP boost armed-state reset, out of direct `TurnEndEvent.Commit` cleanup and into the unified end-of-turn cleanup stage.
- Keep cleanup observable by emitting the existing state-change events, such as `CounterChangedEvent`, `PpBoostStateUpdatedEvent`, and `BuffRemovedEvent`.
- Align buff duration cleanup with the same boundary cleanup model, including end-of-turn and start-of-turn duration types.
- Ensure turn-end abilities resolve against the pre-cleanup state for the ending turn, while the next turn begins only after boundary cleanup has completed.
- Preserve existing public SignalR event contracts unless a design-phase review identifies a required event addition.

## Capabilities

### New Capabilities

- `turn-boundary-cleanup`: Defines unified cleanup timing for turn boundary state, including player counters, temporary player flags, and turn-duration buffs.

### Modified Capabilities

- None.

## Impact

- Affected backend flow: `GameActionTransactionService`, `TurnEndEvent`, `TurnStartEvent`, `PlayerState`, `BuffService`, and related domain events.
- Affected runtime behavior: timing of combo reset, PP boost armed-state reset, and turn-duration buff expiration.
- Affected tests: transaction/event ordering tests, buff duration tests, counter update tests, and card scenario coverage for turn-end combo effects.
- Data/schema impact: none expected.
- Frontend/API impact: existing event DTOs should remain compatible; event ordering may become more explicit and should be covered by tests.
