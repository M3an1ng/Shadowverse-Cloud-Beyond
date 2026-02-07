# Change: Replace Destroyed History With Move Reasons

## Why
The current reanimate implementation relies on a DestroyedFollowerRecord registry that is opaque to DSL and hard to adjust by abilities. We want a uniform, DSL-friendly history model that uses graveyard cards plus explicit move reasons, and allows special abilities to reclassify history when needed.

## What Changes
- Introduce a CardMoveReason enum and add LastMoveReason tracking on CardInstance (including Transform).
- Extend CardMovedEvent and card move APIs to carry a move reason and apply it after reset.
- Update reanimate selection to use graveyard + move reasons instead of DestroyedFollowerRecord.
- Remove DestroyedFollowerRecord storage and related wiring.

## Impact
- Affected specs: card-move-reason, reanimate
- Affected code: CardMovedEvent, PlayerState MoveCard flow, CardInstance reset ordering, reanimate effect handler, tests
