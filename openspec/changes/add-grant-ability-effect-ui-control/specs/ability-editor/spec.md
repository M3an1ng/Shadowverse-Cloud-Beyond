## ADDED Requirements
### Requirement: Grant Ability List Editor
The effect editor SHALL render a structured list editor for the Grant Ability parameter instead of a freeform JSON text area.

#### Scenario: Open Grant Ability parameters
- **WHEN** the user opens a Grant Ability effect
- **THEN** a list-based ability editor control is shown
- **AND** the JSON textarea is not shown

### Requirement: Manage Granted Abilities
The Grant Ability list editor SHALL allow users to add, edit, and remove ability configurations and persist them in the effect parameters.

#### Scenario: Add and remove ability configs
- **WHEN** the user adds or deletes an ability entry
- **THEN** the effect parameters update to match the current list

### Requirement: Ability Summary Display
Each ability entry in the Grant Ability list editor SHALL display a concise summary showing only the trigger name.

#### Scenario: Render summaries
- **WHEN** the list editor renders ability entries
- **THEN** each entry shows its name and the trigger name summary

### Requirement: Persist Structured Ability Configs
The Grant Ability list editor SHALL store the ability list as structured AbilityConfig objects rather than a JSON string.

#### Scenario: Save and reload
- **WHEN** the user saves an effect containing granted abilities
- **THEN** the stored parameters contain an array of ability configs
- **AND** reopening the effect restores the same list entries

### Requirement: Reorder Granted Abilities
The Grant Ability list editor SHALL allow users to reorder ability entries.

#### Scenario: Reorder list entries
- **WHEN** the user moves an ability entry up or down
- **THEN** the saved list order matches the UI order

### Requirement: Copy Existing Abilities Into the List
The Grant Ability list editor SHALL allow users to copy an existing ability from the ability library, reusing the card editor "Copy Existing" flow.

#### Scenario: Copy existing ability
- **WHEN** the user selects an existing ability to copy
- **THEN** the copied ability is added as a new list entry
