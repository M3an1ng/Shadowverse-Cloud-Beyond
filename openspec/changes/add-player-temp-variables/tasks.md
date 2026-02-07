## 1. Implementation
- [x] 1.1 Add player counters container, public accessors, and pre-registered keys (combo/graveyard/bloodpool).
- [x] 1.2 Bridge existing fields (Combo/GraveyardCount/BloodPool) to counters without breaking DTOs/tests.
- [x] 1.3 Update event/handler paths that mutate combo/graveyard/bloodpool to use counters.
- [x] 1.4 Add ModifyVariable effect type, params, and handler (support add/set/consume).
- [x] 1.5 Emit appropriate update events (at least GraveyardUpdatedEvent) when affected counters change.
- [ ] 1.6 Update DSL docs/metadata if needed for new player variable access.
- [ ] 1.7 Add/adjust tests for player counters and ModifyVariable effect.
