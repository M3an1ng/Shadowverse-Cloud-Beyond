## ADDED Requirements

### Requirement: Collection Union Operator
The DSL MUST support the `|` operator to produce the union of two collection expressions with compatible element types.

#### Scenario: Union combines two target pools
- **WHEN** a user evaluates `owner.field | opponent.field`
- **THEN** the result contains unique elements from both collections

### Requirement: Collection Intersection Operator
The DSL MUST support the `&` operator to produce the intersection of two collection expressions with compatible element types.

#### Scenario: Intersection keeps only shared elements
- **WHEN** a user evaluates `state.allCards & owner.field`
- **THEN** the result contains only elements present in both collections

### Requirement: Collection Difference Operator
The DSL MUST support the `-` operator to produce the difference of two collection expressions with compatible element types.

#### Scenario: Difference excludes items from the second collection
- **WHEN** a user evaluates `state.allCards - owner.field`
- **THEN** the result excludes elements that appear in `owner.field`

### Requirement: Collection Operator Type Enforcement
The DSL MUST reject collection operators when operands are not collections or element types are incompatible.

#### Scenario: Type mismatch is reported
- **WHEN** a user evaluates `owner.field | 1`
- **THEN** the parser or validator reports a type error and the expression does not compile

### Requirement: Collection Operators in Query Sources
The query system MUST accept collection operators inside `QueryConfig.SourcePoolExpression` and evaluate them before applying `.query/.orderBy/.take` operations.

#### Scenario: Query source uses union
- **WHEN** a query source expression includes `owner.field | opponent.field`
- **THEN** the query executes against the unioned pool before applying pipeline operations

### Requirement: Editor Exposure for Collection Operators
The DSL editor MUST expose collection operators when the current slot expects a collection type.

#### Scenario: Operator choices include set operators
- **WHEN** the editor is building a `List<CardInstance>` expression
- **THEN** it offers Union (`|`), Intersection (`&`), and Difference (`-`) as selectable operators
