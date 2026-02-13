## ADDED Requirements
### Requirement: GameContext EventHistory Reader
The system SHALL expose a read-only EventHistory reader on GameContext for DSL evaluation.

#### Scenario: Access event history in DSL
- **WHEN** a DSL expression references `context.eventHistory`
- **THEN** the reader is available and returns events for the current game context

### Requirement: EventHistory Full Event Access
The EventHistory reader SHALL continue to expose full event history through `Events` without restricting event types.

#### Scenario: Raw full-history access remains available
- **WHEN** a DSL expression references `context.eventHistory.events`
- **THEN** all recorded events for the game are available in storage order

### Requirement: Generic byType Filtering for Event Enumerables
The DSL runtime SHALL provide `byType(source, eventType)` for filtering any `IEnumerable<IGameEvent>` by event type.

#### Scenario: Filter EventHistory events by type
- **WHEN** a DSL expression evaluates `context.eventHistory.events.byType("Damage")`
- **THEN** the result contains only events whose `EventType` is `Damage`

#### Scenario: Filter non-EventHistory enumerable by type
- **WHEN** a DSL expression evaluates `someEnumerableOfGameEvents.byType("Destroy")`
- **THEN** the result contains only events whose `EventType` is `Destroy`

#### Scenario: Unknown event type key
- **WHEN** `byType` receives an unknown or unresolvable event type argument
- **THEN** the result is an empty event enumerable

### Requirement: byType Pipeline Compatibility
The system SHALL allow results of `byType` to continue through standard DSL collection pipelines.

#### Scenario: Query after byType can access event parameters at runtime
- **WHEN** a DSL expression evaluates `context.eventHistory.events.byType("Damage").query(self.DamageAmount > 0)`
- **THEN** the query executes successfully and evaluates `DamageAmount` on each filtered event

#### Scenario: Parser lowering compatibility
- **WHEN** a user writes method-call syntax `source.byType("Damage")`
- **THEN** the expression is handled through existing function-call lowering without requiring grammar changes

### Requirement: Deterministic Editor Narrowing from byType Argument
The DSL editor SHALL derive event-type narrowing from explicit `byType` argument values and provide deterministic `self` parameter suggestions.

#### Scenario: Literal byType argument narrows self parameter options
- **WHEN** the user builds `context.eventHistory.events.byType("Destroy").query(...)`
- **THEN** `self` options in `query` include `Destroy` event parameters (for example `DestroyTarget`, `Reason`) without predicate inference

#### Scenario: Non-deterministic byType argument falls back safely
- **WHEN** the `byType` argument cannot be resolved to a concrete event type in editor context
- **THEN** `self` options fall back to base `IGameEvent` fields only

### Requirement: Backward Compatibility for Existing EventHistory DSL Paths
Existing DSL expressions that use `context.eventHistory.events` and generic query pipeline operations SHALL continue to evaluate correctly.

#### Scenario: Existing generic query remains valid
- **WHEN** an existing expression `context.eventHistory.events.query(self.EventType == "Destroy")` is evaluated
- **THEN** it still executes successfully with unchanged semantics
