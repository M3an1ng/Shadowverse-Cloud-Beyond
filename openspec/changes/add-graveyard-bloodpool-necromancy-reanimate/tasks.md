## 1. Implementation
- [x] 1.1 Add player counters for graveyard count and blood pool (state + DTO).
- [x] 1.2 Increment graveyard count when a card moves into Graveyard; exclude Removed/Banish paths.
- [x] 1.3 Increment blood pool by actual leader damage amount.
- [x] 1.4 Add Necromancy and Sacrifice effects (params + handlers) that consume counters and emit failure for OnFailure.
- [x] 1.5 Add destroyed-follower registry (definition id + cost + count) for reanimate weighting.
- [x] 1.6 Add Reanimate effect (params + handler) that selects a follower <= cost and summons it with the reanimate tribe marker.
- [x] 1.7 Expose counters in DSL (e.g., owner.GraveyardCount, owner.BloodPool).
- [x] 1.8 Add tests for counter increments, cost consumption, and reanimate selection weighting.
