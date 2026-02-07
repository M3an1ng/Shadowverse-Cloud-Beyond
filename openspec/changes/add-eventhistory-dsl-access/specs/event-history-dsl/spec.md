## ADDED Requirements
### Requirement: GameContext EventHistory Reader
The system SHALL expose a read-only EventHistory reader on GameContext for DSL evaluation.

#### Scenario: Access event history in DSL
- **WHEN** a DSL expression references `context.EventHistory`
- **THEN** the reader is available and returns events for the current game context

### Requirement: EventHistory Scope Filtering
The EventHistory reader SHALL support scoped queries for match, current-turn, last-turn, and recent events, filtered to the requesting player's visibility.

#### Scenario: Request last turn events
- **WHEN** the reader is queried with scope `last-turn` for the requesting player
- **THEN** only events from the requesting player's previous turn are returned

### Requirement: Events DSL Function
The DSL SHALL provide `events(scope, filterExpr?, limit?)` to return a list of events using the GameContext EventHistory reader and an optional predicate.

#### Scenario: Filter events by type
- **WHEN** `events("match", "self.EventType == 'Destroy'")` is evaluated
- **THEN** the resulting list includes only Destroy events visible to the requesting player

#### Scenario: Apply limit
- **WHEN** `events("recent", null, 50)` is evaluated
- **THEN** at most 50 events are returned

### Requirement: EventHistory Unbounded Access
The EventHistory reader SHALL return all events within the requested scope when `limit` is omitted.

#### Scenario: No limit returns full scope
- **WHEN** `events("match")` is evaluated without a limit
- **THEN** all events in the match scope are returned

### Requirement: EventHistory Optional Limit
The EventHistory reader SHALL apply the `limit` parameter only when explicitly provided.

#### Scenario: Limit restricts result count
- **WHEN** `events("match", null, 50)` is evaluated
- **THEN** at most 50 events are returned

### Requirement: EventHistory Full Visibility
The EventHistory reader SHALL return all event types within the requested scope.

#### Scenario: Include request events
- **WHEN** `events("match")` is evaluated and AttackRequested events exist in scope
- **THEN** AttackRequested events are included in the results
