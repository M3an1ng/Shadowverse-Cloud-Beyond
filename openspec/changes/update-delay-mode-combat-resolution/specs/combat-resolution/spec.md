## ADDED Requirements

### Requirement: Delay modes for delayed events
The event processing system SHALL support multiple delay modes so that delayed events can wait until either a parent event's direct effects complete or the entire event/effect queues are drained.

#### Scenario: Queue-drained delay releases only after all queues are empty
- **GIVEN** a delayed event configured with QueueDrained mode
- **WHEN** the event and effect queues still contain work
- **THEN** the delayed event is not executed
- **AND** it executes only after both queues are empty

### Requirement: Combat resolution after pre-combat effects
The combat system SHALL resolve attack-time and clash-time effects fully before applying combat damage.

#### Scenario: Pre-combat effects end combat
- **GIVEN** an attacker and defender enter combat
- **AND** a pre-combat effect removes a participant from the field
- **WHEN** the combat resolution occurs
- **THEN** no combat damage is applied
- **AND** the attack count remains consumed

#### Scenario: Pre-combat effects do not end combat
- **GIVEN** an attacker and defender enter combat
- **AND** both remain on the field after pre-combat effects resolve
- **WHEN** the combat resolution occurs
- **THEN** combat damage is applied to both sides

