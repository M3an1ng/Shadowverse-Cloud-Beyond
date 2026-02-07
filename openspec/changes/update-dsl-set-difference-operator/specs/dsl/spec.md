# DSL Set Operators (Delta)

## MODIFIED Requirements

### Requirement: Set Difference Operator Symbol
The DSL MUST use ~ as the set-difference operator, and - MUST be reserved for numeric subtraction only.

#### Scenario: Parse Set Difference
Given a DSL expression context.owner.field ~ context.owner.leader, the parser yields a set-difference operator node.

#### Scenario: Evaluate Set Difference
Given two collections A and B, evaluating A ~ B returns elements in A that are not in B.

#### Scenario: Editor Shows Set Difference Operator
When editing a DSL expression, the operator menu and preview text show ~ for set-difference.
