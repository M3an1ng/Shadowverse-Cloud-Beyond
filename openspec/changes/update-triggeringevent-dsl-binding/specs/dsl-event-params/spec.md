## ADDED Requirements
### Requirement: TriggeringEvent parameters are exposed only in the TriggeringEvent context
The editor MUST show TriggeringEvent parameters only when the user enters the TriggeringEvent context via `context.triggeringEvent`. These parameters MUST come from event metadata and be filtered by the selected event type.

#### Scenario: Summon event shows only Summon parameters
- **WHEN** the user selects Summon as the trigger event and navigates into `context.triggeringEvent`
- **THEN** the editor shows only the Summon parameter list

### Requirement: GameEventBase base fields are not exposed
The editor MUST NOT expose GameEventBase base fields (EventType/EventId/Timestamp/Source/Target) as selectable parameters. TriggeringEvent parameter lists MUST be defined by explicit event parameters only.

#### Scenario: Base fields are absent in the parameter list
- **WHEN** the user opens the TriggeringEvent parameter list
- **THEN** the list does not include EventType/EventId/Timestamp/Source/Target

### Requirement: TriggeringEvent parameters are not global menu items
The editor MUST NOT list TriggeringEvent parameters as global data source options. Parameters are only accessible through the TriggeringEvent context entry.

#### Scenario: Global data source menu does not show event parameters
- **WHEN** the user edits DSL in a normal data source menu
- **THEN** TriggeringEvent parameters are not listed
