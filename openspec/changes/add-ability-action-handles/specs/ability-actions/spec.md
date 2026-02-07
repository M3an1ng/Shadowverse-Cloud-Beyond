## ADDED Requirements
### Requirement: Ability action handles for card operation buttons
The system SHALL expose per-ability action handles for card operation buttons (Act/Fusion/Enhance/etc.) so the client can select a specific ability to execute.

#### Scenario: Multiple Act abilities
- **GIVEN** a card on the field with two Act abilities
- **WHEN** the client requests Act using one action handle
- **THEN** only the selected Act ability MUST execute

#### Scenario: Fusion/Enhance actions are selectable
- **GIVEN** a card that exposes Fusion or Enhance operations via multiple abilities
- **WHEN** the client requests the action using a specific handle
- **THEN** the server MUST execute that ability and validate its cost/targets

### Requirement: Action handle validation and expiry
The system SHALL validate that an action handle maps to the current card/player context and MUST reject invalid or expired handles without mutating state.

#### Scenario: Invalid action handle
- **GIVEN** an action handle that is unknown or expired
- **WHEN** the client submits it
- **THEN** the server MUST reject the request without applying costs or effects

### Requirement: Committed events carry triggering ability identity
Committed action events SHALL include the triggering ability identity so AbilityManager executes only that ability.

#### Scenario: Act event filters to chosen ability
- **GIVEN** an ActInvokedEvent with a TriggeredAbilityRuntimeId
- **WHEN** AbilityManager resolves triggered abilities
- **THEN** only the matching ability MUST be executed
