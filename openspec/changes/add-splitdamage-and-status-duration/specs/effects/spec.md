## ADDED Requirements
### Requirement: Split damage distribution mode
The system MUST support a split damage mode that distributes a total damage amount across multiple follower targets in summon order and then applies remaining damage based on the target set.

#### Scenario: Split damage distributes by field order
- **WHEN** a split damage effect targets multiple followers
- **THEN** damage is allocated from earlier summoned followers to later ones, up to each follower's current health at allocation time
- **AND** if remaining damage exists after the last follower, it is applied to the leader if the leader is among the targets; otherwise to the last follower
- **AND** the allocation order uses the current field order (summon order), which does not change when a follower transforms

### Requirement: Temporary status duration for ModifyStats
The system MUST allow ModifyStats (SetStatus) to specify a duration mode for temporary effects.

#### Scenario: Temporary cost reduction expires at end of turn
- **WHEN** ModifyStats applies a cost modifier with duration set to end of turn
- **THEN** the modifier expires at the end of the current turn

### Requirement: Reset attack times effect
The system MUST provide an effect that resets a card's attacks-this-turn state and emits a corresponding event.

#### Scenario: Reset attack allows attacking again
- **WHEN** the reset-attack effect is applied to a follower that has already attacked
- **THEN** the follower's attacks-this-turn count is reset and it can attack again if other conditions allow
