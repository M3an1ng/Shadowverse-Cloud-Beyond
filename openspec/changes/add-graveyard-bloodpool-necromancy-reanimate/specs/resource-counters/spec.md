## ADDED Requirements
### Requirement: Player resource counters
The system SHALL track a graveyard counter and a blood pool counter per player.

#### Scenario: Graveyard counter increments
- **WHEN** a card enters the Graveyard zone from any other zone
- **THEN** the owning player's graveyard counter increases by 1
- **AND** cards moved to Removed/Banish do not increase the counter

#### Scenario: Blood pool increments on leader damage
- **WHEN** the leader takes damage
- **THEN** the owning player's blood pool increases by the actual damage amount

### Requirement: Necromancy and Sacrifice consumption effects
The system SHALL provide Necromancy and Sacrifice effects that consume the corresponding counters and respect OnFailure handling.

#### Scenario: Necromancy consumes graveyard counter
- **WHEN** Necromancy is executed with amount N and the player has at least N graveyard count
- **THEN** the graveyard counter decreases by N and the effect succeeds

#### Scenario: Sacrifice fails when insufficient
- **WHEN** Sacrifice is executed with amount N and the player has less than N blood pool
- **THEN** the effect fails and OnFailure behavior applies

### Requirement: Counter access in DSL
The system SHALL expose graveyard count and blood pool for DSL conditions and formulas.

#### Scenario: DSL reads graveyard count
- **WHEN** a condition evaluates owner.GraveyardCount
- **THEN** the current graveyard counter value is returned
