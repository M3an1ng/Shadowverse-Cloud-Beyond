## Context

`CardInstance.CurrentHealth` is calculated as `MaxHealth - Damage`, and `Damage` is treated throughout the domain as accumulated non-negative damage. Existing buff removal logic captures pre-removal current health, unregisters the buff, invalidates derived state, and then adjusts `Damage` so current health is clamped to the new max health.

A previous fix path can destroy a follower when removing a health buff leaves effective max health at 0 or below. The requested behavior is different: health buff removal should preserve the follower at 1 health, but applying a new health-reduction buff that makes a follower 0 health or less should still destroy it normally.

## Goals / Non-Goals

**Goals:**
- Preserve followers at 1 health only when health-modifying buff removal leaves raw max health <= 0.
- Keep normal lethal behavior for new health-reduction buffs, damage, and other non-removal stat changes.
- Preserve non-negative `Damage` semantics.
- Make the rule testable by separating raw max health from max health after the buff-removal floor.

**Non-Goals:**
- Do not introduce a general "cannot die from max health reduction" mechanic.
- Do not apply the floor to leaders.
- Do not use negative `Damage` to represent current health above max health.
- Do not change public API or database schema.

## Decisions

### Decision: Model the floor explicitly on card state

Add explicit battlefield state to `CardInstance`, such as `HealthFloorFromBuffRemoval` or `MinimumHealthFromBuffRemoval`, with values `0` or `1`.

Rationale:
- `CurrentHealth = MaxHealth - Damage` cannot represent `1 / 0` without negative `Damage`.
- Negative `Damage` breaks healing and other logic that treats `Damage` as accumulated injury.
- A named state makes the special rule auditable and resettable.

Alternative considered: set `Damage = -1` when max health is 0. This is rejected because it weakens a core invariant used by healing, copying, snapshots, and UI mapping.

### Decision: Separate raw max health from effective max health

Extend derived health calculation so the system can observe both:
- `RawMaxHealth`: base health plus active stat buffs/debuffs, before the floor.
- `MaxHealth`: gameplay max health after applying the buff-removal floor.

Rationale:
- Buff removal must know when raw max health is <= 0 to enable the floor.
- Later stat-change lethal checks must be able to ignore or clear the floor so new `-X/-X` effects can still kill.

Implementation can expose this as a dedicated internal method such as `GetRawEffectiveMaxHealth(card, context)` or by extending the derived state cache entry with `RawMaxHealth`.

### Decision: Apply the floor only from `BuffRemovedEvent`

`BuffRemovedEvent` should be the only event that creates the health floor. After removing a health-modifying buff and invalidating derived state:
- compute raw max health;
- if target is a follower and raw max health <= 0, set the floor to 1, invalidate derived state again, and set `Damage` so current health is 1 without becoming negative;
- otherwise keep the existing current-health clamp behavior.

`StatChangedEvent` should not create the floor. If a health-reducing buff is applied and its projected health is <= 0, the existing lethal path should generate a destroy leave-field event.

### Decision: Clear the floor when it stops being relevant

Clear `HealthFloorFromBuffRemoval` when:
- raw max health becomes positive;
- the follower receives a further health-reducing stat change that would be lethal;
- the card leaves field, transforms, or resets to a new card state.

Rationale:
- The floor is a correction for a specific removal resolution, not persistent protection.
- Resetting it at identity/zone boundaries prevents hidden state from leaking across card lifecycle transitions.

## Risks / Trade-offs

- Raw/effective health split increases derived-state complexity -> keep the API internal and cover it with focused tests.
- Clearing the floor in every lifecycle path can be missed -> add tests for leave-field/reset/transform paths that already reset buffs and damage.
- Existing tests may assume max health never differs from raw buff calculation -> update only tests that cover the new buff-removal floor behavior.
- Applying the floor to batched buff removals could be duplicated -> evaluate the final raw max health after each committed removal and keep the floor idempotent at value 1.

## Migration Plan

1. Add failing tests for buff-removal preservation, lethal new health-reduction buff, damage after floor, positive raw max clearing, and reset behavior.
2. Add explicit floor state to `CardInstance` and ensure reset/copy/transform paths handle it.
3. Extend buff-derived health calculation to expose raw and floored max health.
4. Replace the current destroy-on-health-buff-removal behavior in `BuffRemovedEvent` with the floor behavior.
5. Run targeted server tests around buff removal, stat changes, damage lethal, card reset, transform, and copy.

## Open Questions

- Copy behavior should default to not preserving the floor unless existing copy semantics require copying all battlefield state.
- Batch removal ordering should be validated against current event processing to ensure the final visible result is one floor state and no destroy event.
