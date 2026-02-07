# Change: Add before-event intercept effects (backlog)

## Why
Some mechanics must rewrite or replace events before commit so the original event never reaches On/After (e.g., card-configured leave-field intercepts should replace Destroy/Return/Tuck with Vanish, and damage modifiers should rewrite Damage). The intercept pipeline must be safe for any event type, not limited to a fixed allowlist.

## What Changes
- Expand the before-event intercept pipeline to operate on any event type safely.
- Provide a generic parameter rewrite mechanism that only touches rewritable event parameters.
- Generalize event replacement to support leave-field conversions (Destroy/Return/Tuck) into Vanish.
- Replace the temporary LeaveVanish keyword/system ability with a card-configured Before ability that executes ReplaceEvent.

## Impact
- Affected specs: event-intercept
- Affected code: GameActionTransactionService (intercept pipeline), event rewrite/replace logic, ability configuration/data, removal of LeaveVanish keyword/system ability, PlayerState.MoveCard.
