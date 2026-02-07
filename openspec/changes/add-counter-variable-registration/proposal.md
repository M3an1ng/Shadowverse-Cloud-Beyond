# Change: Add counter variable registration

## Why
Ability configs can now declare variables with initial values, but counters are not initialized automatically. We need a consistent backend initialization path so DSL access and gameplay logic see expected values.

## What Changes
- Add a counter registration list to ability configs (name + initial value).
- Initialize counters when card instances are created and when buffs grant new abilities.
- Skip initialization if the counter already exists (system counters are pre-initialized).

## Impact
- Specs: ability-variable-registration
- Code: Server/Domain/Abilities/AbilityConfig.cs, Server/Domain/Factories/CardFactory.cs, Server/Domain/Buffs/BuffManager.cs, Server/Domain/Entities/CardCounters.cs, ability editor payloads
