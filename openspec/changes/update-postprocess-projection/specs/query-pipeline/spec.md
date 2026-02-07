## MODIFIED Requirements

### Requirement: Post-processing stage projects item property values
The system SHALL allow a query pipeline post-processing stage to project each item in the result set to a value derived from the current item (self).

#### Scenario: Project CardInstance property list
- **GIVEN** a result list of CardInstance items
- **AND** a post-processing projection expression `self.CurrentCost`
- **WHEN** the pipeline executes
- **THEN** the result SHALL be a list of Int32 values corresponding to each item's CurrentCost

### Requirement: Select stage projects items
The system SHALL provide a select pipeline stage that projects each item in a collection to a value derived from the current item (self).

#### Scenario: Select CardInstance property list
- **GIVEN** a result list of CardInstance items
- **AND** a select projection expression `self.CurrentCost`
- **WHEN** the pipeline executes
- **THEN** the result SHALL be a list of Int32 values corresponding to each item's CurrentCost

### Requirement: Legacy DTO post-processing is removed
The system SHALL no longer support post-processing types `ToCardDefinitionDto` or `ToCardInstanceDto`.

#### Scenario: Legacy post-processing rejected
- **GIVEN** a query pipeline containing a legacy DTO post-processing type
- **WHEN** the pipeline executes or validates
- **THEN** the system SHALL report that the configuration is unsupported
