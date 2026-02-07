## 1. Implementation
- [x] 1.1 Add AuraConfig (or AddAuraEffectParams) to AbilityConfig and JSON loader.
- [x] 1.2 Extend DSL AST/parser to support refresh trigger blocks and aura definitions.
- [x] 1.3 Introduce IAuraManager/AuraManager service with event-type indexing.
- [x] 1.4 Implement aura registration from AddAura effect and unregistration on source removal.
- [x] 1.5 Implement refresh evaluation: trigger check, condition evaluation, target resolution, target diffing.
- [x] 1.6 Apply/remove buffs using BuffService with deterministic EffectKey and stacking rules.
- [x] 1.7 Integrate AuraManager refresh pass into GameActionTransactionService event pipeline (Before/On/After).
- [x] 1.8 Add loop-prevention and refresh-cycle guards.
- [x] 1.9 Add DTO/telemetry updates if needed for buff visibility.

## 2. Tests
- [x] 2.1 Aura refresh on CardMoved for entering/leaving field.
- [x] 2.2 Aura refresh on Transform for type change.
- [x] 2.3 Aura removal when source leaves field or is silenced.
- [x] 2.4 Stacking behavior when multiple auras apply to same target.
- [x] 2.5 Refresh guard prevents infinite loops from buff events.
