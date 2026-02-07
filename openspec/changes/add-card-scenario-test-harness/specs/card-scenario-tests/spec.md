## ADDED Requirements

### Requirement: Data-driven card scenarios
The system SHALL support data-driven scenario definitions for card behavior tests, including initial state, actions, and expected outcomes.

#### Scenario: Execute a scenario
- **WHEN** a scenario is executed
- **THEN** the runner initializes state, performs actions, and evaluates expectations.

### Requirement: Deterministic choice resolution
The system SHALL provide deterministic resolution for player choices and random selection during scenario tests.

#### Scenario: Resolve a choice deterministically
- **WHEN** a scenario specifies a selection strategy
- **THEN** the runner applies the strategy consistently across runs.

### Requirement: Card data integration
The system SHALL load abilities from card_data_*.json and attach them to card instances in scenario tests.

#### Scenario: Use card data in a scenario
- **WHEN** a scenario references a card id
- **THEN** the test uses the corresponding ability configurations from card data.
