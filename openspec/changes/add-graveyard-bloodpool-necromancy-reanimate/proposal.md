# Change: Add graveyard/bloodpool counters and reanimate

## Why
Abysscraft mechanics require persistent resource counters and a reanimate effect to convert card data into runnable abilities.

## What Changes
- Add player counters for graveyard count and blood pool.
- Track graveyard count on card moves into Graveyard (excluding banish/removed).
- Track blood pool on leader damage (increase by actual damage).
- Add Necromancy and Sacrifice cost effects that consume the corresponding counters and respect OnFailure handling.
- Add Reanimate effect + handler to summon a destroyed follower with cost <= N, weighted by destroy history.
- Add data/DSL access to the counters for ability conditions.

## Impact
- Affected specs: resource-counters, reanimate
- Affected code: PlayerState counters, card move/damage events, effect handlers, DSL value access, tests
