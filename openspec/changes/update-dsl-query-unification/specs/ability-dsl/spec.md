## ADDED Requirements

### Requirement: Unified collection query operators
The system SHALL support `.query(...)`, `.orderBy(...)`, and `.take(...)` operators in all DSL expression contexts, including target selection, conditions, and effect parameters.

#### Scenario: Effect parameter uses query operators
- **WHEN** an effect parameter formula contains `owner.deck.query(self.Type == "Follower").take(random 1)`
- **THEN** the expression evaluates to a collection of matching cards without requiring a separate query parser

### Requirement: AbilityTargets access in all DSL contexts
The system SHALL expose resolved targets through `AbilityTargets` in all DSL expressions, regardless of whether the expression is used by target selection, conditions, or effect parameters.

#### Scenario: Condition references AbilityTargets
- **WHEN** a condition expression references `AbilityTargets.target1.find(1)`
- **THEN** the expression resolves the chosen target using the current TargetSelectionContext