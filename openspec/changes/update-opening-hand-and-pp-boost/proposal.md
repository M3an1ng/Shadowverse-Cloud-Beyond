# Change: Update opening hand and second-player PP boost

## Why
The current match start flow gives the second player an extra draw. We need to replace that with a PP+1 boost mechanic while keeping the mulligan flow consistent for both players.

## What Changes
- Deal 4 cards to both players at match start; both players mulligan from those 4 cards. **BREAKING**
- Remove the second-player bonus draw. **BREAKING**
- Add a second-player PP+1 boost ability with 2 total uses: one available from match start until the start of the second player\'s turn 6, and one available from turn 6 onward (reset even if the first was unused). **BREAKING**
- Allow PP+1 boost to temporarily exceed max PP by 1 for the turn without changing max PP.

## Impact
- Affected specs: match-start, pp-boost
- Affected code: game start/mulligan flow, player resource model, turn-start logic, action requests/events, UI/DTOs, AI, tests.
