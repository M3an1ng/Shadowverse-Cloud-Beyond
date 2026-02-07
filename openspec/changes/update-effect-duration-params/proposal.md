# Change: Add Duration Fields to Buff-Producing Effects

## Why
Buff-producing effects like ModifyStatsEffect and SetKeywordEffect do not expose duration settings consistently, which makes it hard to create time-limited buffs via the effect system.

## What Changes
- Add DurationType and DurationParameter to effect parameter definitions for buff-producing effects (e.g., ModifyStatsEffect, SetKeywordEffect, GrantAbilityEffect).
- When DurationType is StartOfTurn or EndOfTurn, use DurationParameter as the turn-count parameter (existing duration behavior is preserved when parameter is missing or <= 0).
- Ensure effect handlers pass duration fields through when creating buffs.

## Impact
- Affected specs: ability-effects
- Affected code: effect parameter classes and handlers for buff-producing effects; editor UI metadata.