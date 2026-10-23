## MODIFIED Requirements

### Requirement: Distinct pipeline operation
The system SHALL support a distinct pipeline operation for collection queries in both DSL expressions and QueryConfig pipelines. The DSL syntax SHALL be `collection.distinct(keyExpr)` where `keyExpr` is evaluated per element (`self`) to determine uniqueness. The key expression is required; missing `keyExpr` SHALL be rejected during parsing or validation.

Scalar key results SHALL continue to use stable first-occurrence deduplication. List-valued key results SHALL be expanded into single-element keys and deduplicated by selecting a maximum-cardinality subset of source elements such that each source element is kept at most once and each expanded single-element key is assigned to at most one retained source element. Entity/object keys remain invalid and SHALL produce an execution error.

#### Scenario: Distinct by cost in DSL
- **WHEN** a query includes `context.owner.field.distinct(self.Cost)`
- **THEN** the result retains only the first card for each distinct cost value in the original order.

#### Scenario: Distinct by list key keeps the largest possible subset
- **WHEN** a query includes a list-valued key such as `source.distinct(self.Tags)`
- **AND** the expanded single-element keys allow multiple maximum-cardinality matchings
- **THEN** the executor SHALL retain a subset with the largest possible number of source elements
- **AND** each retained source element SHALL appear at most once in the output

#### Scenario: Distinct by tribe list expands tribe semantics
- **WHEN** a query includes `source.distinct(self.Tribe)` or `source.distinct(self.LeaveTargetSnapshot.Tribe)`
- **THEN** the executor SHALL expand tribe keys through the runtime tribe semantics layer before applying list-key distinct matching
- **AND** special match-all tribe values SHALL expand to the current known normal tribes

#### Scenario: Cyclic tribe overlap retains all three cards when possible
- **WHEN** distinct is applied to three list-valued tribe keys:
  - `[士兵, 海洋]`
  - `[海洋, 妖精]`
  - `[妖精, 士兵]`
- **THEN** the executor SHALL retain all three source elements because a maximum-cardinality matching exists

#### Scenario: Distinct operation via QueryConfig
- **WHEN** QueryConfig includes a DistinctOperation with a key selector equivalent to `self.Cost`
- **THEN** the executor returns the same deduplicated order as the DSL distinct operation
- **AND** list-valued keys SHALL use the same expanded maximum-cardinality semantics as DSL distinct

#### Scenario: Distinct rejects entity keys
- **WHEN** a distinct key expression evaluates to a business entity object
- **THEN** the query execution fails with a validation error indicating the key type is unsupported
