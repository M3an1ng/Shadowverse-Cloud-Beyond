# Change: Update Enhance Cost Selection and Priority

## Why
Enhance abilities may declare different EnhanceCost values. The system needs a deterministic rule
to select the applicable Enhance cost based on current PP and to prioritize Enhance over Base play.

## What Changes
- Enhance availability is determined by per-ability EnhanceCost values, allowing mixed costs.
- Enhance mode uses the highest affordable EnhanceCost for the current PP.
- Enhance mode is preferred over Base whenever available, even if Base cost is higher.

## Impact
- Affected specs: card-play-modes
- Affected code: play mode resolver, playability checks, play action validation
