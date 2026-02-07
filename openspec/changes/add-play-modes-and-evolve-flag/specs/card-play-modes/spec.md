## ADDED Requirements
### Requirement: Play Mode Availability
The system SHALL compute a list of play modes for a card at playability check time.

#### Scenario: Enhance available
- **WHEN** the player can afford the Enhance cost and no mode restrictions apply
- **THEN** the Enhance mode is listed as available and has higher priority than the Base mode

#### Scenario: Enhance disabled
- **WHEN** an effect disables Enhance for the card
- **THEN** the Enhance mode is not available and cannot be auto-selected

### Requirement: Play Mode Selection
The system SHALL determine a default play mode using the highest-priority available mode.

#### Scenario: Default selection
- **WHEN** multiple modes are available
- **THEN** the default selection is the highest-priority available mode

### Requirement: Frontend Visibility
The system SHALL expose play mode availability and the selected mode in playability results.

#### Scenario: UI receives mode list
- **WHEN** playability results are sent to the client
- **THEN** the payload includes the available modes, their costs, and the selected mode
