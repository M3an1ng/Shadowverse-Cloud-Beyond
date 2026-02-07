# Change: Add Instance Mode for Card Source Effects

## Why
Card creation effects currently only support creating new cards from definitions. Authors need a way to move existing card instances (hand/deck/graveyard) into the destination zone without adding new effects.

## What Changes
- Summon, GetToken, and AddDeck add a CardSourceMode parameter (Definition or Instance).
- Instance mode uses selected card targets as the source cards and always preserves the original owner for destination placement.
- Definition mode behavior stays unchanged and ignores card targets.

## Impact
- Affected specs: effect-card-source
- Affected code: effect parameters, effect handlers, ability editor metadata, localization.
- **BREAKING**: None (default CardSourceMode is Definition).
