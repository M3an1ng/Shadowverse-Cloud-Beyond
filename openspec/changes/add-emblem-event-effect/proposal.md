# Change: Add Emblem Placement Event and Effect

## Why
Emblem (crest) cards currently have a dedicated zone but no first-class event/effect to add them. Designers need a reliable, validated way to grant emblems through abilities.

## What Changes
- Add `EventType.AddEmblem` and an `AddEmblemEvent` that commits cards into the Emblem zone.
- Add `EffectType.AddEmblem` with parameters mirroring AddDeck, plus a duplicate-check toggle.
- Enforce crest-only validation when placing cards into the Emblem zone.
- Provide a PlayerState helper to move cards into the Emblem zone consistently.

## Impact
- Affected specs: effect-add-emblem
- Affected code: EventType enum, effect type enum, event/effect classes, PlayerState, localization, tests.
- **BREAKING**: None (new effect/event only).
