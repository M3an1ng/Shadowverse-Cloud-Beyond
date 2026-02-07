# Change: Add Condition Trace Logging and Devtools Toggle

## Why
Diagnosing ability condition failures currently requires re-evaluating expressions and still lacks clear intermediate values. We need single-pass trace logging that can be enabled on demand, plus a devtools toggle to control it.

## What Changes
- Add optional trace collection during DSL condition evaluation to capture intermediate scalar values in a single pass.
- Log formatted condition traces on failure when trace logging is enabled.
- Add a devtools UI toggle to enable/disable condition trace logging for the current session.

## Impact
- Affected specs: condition-trace-logging
- Affected code: Server/Domain/Abilities/Conditions, Server/Domain/Abilities/DSL/Evaluation, Server/Domain/Abilities/Ability.cs, devtools log viewer UI components
