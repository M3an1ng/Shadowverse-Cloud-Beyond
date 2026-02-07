## 1. Implementation
- [x] 1.1 Add CanStack to buff-producing effect params (ModifyStats, SetKeyword, GrantAbility, SetStats) and update DSL defaults.
- [x] 1.2 Ensure EffectKey is generated for all ability-sourced buffs (sourceAbilityId:effectIndex).
- [x] 1.3 Apply CanStack gating during effect handling to skip adding buffs when a matching EffectKey exists.
- [x] 1.4 Remove EffectKey-based replacement logic from BuffStackingRules (effect-stage only).
- [ ] 1.5 Update tests or scenario coverage for CanStack behavior and system buffs without keys.
