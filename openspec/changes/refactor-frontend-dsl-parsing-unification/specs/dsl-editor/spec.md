## ADDED Requirements
### Requirement: Unified frontend DSL parsing
The frontend SHALL use a single, stable parsing and type inference rule set for editor rendering, validation, and local parsing so the same DSL expression resolves to the same type everywhere.

#### Scenario: Consistent type resolution across editor and validation
- **WHEN** a DSL expression is used in the editor and in validation with the same trigger context
- **THEN** the inferred return type SHALL match across both paths

#### Scenario: Triggering event fields are preserved
- **WHEN** a DSL expression accesses `context.triggeringEvent` fields
- **THEN** the field type SHALL resolve using event metadata rather than defaulting to Object
