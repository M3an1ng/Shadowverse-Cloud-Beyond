# Change: Add EffectCompletedEvent.GeneratedCards

## Why
Downstream DSL/UI needs to identify newly created card instances during an effect execution without counting existing instances.

## What Changes
- Add EffectCompletedEvent.GeneratedCards parameter that lists newly created CardInstance objects during the effect.
- Populate GeneratedCards in EffectExecutor by detecting newly registered card instances during handler execution.

## Impact
- Affected specs: effect-completion
- Affected code: Server/Domain/Events/SignalEvents.cs, Server/Domain/Abilities/Effects/EffectExecutor.cs, Server/Domain/Entities/GameState.cs