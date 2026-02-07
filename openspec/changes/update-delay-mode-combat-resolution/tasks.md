## 1. Implementation
- [ ] Add DelayMode to delayed-event contracts and update existing delayed events with ParentEffectsComplete.
- [ ] Extend the transaction processing loop with a queue-drained delayed-event queue.
- [ ] Introduce CombatResolutionEvent (QueueDrained) and combat damage commit rules.
- [ ] Update attack request flow to enqueue attack/clash events plus CombatResolutionEvent without immediate damage.
- [ ] Adjust clash ability ordering to prioritize attacker before defender.

## 2. Tests
- [ ] Add scenario coverage for combat ending before damage when pre-combat effects remove a participant.
- [ ] Add scenario coverage for combat damage when both participants remain after pre-combat effects.

## 3. Validation
- [ ] Run targeted server tests for combat scenarios.

