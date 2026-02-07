## ADDED Requirements
### Requirement: TriggeringEvent parameters are accessible via MemberAccess
The DSL MUST allow access to event parameters using MemberAccess on `context.triggeringEvent`, e.g., `context.triggeringEvent.TargetCard`.

#### Scenario: Accessing a card parameter
- **WHEN** the DSL references `context.triggeringEvent.TargetCard`
- **THEN** the evaluator returns the event's TargetCard parameter value

### Requirement: MemberAccess chains resolve parameter types
The DSL evaluator MUST return typed values for event parameters so that chained access is supported (e.g., `context.triggeringEvent.TargetCard.Type`).

#### Scenario: Chained access to a card field
- **WHEN** the DSL references `context.triggeringEvent.TargetCard.Type`
- **THEN** the evaluator resolves `TargetCard` as a CardInstance and returns its `Type`

### Requirement: Editor exposes event parameters as properties in TriggeringEvent context
When editing within the `context.triggeringEvent` path, the UI MUST list event parameters as property options and emit MemberAccess expressions instead of `event*` helper functions.

#### Scenario: TriggeringEvent property list uses event metadata
- **WHEN** the user enters `context.triggeringEvent` in the editor for a Summon trigger
- **THEN** the property list shows Summon parameters (with correct types) and selecting one produces `context.triggeringEvent.<Param>`

### Requirement: Backward compatibility for event* helpers
Existing DSL expressions that use `eventCard/eventInt/eventString/eventBool/eventGuid` MUST continue to evaluate correctly.

#### Scenario: Legacy helper evaluation
- **WHEN** the DSL references `eventCard("TargetCard")`
- **THEN** the evaluator returns the same value as `context.triggeringEvent.TargetCard`
