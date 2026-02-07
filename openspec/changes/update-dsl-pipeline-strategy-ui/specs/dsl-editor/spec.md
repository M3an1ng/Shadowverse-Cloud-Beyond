## ADDED Requirements
### Requirement: Pipeline Strategy Controls
The DSL pipeline editor SHALL allow configuring orderBy direction and take strategy/count without manual DSL edits.

#### Scenario: Configure orderBy direction
- **WHEN** the user edits an orderBy pipeline stage
- **THEN** the UI shows an asc/desc selector and applies it to the generated expression

#### Scenario: Configure take strategy and count
- **WHEN** the user edits a take pipeline stage
- **THEN** the UI shows a strategy selector (first/last/random/all) and a numeric count input when applicable

#### Scenario: Simplified take count input
- **WHEN** the user provides a take count value
- **THEN** the UI accepts a numeric input without exposing self-context DSL nodes

#### Scenario: Preserve existing strategy
- **WHEN** an expression containing orderBy direction or take strategy is loaded
- **THEN** the pipeline editor reflects the stored strategy selections
