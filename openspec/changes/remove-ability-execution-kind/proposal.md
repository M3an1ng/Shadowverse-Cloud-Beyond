# Change: Remove AbilityExecutionKind

## Why
- `executionKind` in ability JSON can be empty, causing deserialization failures.
- The execution branch can be inferred from `AbilityType`, simplifying config and runtime logic.

## What Changes
- Remove `AbilityExecutionKind` and the `executionKind` config field.
- Derive execution behavior from `AbilityType` (`Triggered`/`Activated` => effects, `Continuous` => auras).
- Ignore any `executionKind` values present in legacy JSON data.

## Impact
- Affected code: ability config/loader, ability manager, transaction service.
- Affected data: legacy JSON configs that include `executionKind` (now ignored).
