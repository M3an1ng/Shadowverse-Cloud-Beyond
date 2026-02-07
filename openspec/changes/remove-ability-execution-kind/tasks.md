## 1. Implementation
- [x] 1.1 Remove `AbilityExecutionKind` enum and `AbilityConfig.ExecutionKind` field.
- [x] 1.2 Update runtime execution branching to use `AbilityType`.
- [x] 1.3 Update chained ability and aura-only checks to use `AbilityType`.
- [x] 1.4 Update JSON/serialization assumptions to tolerate legacy `executionKind` fields.
- [ ] 1.5 Add/update tests covering execution branching and legacy JSON.
