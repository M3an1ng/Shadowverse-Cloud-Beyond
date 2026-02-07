# Change: Parameterized End/Start-of-Turn Buff Expiry

## Why
DurationParameter is stored but currently unused for buff expiry. Designers need a way to specify that a buff should expire after N occurrences of EndOfTurn or StartOfTurn, rather than always expiring on the first occurrence.

## What Changes
- Interpret DurationParameter as a positive integer N for DurationType.EndOfTurn and DurationType.StartOfTurn, expiring on the Nth matching phase (counted globally per phase).
- Normalize missing or non-positive DurationParameter to 1 to preserve existing behavior.
- **BREAKING**: Change DurationParameter type from object? to int?/int across buff models, DTOs, and effect parameters.
- Update expiry processing to count phase occurrences per buff and remove when the threshold is reached.

## Impact
- Affected specs: buff-duration
- Affected code: BuffService, Buff, IBuffFactory/BuffFactory, BuffAppliedEventDto, GrantAbilityEffectParams, and buff parsing/serialization.
