## 1. Implementation
- [x] 1.1 Add ability keyword modifier config and buff effect types.
- [x] 1.2 Extend AuraManager and BuffFactory to build/clone the new effect.
- [x] 1.3 Add BuffService API to compute effective ability keyword values.
- [x] 1.4 Add CardInstance.GetActCost and use it in ActRequestedEvent.
- [x] 1.5 Update call sites and keep behavior aligned (no silence in current system).

## 2. Tests
- [ ] 2.1 Act cost uses ability keyword value / ActivateCost fallback.
- [ ] 2.2 Act cost applies ability-keyword buff modifiers (additive/set).
