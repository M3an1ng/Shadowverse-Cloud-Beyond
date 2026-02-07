# Change: Add delay modes for combat resolution

## Why
The current delayed-event model only supports delaying until a specific parent event's direct effects finish. Combat needs a stronger guarantee: all queued attack/clash effects (including chained effects like Lastword) must resolve before combat damage is applied.

## What Changes
- Introduce a delay mode concept for delayed events, including a queue-drained mode.
- Add a CombatResolutionEvent that defers combat damage until the event and effect queues are fully drained.
- Ensure attack declarations still consume attack counts even if combat terminates early.

## Impact
- Affected capability: combat resolution and event processing.
- Affected code: event processing loop, delayed event handling, attack request flow, combat damage generation.
- Risk: ordering regressions if delay modes are misapplied; requires targeted tests.

