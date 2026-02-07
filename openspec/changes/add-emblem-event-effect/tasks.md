## 1. Implementation
- [x] 1.1 Add `EventType.AddEmblem` and `AddEmblemEvent` with metadata + parameters
- [x] 1.2 Add `EffectType.AddEmblem` and `AddEmblemEffectParams` (PlayerScope, CardSourceMode, CardDefinitionId, Amount, duplicate-check toggle)
- [x] 1.3 Implement `AddEmblemEffectHandler` with crest-only validation and duplicate-check logic
- [x] 1.4 Add `PlayerState.AddCardsToEmblem` helper and move logic
- [x] 1.5 Extend `CardType` enum for crest (if missing) and localization labels
- [x] 1.6 Add tests covering definition/instance modes, crest-only validation, and duplicate-check behavior
