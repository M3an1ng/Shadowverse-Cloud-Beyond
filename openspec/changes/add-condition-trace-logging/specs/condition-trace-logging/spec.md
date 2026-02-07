## ADDED Requirements

### Requirement: Condition Trace Logging
The system SHALL support optional tracing of ability condition evaluation to capture intermediate scalar values and emit a formatted trace string for diagnostics.

#### Scenario: Condition fails with trace enabled
- **WHEN** an ability condition evaluates to false and trace logging is enabled
- **THEN** the log entry includes the DSL expression and a trace of intermediate values (e.g., `player.Hand.Count→(2) > 3`).

### Requirement: Trace Logging Toggle
The devtools UI SHALL provide a toggle that enables or disables condition trace logging for the current session.

#### Scenario: Toggle off
- **WHEN** the toggle is off
- **THEN** condition evaluation runs without trace collection and no trace output is logged.

### Requirement: Non-Scalar Value Handling
The trace collector SHALL only materialize scalar values (string, number, bool, enum) and MUST summarize other values by type and optional count to prevent heavy serialization.

#### Scenario: Collection encountered
- **WHEN** a traced expression resolves to a collection
- **THEN** the trace output shows the type name and count without enumerating items.

### Requirement: Performance Guard
The system SHALL avoid double-evaluating conditions to produce trace output.

#### Scenario: Trace on
- **WHEN** trace logging is enabled
- **THEN** condition evaluation is performed once and trace data is gathered during that evaluation.
