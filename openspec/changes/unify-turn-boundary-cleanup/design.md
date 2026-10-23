## Context

Turn-boundary cleanup is currently split across multiple responsibilities. `TurnEndEvent.Commit` delegates to `PlayerState.EndTurn`, which immediately resets combo and PP boost armed state. End-of-turn buff expiry is collected later by `GameActionTransactionService` after `TurnEndEvent` commit, and start-of-turn buff expiry is handled in a separate start-turn branch.

This split creates inconsistent state windows. A turn-end ability can pass conditions using pre-cleanup combo, but its deferred target resolution can later run after combo has already been reset. The cleanup model needs to become explicit and shared across counters, player flags, and buffs.

## Goals / Non-Goals

**Goals:**
- Define a single cleanup stage for each turn boundary timing.
- Move combo and PP boost armed-state cleanup out of direct `TurnEndEvent.Commit`.
- Keep cleanup observable through existing domain events.
- Keep persistent counters such as graveyard, bloodpool, and rally untouched by turn-end cleanup.
- Preserve existing SignalR DTO contracts unless implementation discovers a necessary compatibility issue.
- Add regression coverage for turn-end combo effects and cleanup ordering.

**Non-Goals:**
- Redesign the entire transaction/event queue architecture.
- Change card database ability DSL syntax.
- Change persistent counter semantics.
- Add new public frontend APIs unless required for event ordering visibility.

## Decisions

### Use a boundary cleanup coordinator

Create a small domain/application service that coordinates cleanup for a declared timing, such as `TurnEnd` or `TurnStart`. The coordinator will collect cleanup events from player state and buff duration rules, then let the existing transaction recursion process those events.

Alternatives considered:
- Keep cleanup inside `TurnEndEvent.Commit`: rejected because it preserves the current timing split.
- Move only combo reset after buff expiry: rejected because it fixes one symptom while leaving future player turn state vulnerable to the same ordering issue.
- Let each subsystem register ad hoc cleanup callbacks: rejected for now because the current need is narrow and a callback registry would add unnecessary abstraction.

### Keep state mutation event-driven

Cleanup must continue to emit existing events such as `CounterChangedEvent`, `PpBoostStateUpdatedEvent`, and `BuffRemovedEvent`. Direct silent mutation is not acceptable because event history, replay, UI updates, and ability listeners depend on observable domain events.

Player state cleanup should expose a method that returns generated events for a specific cleanup timing rather than directly being called by `TurnEndEvent.Commit`.

### Treat buffs as cleanup participants

`BuffService.CollectEndOfTurnExpiryEvents` and `CollectStartOfTurnExpiryEvents` already model buff expiry as generated `BuffRemovedEvent`s. The unified coordinator should reuse this behavior, either by wrapping the existing methods or by moving their timing switch behind a common cleanup API.

The first implementation should avoid changing buff duration schema. Existing `DurationType` values remain the source of truth for which cleanup timing applies.

### Ensure turn-end effects drain before cleanup

End-of-turn cleanup must run after `TurnEnd` triggered abilities and their effects have resolved. This is the key behavioral change that lets turn-end abilities evaluate and resolve targets against the ending turn's final pre-cleanup state.

This implies the transaction service needs an explicit point after the relevant effect queue is drained for the `TurnEndEvent`, before generated turn-start processing for the next player observes state.

### Preserve deterministic ordering

Cleanup-generated events should be collected in a stable order:
1. Player turn-lifetime state events for the subject player.
2. Buff removal events in the existing deterministic card/buff traversal order.
3. Derived state cache refresh after generated cleanup events are processed.

If existing buff traversal order is not stable enough, implementation should sort by stable in-game/card identifiers before emitting events.

## Risks / Trade-offs

- [Risk] Moving cleanup later can expose combo or PP boost armed state to additional events that previously saw cleaned state. → Mitigation: limit the later window to turn-end triggers/effects only, and add ordering tests around turn start.
- [Risk] Processing cleanup after queued effects may be hard to place in the current transaction recursion. → Mitigation: keep the first version scoped to `TurnEndEvent` and reuse existing recursive event processing helpers.
- [Risk] Buff expiry and player cleanup can trigger secondary abilities, increasing event ordering complexity. → Mitigation: process cleanup as generated domain events through the existing transaction path rather than mutating silently.
- [Risk] UI may briefly receive counter reset later than before. → Mitigation: the reset still occurs before the next turn begins; verify SignalR event order in integration tests.

## Migration Plan

1. Add the boundary cleanup abstraction and route end-of-turn cleanup through it.
2. Remove combo and PP boost armed-state mutation from direct `TurnEndEvent.Commit`.
3. Move current end-of-turn buff expiry collection behind the cleanup stage.
4. Align start-of-turn buff expiry with the same abstraction without changing its existing timing unless tests reveal current behavior depends on a different order.
5. Add regression tests for combo-based turn-end target resolution, cleanup event ordering, and persistent counter preservation.

Rollback is straightforward: restore `PlayerState.EndTurn` cleanup from `TurnEndEvent.Commit` and call the previous buff expiry collection points directly. No database migration is expected.

## Open Questions

- Should start-of-turn cleanup run before or after `TurnStart` triggered abilities? The initial implementation should preserve the current effective start-turn ordering while making the cleanup mechanism explicit.
- Should cleanup itself be represented as a domain event such as `TurnBoundaryCleanupEvent`, or remain an internal transaction stage? A domain event improves history visibility; an internal stage is smaller. Implementation should choose the simpler option that still satisfies deterministic ordering and observability of generated cleanup events.
