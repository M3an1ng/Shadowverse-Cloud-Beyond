## ADDED Requirements

### Requirement: Event Metadata MUST Provide Player-Facing Category And Visibility
Ability editor event metadata MUST include `CategoryId`, `CategoryLabel`, and `Visibility` for each event entry.

#### Scenario: Category and visibility are present
- **WHEN** the ability editor metadata is requested
- **THEN** each event metadata entry MUST include `CategoryId` and `CategoryLabel`
- **AND** each entry MUST include `Visibility` with a default of `Visible` when not explicitly set

### Requirement: ActionRequest Events MUST Be Marked As System Or Hidden
ActionRequest events MUST be tagged with `Visibility = System` or `Visibility = Hidden` so they are not shown by default in the editor.

#### Scenario: System events are hidden by default
- **GIVEN** an ActionRequest event is marked with `Visibility = System`
- **WHEN** the editor renders the event list with default settings
- **THEN** the ActionRequest event MUST NOT be shown

### Requirement: Effect Metadata MUST Provide Player-Facing Category
Ability editor effect metadata MUST include `CategoryId` and `CategoryLabel` for each effect entry.

#### Scenario: Effect category is present
- **WHEN** the ability editor metadata is requested
- **THEN** each effect metadata entry MUST include `CategoryId` and `CategoryLabel`

### Requirement: Ability Editor MUST Group Events And Effects By Category
The editor MUST group events and effects by `CategoryLabel` and allow system events to be shown only via an explicit toggle.

#### Scenario: Grouped lists with system toggle
- **GIVEN** metadata entries include category and visibility fields
- **WHEN** the user opens the event or effect selector
- **THEN** items MUST be grouped by `CategoryLabel`
- **AND** system/hidden items MUST be excluded unless the user enables a “show system events” option

### Requirement: CategoryId MUST Use The Player-Facing Set
`CategoryId` MUST be one of: `base-action`, `position-change`, `temp-state`, `resource-value`, `turn-flow`, `special`, `system-request`, or `uncategorized`.

#### Scenario: Unknown category defaults to uncategorized
- **GIVEN** an event or effect has no explicit category
- **WHEN** metadata is generated
- **THEN** the entry MUST use `CategoryId = uncategorized` and `CategoryLabel = 未分类`
