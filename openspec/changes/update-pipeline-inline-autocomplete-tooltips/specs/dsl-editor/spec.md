## MODIFIED Requirements
### Requirement: Pipeline Stage Editing Uses Node-Based DSL
The system SHALL provide pipeline stage editing through a lightweight formula input and SHALL surface node-based recommendations via tooltip suggestions, including operators and event fields.

#### Scenario: Tooltip suggestions while editing a pipeline stage
- **WHEN** the user types in a pipeline stage formula input
- **THEN** the system shows tooltip suggestions derived from the node recommendation logic
- **AND** selecting a suggestion inserts the corresponding DSL token into the input
- **AND** operator and event field suggestions appear when applicable
