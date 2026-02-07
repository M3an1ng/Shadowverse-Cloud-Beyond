# Change: Update ability tags and cost keywords

## Why
Abilities need multiple keywords and cost-bearing tags to express compound mechanics (e.g., Enhance + Necromancy) without overloading trigger semantics.

## What Changes
- Replace single AbilityTag with a list of ability keywords.
- Ability keywords no longer drive triggering; TriggerConfig remains the only trigger source.
- Allow certain keywords to carry numeric parameters (Enhance, Act, Necromancy, Sacrifice).
- Update serialization, editor metadata, and runtime queries to read the new structure.

## Impact
- Affected specs: ability-tags
- Affected code: AbilityConfig, ability editor UI, DSL metadata, serialization, tests
