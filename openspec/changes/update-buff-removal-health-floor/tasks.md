## 1. Regression Tests

- [x] 1.1 Add a buff-removal test where a follower with temporary health support and an existing health debuff survives at 1 health after the support buff is removed.
- [x] 1.2 Add a stat-change test proving a newly applied health-reduction buff still destroys a follower when projected health is 0 or below.
- [x] 1.3 Add a test proving a follower preserved by the buff-removal health floor can still be destroyed by lethal damage.
- [x] 1.4 Add a test proving a later positive raw max health clears the buff-removal health floor.
- [x] 1.5 Add reset/lifecycle coverage for clearing the floor when the card leaves field, transforms, or resets state.

## 2. Health Floor State

- [x] 2.1 Add explicit `CardInstance` state for the buff-removal health floor without allowing negative `Damage`.
- [x] 2.2 Ensure card reset, leave-field, transform, and relevant copy paths clear or intentionally exclude the floor state.
- [x] 2.3 Add snapshot/DTO handling only if existing card state projections need to expose the floored max/current health.

## 3. Raw Versus Effective Health Calculation

- [x] 3.1 Extend buff-derived state to retain raw max health before applying the buff-removal floor.
- [x] 3.2 Add an internal way to query raw effective max health for buff-removal and stat-change logic.
- [x] 3.3 Apply the floor only to follower effective max health when the explicit floor state is active.
- [x] 3.4 Clear the floor when raw max health becomes positive.

## 4. Buff Removal Behavior

- [x] 4.1 Replace the current destroy-on-health-buff-removal behavior in `BuffRemovedEvent`.
- [x] 4.2 When a health buff removal leaves follower raw max health <= 0, set the floor to 1 and keep current health at 1.
- [x] 4.3 Preserve existing current-health clamp behavior when raw max health remains positive.
- [x] 4.4 Keep leaders outside the floor behavior.

## 5. Lethal Stat And Damage Behavior

- [x] 5.1 Ensure `StatChangedEvent` does not create the floor for newly applied health-reduction buffs.
- [x] 5.2 Ensure lethal health-reduction buffs clear or bypass any existing buff-removal floor before resolving destruction.
- [x] 5.3 Ensure normal damage lethal resolution is unaffected by the floor.

## 6. Verification

- [x] 6.1 Run targeted server tests for buff removal, stat changes, damage lethal, card reset, transform, and copy behavior.
- [x] 6.2 Run the relevant broader test filter for combat/event regression coverage.
- [x] 6.3 Review generated event sequences to confirm buff removal does not emit destroy solely due to raw max health <= 0.
