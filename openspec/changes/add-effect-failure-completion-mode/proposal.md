# Change: Add effect failure completion mode

## Why
Some abilities need to continue their chain even when a preceding effect fails, while still exposing the failure to downstream logic.

## What Changes
- Add a new FailureHandling option that allows EffectCompletedEvent to be emitted on failure and chained abilities to execute.
- Include a failure flag in EffectCompletedEvent outputs for downstream DSL checks.

## Impact
- Affected specs: effect-execution
- Affected code: FailureHandling enum, EffectExecutor, GameActionTransactionService, localization
