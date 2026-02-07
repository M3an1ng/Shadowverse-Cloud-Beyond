## ADDED Requirements
### Requirement: Separate Countdown Value
The system SHALL store countdown values independently from health in card definitions and runtime instances.

#### Scenario: Initialize countdown from definition
- **WHEN** a card with the Countdown keyword is instantiated
- **THEN** its current countdown is set from CardDefinition.Countdown
- **AND** Health is not used to derive countdown

### Requirement: Explicit Countdown Usage
The system SHALL treat Countdown as optional and SHALL NOT infer countdown from Health.

#### Scenario: Missing countdown
- **WHEN** a Countdown card definition omits Countdown
- **THEN** no countdown is initialized and countdown effects do not apply

### Requirement: Countdown Tick Compatibility
Countdown ticking SHALL continue to decrement the current countdown for eligible cards on the field after the schema split.

#### Scenario: Countdown tick for amulet
- **WHEN** a CountdownTickEvent is applied to an amulet with countdown
- **THEN** its current countdown decreases by the event delta
