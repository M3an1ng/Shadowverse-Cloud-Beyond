## ADDED Requirements
### Requirement: Action Restriction Buffs
The system SHALL support action restriction buffs that include an action type, optional play mode,
condition DSL, denial reason, and duration metadata.

#### Scenario: Conditional attack restriction
- **WHEN** a card has an attack restriction buff whose condition evaluates to true
- **THEN** the card cannot declare attacks and the restriction reason is available for UI feedback

### Requirement: Central Permission Evaluation
The system SHALL evaluate action restrictions through a centralized permission service during
playability checks and action request validation.

#### Scenario: Request validation blocks restricted action
- **WHEN** a player submits an action that is currently restricted by an active buff
- **THEN** the request is rejected with the restriction reason

#### Scenario: Playability surfaces restriction
- **WHEN** playability is computed for a card with an active restriction
- **THEN** the playability result indicates the action is unavailable and includes the reason
