# Change: Conditional Removal for Buff DurationType

## Why
DurationType.ConditionalRemoval exists but is not implemented. Designers need buffs to expire when a specified RemovalCondition becomes true.

## What Changes
- Evaluate RemovalCondition for buffs with DurationType.ConditionalRemoval and remove the buff once the condition becomes true.
- Validate that RemovalCondition is provided for ConditionalRemoval buffs; invalid buffs are rejected with a clear error.
- Evaluate conditions using the existing IConditionEvaluator with the current GameContext (including TriggeringEvent when applicable).

## Impact
- Affected specs: buff-duration
- Affected code: BuffService, GameActionTransactionService (or event pipeline), Buff validation, tests.