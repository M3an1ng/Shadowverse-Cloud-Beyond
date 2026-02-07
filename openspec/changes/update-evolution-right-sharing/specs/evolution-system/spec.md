## ADDED Requirements
### Requirement: Evolution and super evolve share a per-turn evolution right
The system MUST treat manual evolve and manual super evolve as consuming the same per-turn evolution right. After one action is performed, the player MUST NOT be able to perform the other action until the next turn.

#### Scenario: Evolve consumes the shared evolution right
- **GIVEN** a player has an unevolved follower, evolution is unlocked, and both EP and SEP are available
- **WHEN** the player performs a manual evolve action
- **THEN** the player cannot perform a manual super evolve action for the rest of the turn

#### Scenario: Super evolve consumes the shared evolution right
- **GIVEN** a player has an unevolved follower, super evolve is unlocked, and both EP and SEP are available
- **WHEN** the player performs a manual super evolve action
- **THEN** the player cannot perform a manual evolve action for the rest of the turn
