## ADDED Requirements
### Requirement: Ability Counter Registration
The system SHALL allow ability configs to declare counter registrations with a name and initial value.

#### Scenario: Card creation initializes counters
- **WHEN** a card instance is created and its ability configs declare counters
- **THEN** counters that do not already exist SHALL be initialized to the declared initial values

#### Scenario: Buff-granted abilities initialize counters
- **WHEN** a buff grants abilities that declare counters
- **THEN** counters missing on the target card SHALL be initialized to the declared values

### Requirement: Counter Registration Conflict Handling
The system SHALL skip initialization when a counter name already exists on the card.

#### Scenario: Existing counter is preserved
- **WHEN** a counter with the same name already exists on the card
- **THEN** the registration SHALL be ignored and the existing value preserved
