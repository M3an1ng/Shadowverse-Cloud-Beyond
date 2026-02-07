# Change: Add grant ability, set stats, and effect output targets

## Why
Forestcraft conversion requires dynamic ability grants and chained abilities need access to cards created by the previous effect.

## What Changes
- Add a GrantAbility effect that attaches ability configs via the Buff system.
- Add a SetStats effect that sets attack/health/cost using StatChangedEvent with set-layer modifiers.
- Extend EffectCompletedEvent to expose generated card instances for chained targeting.
- Update Forestcraft crest same-name summon to query graveyard crests by name.

## Impact
- Affected specs: `effects`
- Affected code: effect types/handlers, BuffService integration, EffectExecutor/EffectCompletedEvent, forestcraft card data
