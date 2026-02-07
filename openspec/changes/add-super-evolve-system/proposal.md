# Change: Add Super Evolve System and Update Evolution Timing

## Why
SuperEvolve events exist but are not part of the core game flow. The evolution timing and points need to align with the new super evolve rules.

## What Changes
- Update evolution unlock timing and points: first player turn 5, second player turn 4; grant 2 evolve points each (previous second player had 3). **BREAKING**
- Add super evolve unlock timing and super evolve points: first player turn 7, second player turn 6; grant 2 super evolve points each.
- Define super evolve resolution: counts as an evolution, applies +3/+3 and attack-ready, adds own-turn damage/ability-destroy immunity, and deals 1 leader damage when its attack destroys an enemy follower (blow-away is visual only).

## Impact
- Affected specs: evolution-system
- Affected code: Server/Domain game flow, event pipeline, state and DTOs; client UI and display of evolve/super evolve resources; tests.
