## 1. Regression Tests

- [x] 1.1 Add a failing test showing a `TurnEnd` combo-based target query observes the ending turn combo before cleanup.
- [x] 1.2 Add a failing test proving combo reset emits `CounterChangedEvent` during end-of-turn cleanup.
- [x] 1.3 Add a failing test proving PP boost armed-state reset emits `PpBoostStateUpdatedEvent` during end-of-turn cleanup.
- [x] 1.4 Add a failing test proving graveyard, bloodpool, and rally counters survive end-of-turn cleanup.
- [x] 1.5 Add or update buff duration tests proving end-of-turn and start-of-turn buff expiry are processed by boundary cleanup.

## 2. Cleanup Model

- [x] 2.1 Add a cleanup timing model for turn boundary cleanup, covering turn end and turn start.
- [x] 2.2 Add a focused cleanup coordinator that collects cleanup-generated events for a timing and subject player.
- [x] 2.3 Move player turn-lifetime cleanup out of direct `TurnEndEvent.Commit` and into a player-state cleanup method used by the coordinator.
- [x] 2.4 Ensure player cleanup resets only explicitly turn-lifetime state, including combo and PP boost armed state.
- [x] 2.5 Route existing buff end/start expiry collection through the coordinator without changing duration type data shape.

## 3. Transaction Integration

- [x] 3.1 Insert end-of-turn cleanup after `TurnEnd` triggered abilities and their effects have resolved.
- [x] 3.2 Ensure next-player turn-start processing begins only after end-of-turn cleanup-generated events are processed.
- [x] 3.3 Keep start-of-turn buff cleanup behavior equivalent to current behavior while using the shared cleanup path.
- [x] 3.4 Refresh derived state cache after cleanup-generated events are processed for each boundary timing.
- [x] 3.5 Preserve deterministic cleanup event ordering for player state events and buff removal events.

## 4. Validation

- [x] 4.1 Run focused backend tests for transaction ordering, player counters, PP boost state, and buff duration expiry.
- [x] 4.2 Run the broader backend test suite or the relevant stable subset documented for core game flow changes.
- [x] 4.3 Verify no SignalR DTO contract changes are required for existing counter, PP boost, and buff removal updates.
- [x] 4.4 Update changelog or developer notes if implementation changes observable event ordering.
