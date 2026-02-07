# Change: Refactor Target Resolution Timing

## Why
Target resolution currently happens during effect queuing, which can miss targets created by earlier effects in the same transaction.

## What Changes
- Default target resolution for non-IPlayerActionEvent abilities is deferred to effect execution time.
- Primary abilities triggered by IPlayerActionEvent keep pre-resolution to preserve cancellable player choices.
- Pre-resolved targets remain available to effects that rely on them.

## Impact
- Affected specs: ability-target-resolution
- Affected code: Server/Services/GameActionTransactionService.cs, Server/Domain/Abilities/AbilityManager.cs, Server/Domain/Abilities/Targets/TargetSelectionService.cs, Server/Domain/Abilities/Effects/EffectExecutor.cs, data/card_data.json
