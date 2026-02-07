# Change: Cap Current Health After Health Buff Removal

## Why
When a health-modifying buff is removed, current health can drop below the pre-removal value because damage remains unchanged. Designers want current health to be clamped to the lower of the previous current health and the new max health, preserving the unit's current value when possible.

## What Changes
- On buff removal, if the removed buff modifies max health, adjust the target's damage so current health becomes min(previous current, new max).
- Apply this adjustment for followers/leaders that have health.

## Impact
- Affected specs: buff-removal
- Affected code: BuffRemovedEvent, tests.