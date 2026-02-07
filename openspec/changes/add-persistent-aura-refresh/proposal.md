# Change: Add Refreshable Persistent Auras via DSL

## Why
Designers need persistent, target-based effects that refresh on events (e.g., cards entering play or transforming) while reusing the existing ability/trigger pipeline and DSL.

## What Changes
- Add a DSL-backed aura configuration that can register a persistent effect set and refresh targets on configured events.
- Add an AuraManager runtime service to track auras, refresh target sets, and apply/remove buffs via the existing buff system.
- Integrate aura refresh evaluation into the same event processing pipeline used for triggered abilities.
- Add guardrails for refresh loops, target selection restrictions, and stacking/identity rules via EffectKey.

## Impact
- Specs: persistent-auras
- Code: AbilityConfig, DSL parser/AST, AbilityLoader/JSON converters, GameActionTransactionService, AbilityManager indexing, BuffService, new AuraManager service and interfaces.
