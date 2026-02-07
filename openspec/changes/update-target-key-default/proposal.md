# Change: Unify Single Target Key As target1

## Why
Single-target configurations currently map to a special key (default), which diverges from multi-target naming and complicates UI/DSL targeting.

## What Changes
- Use `target1` as the key for single-target configurations parsed from arrays.
- Keep multi-target key naming unchanged (`target1`, `target2`, ...).

## Impact
- Affected specs: ability-targets
- Affected code: `Server/Domain/Abilities/JsonConverters/SingleOrDictionaryConverter.cs`