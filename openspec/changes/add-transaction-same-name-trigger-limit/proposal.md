# Change: Add same-name trigger limit per transaction

## Why
Current trigger limits only support per-turn and per-game counters for a single ability instance. Some mechanics require an additional transactional guard: when multiple same-name abilities are simultaneously eligible, only one should trigger within a single transaction.

## What Changes
- Add an optional trigger configuration flag (proposed: `LimitSameNameOncePerTransaction`) to enable same-name trigger limiting within a single transaction.
- Define transaction-scoped same-name identity and filtering behavior for triggered ability collection.
- Keep backward compatibility by defaulting the new option to disabled.

## Impact
- Impacted specs: ability-triggers
- Impacted code: `Server/Domain/Abilities/Triggers/TriggerConfig.cs`, `Server/Services/GameActionTransactionService.cs`, `Server/Domain/Abilities/AbilityManager.cs`, related trigger-processing tests
