## MODIFIED Requirements
### Requirement: Reanimate effect
The system SHALL provide a Reanimate effect that summons a destroyed follower with cost <= the specified value, weighted by destroy history.

#### Scenario: Reanimate selects a valid destroyed follower
- **WHEN** Reanimate is executed with cost limit N
- **THEN** a destroyed follower with cost <= N is chosen and summoned
- **AND** destroyed followers are determined by graveyard cards whose LastMoveReason is Destroyed (or an overridden reason)

#### Scenario: Reanimate weighting
- **WHEN** multiple destroyed followers share the maximum eligible cost
- **THEN** followers with more copies in the graveyard have higher selection probability

### Requirement: Reanimate adds tribe marker
Reanimated followers SHALL gain a standard tribe marker indicating they are reanimated.

#### Scenario: Tribe marker applied
- **WHEN** a follower is reanimated
- **THEN** the follower's tribe list includes the reanimate marker