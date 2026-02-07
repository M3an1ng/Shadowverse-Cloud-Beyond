# Change: Update Ability Target Selection and PlayerScope Parameters

## Why
Ability target selection currently mixes player selection with card instance selection, which confuses authors and makes common effects harder to configure. We want targets to mean "card instances" only and let player-oriented effects declare their player scope explicitly.

## What Changes
- Ability target selection UI only allows selecting card instances; no player selection is exposed.
- Player-oriented effects add a PlayerScope parameter (Self/Opponent/Both) and derive player targets from this scope.
- Effect metadata descriptions clarify when card targets are used and when they are ignored.
- Default PlayerScope is Self when omitted to preserve behavior.

## Impact
- Affected specs: effect-targeting
- Affected code: effect parameter classes, player-targeted effect handlers, ability editor metadata/UI, DSL runtime target resolution.
- **BREAKING**: ability configs that relied on explicit player targets must migrate to PlayerScope (default Self is applied when missing).