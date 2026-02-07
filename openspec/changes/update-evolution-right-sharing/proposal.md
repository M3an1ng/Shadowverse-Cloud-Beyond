# Change: Share evolution right between evolve and super evolve

## Why
Currently, a player can evolve and then super evolve in the same turn because the rights are tracked separately. This breaks the intended one-evolution-per-turn rule.

## What Changes
- Evolve and super evolve actions consume the same per-turn evolution right. After either action, the other action is no longer available for the rest of the turn.

## Impact
- Affected specs: evolution-system
- Affected code: Server/Domain/Events/ActionRequests (EvolveRequestedEvent, SuperEvolveRequestedEvent), state updates, tests.
