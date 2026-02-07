## MODIFIED Requirements
### Requirement: Ability Target Selection
The ability editor SHALL treat Target selection as card instance selection only and SHALL NOT expose player selection in the Target UI.

#### Scenario: Configure card-targeted effect
- **WHEN** an author selects card targets in the ability editor
- **THEN** the target list contains only card instances

## ADDED Requirements
### Requirement: PlayerScope for Player Effects
Player-oriented effects SHALL expose a PlayerScope parameter with options Self, Opponent, and Both to determine which player(s) are affected.

#### Scenario: Default player scope
- **WHEN** a player-oriented effect has no PlayerScope configured
- **THEN** the system defaults to Self

#### Scenario: Apply opponent scope
- **WHEN** PlayerScope is Opponent for a player-oriented effect
- **THEN** the effect resolves targets to the opposing player

### Requirement: Effect Target Usage Descriptions
Effect metadata SHALL describe whether card targets are used or ignored for that effect.

#### Scenario: Effect ignores card targets
- **WHEN** an effect does not use card targets
- **THEN** its metadata description states that targets are ignored