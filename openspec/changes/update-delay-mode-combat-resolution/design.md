# Design: Delay modes and combat resolution

## Summary
Add a delay mode to delayed events so some events can wait until the entire event/effect queues are drained. Use this mode for combat damage resolution so attack/clash effects (including chained effects) complete before damage is applied.

## Delay modes
- ParentEffectsComplete (existing behavior): delay only until the parent event's direct effects finish.
- QueueDrained (new): delay until event queue and effect queue are both empty.

## QueueDrained handling
- Maintain a separate FIFO queue for QueueDrained delayed events.
- When both event queue and effect queue are empty (and no parent-delayed events remain), release QueueDrained events into the event queue.
- This preserves existing delay behavior for all current delayed events.

## Combat resolution flow
1. Attack request validates and consumes attack count.
2. Attack and clash trigger events are enqueued (attack-time then clash-time effects).
3. CombatResolutionEvent (QueueDrained) is enqueued.
4. After all attack/clash effects resolve (including chained effects), CombatResolutionEvent commits:
   - If either participant is no longer on the field, combat ends without damage.
   - Otherwise, generate combat damage events for both sides.

## Ordering expectations
- Attack-time effects execute before clash-time effects because AttackEvent is processed before ClashEvent.
- Clash-time effects are ordered so the attacker resolves before the defender (source-card priority).

## Compatibility
- Existing delayed events keep ParentEffectsComplete, avoiding unintended global delays.
- CombatResolutionEvent is the only QueueDrained event in this change.

