# Change: Owner-Aware Turn Start/End Duration Types

## Why
Current DurationType values (StartOfTurn/EndOfTurn) are global and cannot distinguish between the buff owner's turn and the opponent's turn. We need four explicit owner-relative variants.

## What Changes
- Add four DurationType values: OwnerTurnStart, OwnerTurnEnd, OpponentTurnStart, OpponentTurnEnd (names TBD).
- Define owner-relative evaluation: owner/opponent is determined by the buff target card's OwnerPlayerId.
- Update expiry collection logic to respect these new types.

## Impact
- Affected specs: buff-duration
- Affected code: AbilityEnums.DurationType, BuffService expiry collection, tests, UI labels.
