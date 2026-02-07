## ADDED Requirements
### Requirement: Single AST Source of Truth
The DSL editor SHALL maintain exactly one mutable AST for the active expression, and all edit/render/save flows SHALL read from that AST.

#### Scenario: Save after unnest
- **WHEN** the user removes nesting in the workspace and saves
- **THEN** the main expression reflects the workspace AST without reintroducing removed nesting

#### Scenario: Main edit entry uses the same AST
- **WHEN** the user enters main expression edit mode
- **THEN** the editor uses the same AST instance as the workspace view

### Requirement: Derived `astRoot` View
The editor SHALL treat `astRoot` as a derived or aliased view of the workspace AST and SHALL NOT mutate it independently.

#### Scenario: Prevent divergence
- **WHEN** a view requests `astRoot`
- **THEN** it references the current workspace AST state