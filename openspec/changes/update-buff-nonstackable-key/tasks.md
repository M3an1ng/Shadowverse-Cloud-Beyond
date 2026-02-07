## 1. Implementation
- [x] 1.1 Add EffectIndex to Effect and set it in Ability.CreateEffectsAsync.
- [x] 1.2 Add NonStackableKey to Buff and write it when effects are marked non-stackable.
- [x] 1.3 Remove IsNonStackable from Buff entities/DTOs (flag lives only in effect params).
- [x] 1.4 Update BuffStackingEngine to replace buffs by NonStackableKey.
- [x] 1.5 Drop BuffDefinition.IsNonStackable usage from code paths.
- [x] 1.6 Add/adjust tests covering non-stackable behavior across multiple effects in the same ability.
