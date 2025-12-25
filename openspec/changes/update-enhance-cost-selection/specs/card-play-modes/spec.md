## ADDED Requirements
### Requirement: Enhance Cost Selection
The system SHALL allow multiple Enhance abilities on a card to declare different EnhanceCost values.

#### Scenario: Highest affordable EnhanceCost is selected
- **WHEN** a card has multiple EnhanceCost values and the player has enough PP for more than one
- **THEN** the selected Enhance cost is the highest EnhanceCost that does not exceed current PP

#### Scenario: Enhance unavailable when all costs exceed PP
- **WHEN** all EnhanceCost values exceed the player's current PP
- **THEN** Enhance mode is unavailable

### Requirement: Enhance Priority
The system SHALL prioritize Enhance mode over Base play when Enhance is available.

#### Scenario: Enhance selected even if Base cost is higher
- **WHEN** Enhance mode is available
- **THEN** the default selected play mode is Enhance, even if the Base cost is higher than the Enhance cost
