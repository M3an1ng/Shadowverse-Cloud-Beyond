# Change: Add trigger count limits

## Why
Ability triggers currently lack a built-in, declarative way to cap how many times they can fire per turn or across a match, which makes common card designs verbose and inconsistent.

## What Changes
- Add an optional trigger count limit configuration to TriggerConfig with per-turn and per-game caps (scoped per ability instance; 0/negative means unlimited).
- Enforce the configured caps when evaluating whether a trigger can fire.
- Update ability configuration schema and DSL/JSON handling as needed.

## Impact
- Impacted specs: ability-triggers (new)
- Impacted code: Server/Domain/Abilities/Triggers/TriggerConfig.cs, Server/Domain/Abilities/Runtime/Trigger.cs, Server/Domain/Abilities/Ability.cs, ability config JSON/DSL parsing, tests
