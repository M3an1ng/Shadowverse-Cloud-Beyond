# Change: Add EffectCompletedEvent Generated Card Snapshots

## Why
Chained abilities reading EffectCompletedEvent.GeneratedCards can observe mutated card state after zone moves or resets. We need immutable snapshots of the generated cards at effect-completion time.

## What Changes
- Add a snapshot field to EffectCompletedEvent that captures generated card properties at creation time.
- Populate snapshots during EffectExecutor when EffectCompletedEvent is built.
- Expose the snapshot field to DSL/event parameter access.

## Impact
- Affected specs: effect-completion (new delta)
- Affected code: Server/Domain/Events/SignalEvents.cs, Server/Domain/Abilities/Effects/EffectExecutor.cs, mapping/serialization where EffectCompletedEvent is emitted
- Behavior changes: chained abilities can read stable card data even after state resets
