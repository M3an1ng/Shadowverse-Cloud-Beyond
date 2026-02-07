## 1. Implementation
- [x] 1.1 Add Ability execution branch for Aura (ExecutionKind or Aura marker on AbilityConfig).
- [x] 1.2 Move Aura trigger/condition/target resolution into AbilityManager pipeline.
- [x] 1.3 Introduce AuraExecutor (or slim AuraManager) that applies/removes buffs by diffing ActiveTargets.
- [x] 1.4 Ensure aura refresh uses unified trigger matching logic (TriggerConfig static match).
- [x] 1.5 Preserve refresh-safe guard to avoid recursive refresh loops.

## 2. Tests
- [x] 2.1 Aura refresh is driven by AbilityManager trigger pipeline.
- [x] 2.2 Aura reuse of Ability targets/conditions produces correct buff diffs.
- [x] 2.3 Aura removal on condition false or source removal works through unified pipeline.
- [x] 2.4 Refresh recursion guard still prevents infinite loops.
