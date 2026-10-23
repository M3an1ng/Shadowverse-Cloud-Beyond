## Why

Health buff removal currently cannot express the desired Shadowverse-style edge case where a follower survives after temporary health support expires, while active max-health reduction effects should still be able to kill followers when applied. The game needs a rule that preserves a follower at 1 health only for buff-removal resolution, without weakening normal `-X/-X` lethal behavior.

## What Changes

- Replace the current "destroy follower when health buff removal leaves max health <= 0" behavior.
- When a health-modifying buff is removed from a follower and the follower's raw max health after removal is <= 0, keep the follower alive at 1 current health for that removal resolution.
- Ensure this health floor applies only to buff removal, not to newly applied health-reduction buffs or other stat changes.
- Ensure subsequent lethal damage or later health-reduction effects can still destroy the follower normally.
- Keep `Damage` non-negative; do not represent the floor by assigning negative damage.

## Capabilities

### New Capabilities
- `buff-removal`: Defines health preservation behavior when health-modifying buffs are removed.

### Modified Capabilities

## Impact

- Affected domain logic: `BuffRemovedEvent`, `BuffService` derived health calculation, `CardInstance` battlefield state/reset behavior.
- Affected event behavior: health buff removal should no longer directly emit a destroy/leave-field event solely because raw max health falls to zero or below.
- Affected tests: buff removal health clamping tests, lethal stat-change tests, damage-after-floor tests, zone-reset/copy/transform reset tests.
- No API or database schema changes expected.
