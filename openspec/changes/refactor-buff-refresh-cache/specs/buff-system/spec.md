## ADDED Requirements
### Requirement: Derived State Cache
The system SHALL compute and cache each card's derived stats and keywords per refresh cycle and reuse the cached values for all queries within that cycle.

#### Scenario: Reuse within a refresh cycle
- **GIVEN** a card with multiple buffs and refresh cycle id X
- **WHEN** derived stats are queried multiple times without a refresh
- **THEN** the system MUST NOT re-evaluate ApplicabilityCondition for the same buff more than once in cycle X

### Requirement: Cache Consistency Across Derived Queries
The system SHALL return a consistent set of derived values (attack, max health, cost, keywords) computed from the same refresh cycle.

#### Scenario: Consistent values in one cycle
- **GIVEN** a card with multiple buffs and refresh cycle id X
- **WHEN** attack and keywords are queried within cycle X
- **THEN** both values MUST be computed from the same cached evaluation

### Requirement: Cache Invalidation on Buff Mutation
The system SHALL invalidate cached derived values for a card when its buff set changes.

#### Scenario: Buff applied invalidates cache
- **GIVEN** a card with a cached derived state in cycle X
- **WHEN** a buff is applied to the card during a transaction
- **THEN** the cache for that card MUST be invalidated before the next refresh cycle

### Requirement: Refresh Cycle Boundaries
The system SHALL advance the refresh cycle after each committed transaction and at turn boundary processing (TurnStart and TurnEnd).

#### Scenario: Transaction-end refresh
- **GIVEN** a transaction that applies and removes buffs
- **WHEN** the transaction commits
- **THEN** a new refresh cycle MUST begin before the next derived-state query

#### Scenario: Turn boundary refresh
- **GIVEN** a turn transition that commits TurnEndEvent and TurnStartEvent
- **WHEN** the turn boundary processing completes
- **THEN** the refresh cycle MUST advance before the next derived-state query

### Requirement: Conditional Buff Evaluation Window
The system SHALL evaluate ApplicabilityCondition only during refresh cycles and SHALL treat the cached applicability results as authoritative within a cycle.

#### Scenario: Conditional evaluation only at refresh
- **GIVEN** a conditional buff with ApplicabilityCondition
- **WHEN** derived stats are queried within the same refresh cycle
- **THEN** the system MUST reuse the cached applicability result

#### Scenario: Condition changes mid-transaction
- **GIVEN** a conditional buff that depends on game state
- **WHEN** the condition changes during a transaction after the refresh cycle started
- **THEN** the buff's applicability MUST take effect on the next refresh cycle

### Requirement: Duration-Based Expiry at Turn Boundaries
Buffs with duration types EndOfTurn and StartOfNextTurn SHALL be processed at the corresponding turn boundary event.

#### Scenario: End-of-turn expiry
- **GIVEN** a buff with duration EndOfTurn applied during a turn
- **WHEN** TurnEndEvent is committed
- **THEN** the buff MUST be removed before any TurnStart processing in the same transaction

#### Scenario: Start-of-next-turn expiry
- **GIVEN** a buff with duration StartOfNextTurn
- **WHEN** TurnStartEvent is committed for the owning player
- **THEN** the buff MUST be removed before any start-of-turn effects are processed

### Requirement: DurationType And RemovalTrigger Separation
DurationType SHALL represent time-based lifetimes only, while RemovalTrigger and RemovalCondition SHALL represent non-time conditional removal.

#### Scenario: Time-based buff without removal trigger
- **GIVEN** a buff with DurationType set to EndOfTurn or StartOfNextTurn
- **WHEN** the buff is created
- **THEN** RemovalTrigger and RemovalCondition MUST be ignored or absent

#### Scenario: Conditional removal without DurationType
- **GIVEN** a buff that must be removed based on a specific event or condition
- **WHEN** the buff is created
- **THEN** DurationType MUST be Permanent and removal MUST be governed by RemovalTrigger and RemovalCondition
