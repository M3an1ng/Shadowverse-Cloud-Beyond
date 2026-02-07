## 1. Implementation
- [x] 1.1 Add direct AbilityId flow: extend action DTOs to carry AbilityRuntimeId (Act/Fusion/Enhance).
- [x] 1.2 Update client button UI to pass AbilityRuntimeId for each ability button.
- [x] 1.3 Update request validation to resolve AbilityRuntimeId to ability and enforce ownership/turn/zone/cost.
- [x] 1.4 Carry TriggeredAbilityRuntimeId in committed events and filter AbilityManager execution to the chosen ability.
- [x] 1.5 Preserve legacy behavior when no AbilityRuntimeId is provided (fallback to card-level execution).

## 2. Follow-up: ActionId Handles (Future Improvement)
- [ ] 2.1 Define AbilityAction/ActionHandle DTOs for button-producing abilities (Act/Fusion/Enhance/etc.).
- [ ] 2.2 Add server-side ActionRegistry mapping ActionId -> AbilityRuntimeId (+ card, player, cost, refresh cycle).
- [ ] 2.3 Update playability/operation menu generation to emit per-ability actions with ActionId.
- [ ] 2.4 Update client request payloads to submit ActionId (not just card id).
- [ ] 2.5 Update request validation to resolve ActionId to ability and enforce ownership/turn/zone/cost.
- [ ] 2.6 Add feature flag to switch AbilityRuntimeId vs ActionId (or phase-out direct ids).

## 3. Tests
- [ ] 3.1 Multiple Act abilities produce multiple ability-id actions and only the selected one executes.
- [ ] 3.2 Fusion/Enhance ability-id actions validate cost/targets for the selected ability.
- [ ] 3.3 Legacy card-level Act still works when AbilityRuntimeId is omitted.
- [ ] 3.4 Invalid/expired ActionId is rejected without state mutation (future).
