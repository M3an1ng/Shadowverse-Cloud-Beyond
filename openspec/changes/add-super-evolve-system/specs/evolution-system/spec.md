## ADDED Requirements
### Requirement: Evolution unlock timing and evolve points
The system MUST unlock evolve at the start of turn 5 for the first player and at the start of turn 4 for the second player. On unlock, each player MUST receive 2 evolve points.

#### Scenario: First player evolve unlock
- **WHEN** the first player's turn 5 starts
- **THEN** evolve is unlocked for the first player and they have 2 evolve points

#### Scenario: Second player evolve unlock
- **WHEN** the second player's turn 4 starts
- **THEN** evolve is unlocked for the second player and they have 2 evolve points

### Requirement: Super evolve unlock timing and points
The system MUST unlock super evolve at the start of turn 7 for the first player and at the start of turn 6 for the second player. On unlock, each player MUST receive 2 super evolve points.

#### Scenario: First player super evolve unlock
- **WHEN** the first player's turn 7 starts
- **THEN** super evolve is unlocked for the first player and they have 2 super evolve points

#### Scenario: Second player super evolve unlock
- **WHEN** the second player's turn 6 starts
- **THEN** super evolve is unlocked for the second player and they have 2 super evolve points

### Requirement: Super evolve counts as evolution
When a follower is super evolved, the system MUST treat it as an evolution for evolve-triggered effects and counters, and MUST consume 1 super evolve point.

#### Scenario: Super evolve triggers evolve effects
- **WHEN** a follower is super evolved
- **THEN** evolve-triggered effects resolve, evolve counters increment, and 1 super evolve point is consumed

### Requirement: Super evolve buff and protection
A super evolved follower MUST gain +3/+3, can attack the same turn, and during its controller's turn it MUST NOT take damage or be destroyed by abilities.

#### Scenario: Own-turn protection from abilities
- **WHEN** a super evolved follower would take damage or be destroyed by an ability during its controller's turn
- **THEN** the damage is prevented and the follower is not destroyed

### Requirement: Super evolve attack blow-away and leader damage
If an enemy follower is destroyed by an attack from a super evolved follower, the enemy leader MUST take 1 damage and the blow-away effect MUST be shown as a visual only. The destroyed follower MUST still be destroyed by the normal rules.

#### Scenario: Destroyed by super evolve attack
- **WHEN** a super evolved follower destroys an enemy follower by attack
- **THEN** the enemy leader takes 1 damage and the destroyed follower follows the normal destruction flow
