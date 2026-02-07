## 1. Implementation
- [x] 1.1 Add AuraConfig to AbilityConfig (JSON loader + DSL mapping if applicable).
- [x] 1.2 Define BuffTemplate for AuraConfig (stat/keyword/grant ability only).
- [x] 1.3 Implement Ability->AuraConfig runtime mapping (default inherit Targets/Conditions/Properties; allow overrides).
- [x] 1.4 Update AuraManager to materialize BuffTemplate directly (no EffectConfig).
- [x] 1.5 Enforce safe buff template fields during mapping.
- [x] 1.6 Use Conditions as aura-level on/off gate.
- [x] 1.7 Update documentation examples.

## 2. Tests
- [x] 2.1 AuraConfig mapping uses Ability Targets/Conditions by default.
- [x] 2.2 AuraConfig overrides Ability Targets/Conditions when specified.
- [x] 2.3 AuraConfig produces buffs and refresh removal works.
- [x] 2.4 Aura Conditions gate overall enable/disable.
