## ADDED Requirements
### Requirement: Chained Abilities Entry Point
The effect editor SHALL expose a chained abilities editor from the effect accordion header.

#### Scenario: Open chained abilities editor
- **WHEN** the user clicks the chained abilities button on an effect header
- **THEN** a chained abilities editor for that effect is shown

### Requirement: Manage Chained Abilities
The effect editor SHALL allow users to add, edit, and delete chained abilities attached to the effect.

#### Scenario: Add and remove chained abilities
- **WHEN** the user creates or deletes a chained ability
- **THEN** the effect's ChainedAbilities list updates accordingly

### Requirement: Locked Trigger for New Chained Abilities
New chained abilities created from the effect editor SHALL use EffectCompletedEvent as the trigger and the trigger fields SHALL be locked in the UI.

#### Scenario: Create chained ability
- **WHEN** the user creates a chained ability from the effect editor
- **THEN** the trigger is set to EffectCompletedEvent and cannot be modified

### Requirement: Chained Ability Ordering
The chained abilities editor SHALL support drag-and-drop ordering and display a sequence index for each chained ability.

#### Scenario: Reorder chained abilities
- **WHEN** the user drags a chained ability to a new position
- **THEN** the displayed sequence indices and saved order reflect the new arrangement
