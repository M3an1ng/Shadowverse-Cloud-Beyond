# Change: Add play modes for Enhance and mark super evolve on EvolveEvent

## Why
We need a deterministic way to resolve Enhance as a play-mode selection and to distinguish normal evolve from super evolve when abilities trigger on evolve.

## What Changes
- Introduce play modes for card play (Base, Enhance), with mode availability/selection computed during playability checks and used during play.
- Add an IsSuperEvolution flag to EvolveEvent so evolve-triggered abilities can branch on super evolve.
- Expose play mode availability and the selected mode to the frontend.

## Impact
- Affected specs: card-play-modes, evolution-events.
- Affected code: PlayCardActionDto, PlayCardEvent, PlayabilityResultDto, GameService playability checks, EvolveEvent, PlayerState.SuperEvolveCard, DSL metadata exposure.
