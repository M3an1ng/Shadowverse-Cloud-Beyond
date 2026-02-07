## ADDED Requirements

### Requirement: Collection Auto-Boxing
The DSL evaluation engine MUST treat a non-collection value as a singleton collection whenever an `IEnumerable`/`List<T>` is expected.

#### Scenario: Query source accepts single card instance
- **WHEN** a query source expression evaluates to a single `CardInstance`
- **THEN** it is treated as a collection containing that single card

#### Scenario: Collection function accepts single value
- **WHEN** a collection function (e.g., `sum`, `min`, `max`, `isEmpty`) receives a single non-collection input
- **THEN** it evaluates against a singleton collection containing that input

### Requirement: String Auto-Boxing Exclusion
The DSL MUST NOT treat strings as collections when applying collection auto-boxing.

#### Scenario: String is not treated as collection
- **WHEN** a collection-typed context receives a string value
- **THEN** the expression is rejected or handled as a type error rather than iterating characters

## MODIFIED Requirements

### Requirement: Collection Operator Type Enforcement
The DSL MUST reject collection operators when operands are neither collections nor single non-string values that can be auto-boxed into singleton collections.

#### Scenario: Type mismatch is reported
- **WHEN** a user evaluates `owner.field | 1`
- **THEN** the expression auto-boxes the single value into a singleton collection before applying the operator

#### Scenario: Invalid operand is rejected
- **WHEN** a user evaluates `owner.field | "abc"`
- **THEN** the parser or validator reports a type error and the expression does not compile
