## ADDED Requirements
### Requirement: Distinct pipeline operation
The system SHALL support a distinct pipeline operation for collection queries in both DSL expressions and QueryConfig pipelines. The DSL syntax SHALL be `collection.distinct(keyExpr)` where `keyExpr` is evaluated per element (`self`) to determine uniqueness. The key expression is required; missing `keyExpr` SHALL be rejected during parsing or validation. The key expression result SHALL be a non-entity value type or string (for example: numeric, enum, boolean, Guid, DateTime, or string). Entity/object keys are invalid and SHALL produce an execution error. The operation SHALL preserve the first occurrence order and remove subsequent elements with equal keys.

#### Scenario: Distinct by cost in DSL
- **WHEN** a query includes `context.owner.field.distinct(self.Cost)`
- **THEN** the result retains only the first card for each distinct cost value in the original order.

#### Scenario: Distinct operation via QueryConfig
- **WHEN** QueryConfig includes a DistinctOperation with a key selector equivalent to `self.Cost`
- **THEN** the executor returns the same deduplicated order as the DSL distinct operation.

#### Scenario: Distinct rejects entity keys
- **WHEN** a distinct key expression evaluates to a business entity object
- **THEN** the query execution fails with a validation error indicating the key type is unsupported.