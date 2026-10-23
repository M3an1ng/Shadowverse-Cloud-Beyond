## ADDED Requirements

### Requirement: Unified turn-end cleanup timing
The system SHALL execute end-of-turn cleanup as a single boundary stage after all `TurnEnd` triggered abilities and their effects for the ending player have resolved, and before the next player's turn-start processing begins.

#### Scenario: Turn-end ability observes pre-cleanup counters
- **WHEN** a `TurnEnd` triggered ability checks or resolves targets using the ending player's combo counter
- **THEN** the ability SHALL observe the combo value accumulated during the ending turn

#### Scenario: Next turn starts after end cleanup
- **WHEN** end-of-turn cleanup completes for the ending player
- **THEN** the next player's turn-start processing SHALL begin only after combo reset, PP boost armed-state reset, and end-of-turn buff expiry events have been processed

### Requirement: Boundary cleanup includes player turn state
The system SHALL include player-owned turn-lifetime state in the end-of-turn cleanup stage, including the combo counter and PP boost armed state.

#### Scenario: Combo reset is observable
- **WHEN** the ending player's combo counter is greater than zero during end-of-turn cleanup
- **THEN** the system SHALL reset combo to zero and emit a `CounterChangedEvent`

#### Scenario: PP boost armed state reset is observable
- **WHEN** the ending player's PP boost armed state is true during end-of-turn cleanup
- **THEN** the system SHALL reset the armed state to false and emit a `PpBoostStateUpdatedEvent`

### Requirement: Boundary cleanup includes turn-duration buffs
The system SHALL process turn-duration buff expiry through the same boundary cleanup model as player turn state.

#### Scenario: End-of-turn buffs expire in end cleanup
- **WHEN** an `EndOfTurn`, `OwnerTurnEnd`, or `OpponentTurnEnd` buff reaches its expiry point
- **THEN** the system SHALL emit and process the corresponding `BuffRemovedEvent` during end-of-turn cleanup

#### Scenario: Start-of-turn buffs expire in start cleanup
- **WHEN** a `StartOfTurn`, `OwnerTurnStart`, or `OpponentTurnStart` buff reaches its expiry point
- **THEN** the system SHALL emit and process the corresponding `BuffRemovedEvent` during start-of-turn cleanup

### Requirement: Cleanup preserves persistent counters
The system SHALL only reset counters and player flags that are explicitly defined as turn-lifetime state for the current cleanup timing.

#### Scenario: Persistent counters survive end cleanup
- **WHEN** end-of-turn cleanup runs for a player with graveyard, bloodpool, or rally counters
- **THEN** the system SHALL NOT reset those persistent counters unless a separate rule or effect explicitly changes them

### Requirement: Cleanup event ordering is deterministic
The system SHALL process cleanup-generated events in a deterministic order so event history, SignalR updates, and replay output remain stable.

#### Scenario: Cleanup emits stable ordering
- **WHEN** end-of-turn cleanup produces counter, player-state, and buff-removal events
- **THEN** the system SHALL process those cleanup events in the same relative order for equivalent game states

#### Scenario: Cleanup refreshes derived state after removals
- **WHEN** boundary cleanup has processed all cleanup-generated events for the timing
- **THEN** the system SHALL refresh derived state caches before later turn-boundary processing observes affected card stats or keywords
