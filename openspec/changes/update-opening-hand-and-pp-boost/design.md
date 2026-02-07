## Context
The current rules give the second player an extra opening draw to offset going second. The new design removes that bonus draw and replaces it with a PP+1 boost ability that can be used twice, with a reset at the start of the second player\'s turn 6.

## Goals / Non-Goals
- Goals: equalize opening hand size, support mulligan for both players, implement second-player PP+1 boost with a turn-6 reset, allow temporary PP overflow.
- Non-Goals: redesign turn phases, change PP growth per turn, or rework existing evolve/super-evolve rules.

## Decisions
- Decision: Model PP+1 as a single-use action available only to the second player; there is one use from match start until the start of the second player\'s turn 6, then availability resets to one use for the rest of the match (total uses capped at 2).
- Decision: PP+1 can only be activated during the second player\'s own turn.
- Decision: PP+1 increases Current PP by 1 even if it exceeds Max PP; Max PP is unchanged and normal turn-start refresh will clamp Current PP back to Max PP.
- Decision: Both players draw 4 at match start, and the second player receives no bonus draw.

## Risks / Trade-offs
- Allowing Current PP to exceed Max PP requires careful handling in cost checks and UI display.
- Resetting PP+1 availability at a fixed turn adds state complexity that must be represented in DTOs and logs.

## Migration Plan
1. Add fields for PP+1 availability and reset tracking to player state and DTOs.
2. Update match start draw and mulligan logic.
3. Implement PP+1 action/event flow and overflow PP handling.
4. Add UI controls and tests.
