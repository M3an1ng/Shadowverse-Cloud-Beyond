## ADDED Requirements

### Requirement: Preserve explicit parentheses in parse API
The system SHALL represent explicit parentheses in a DSL expression as a ParenthesizedExpression node in the parse API output.

#### Scenario: Parentheses in input
- **WHEN** a DSL expression includes explicit parentheses around a sub-expression
- **THEN** the parse API returns a ParenthesizedExpression node that wraps the inner expression

### Requirement: Restore encapsulated UI nodes from parentheses
The system SHALL map ParenthesizedExpression nodes to encapsulated UI nodes when rebuilding the DSL editor AST.

#### Scenario: Encapsulation restored
- **WHEN** a saved DSL expression with parentheses is loaded into the editor
- **THEN** the editor displays an encapsulated node corresponding to the parenthesized sub-expression

### Requirement: Do not auto-insert parentheses
The system SHALL NOT add or normalize parentheses beyond those explicitly present in the saved DSL expression.

#### Scenario: No implicit grouping
- **WHEN** a saved DSL expression has no explicit parentheses around a sub-expression
- **THEN** the parse API does not emit a ParenthesizedExpression node for that sub-expression
