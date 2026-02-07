## ADDED Requirements
### Requirement: Act cost SHALL be computed at card-level and support buff modifiers
Act cost MUST be computed using the card's Act abilities and MUST incorporate any buff-based keyword modifiers applied to the card.

#### Scenario: Buff modifies Act cost
- **GIVEN** a card with an Act ability whose base cost is 3
- **AND** a buff with an Act keyword modifier of -1 is applied to the card
- **WHEN** the player requests Act
- **THEN** the system MUST use Act cost 2

#### Scenario: Act cost defaults to zero
- **GIVEN** a card with an Act ability without an explicit Act cost value
- **WHEN** the player requests Act
- **THEN** the system MUST treat Act cost as 0
