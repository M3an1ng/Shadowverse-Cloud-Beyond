## 1. Implementation
- [x] 1.1 Add RemoveAbility effect type, params, handler, and metadata (RemoveMode=ID/Keyword/ALL).
- [x] 1.2 Add AbilitySuppressionBuffEffect and encode suppression rules (DefinitionId list, AbilityKeyword, ALL).
- [x] 1.3 Implement AbilityManager suppression filtering (IsAbilitySuppressed, GetEffectiveAbilitiesForCard).
- [x] 1.4 Wire suppression filtering into trigger flow, Act/Fusion requests, playability checks, and UI summaries.
- [x] 1.5 Implement aura cleanup on suppression and aura restoration on suppression removal.
- [x] 1.6 Update effect editor UI/localization for new params.
- [x] 1.7 Add tests for ID/Keyword/ALL suppression and aura removal/restore.
