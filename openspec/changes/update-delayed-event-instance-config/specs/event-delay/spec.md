## ADDED Requirements
### Requirement: Instance-Scoped Event Delay Configuration
The system SHALL allow delayed event behavior to be configured per event instance rather than per event class.

#### Scenario: Default delay disabled
- **WHEN** an event instance does not specify delay configuration
- **THEN** the event is processed immediately in normal queue order

#### Scenario: Parent-effects delay configured
- **WHEN** an event instance specifies DelayMode=ParentEffectsComplete with a parent event id
- **THEN** the event is processed only after the parent event's effects have been drained

#### Scenario: Queue-drained delay configured
- **WHEN** an event instance specifies DelayMode=QueueDrained
- **THEN** the event is processed only after all event/effect queues are drained