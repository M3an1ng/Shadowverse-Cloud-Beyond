# Change: Update Effect Target Pools and Event Payload

## Why
EffectCompletedEvent currently duplicates effect data while omitting the full generated event list, and target-pool propagation relies on a special `previous` key that does not preserve full pool structure.

## What Changes
- EffectCompletedEvent includes the generated event list and no longer carries GeneratedCards.
- GameContext exposes resolved ability target pools via `resolvedTargets` for DSL access.
- Remove the `previous` target key injection during target resolution.

## Impact
- Affected specs: ability-targets, effect-events
- Affected code: `Server/Domain/Abilities/Effects/EffectExecutor.cs`, `Server/Domain/Events/SignalEvents.cs`, `Server/Domain/Events/GameContext.cs`, `Server/Domain/Abilities/Targets/TargetSelectionService.cs`
- **BREAKING**: EffectCompletedEvent payload shape changes (GeneratedCards removed).