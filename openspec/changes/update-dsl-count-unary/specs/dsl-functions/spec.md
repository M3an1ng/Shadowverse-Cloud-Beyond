## MODIFIED Requirements

### Requirement: Count is unary
The system SHALL treat `count` as a unary function with exactly one argument.

#### Scenario: Count collection size
- **GIVEN** a DSL expression `count(context.triggeringEvent.ResolvedTargets)`
- **WHEN** the expression is evaluated
- **THEN** it SHALL return the number of items in the collection

#### Scenario: Count counter value
- **GIVEN** a DSL expression `count("MyCounter")`
- **WHEN** it is evaluated with a source card that has that counter
- **THEN** it SHALL return the counter value

### Requirement: Property-based count is removed
The system SHALL not accept `count(collection, property)`.

#### Scenario: Legacy signature rejected
- **GIVEN** a DSL expression `count(cards, "CurrentCost")`
- **WHEN** it is parsed or validated
- **THEN** it SHALL report an unsupported signature

### Requirement: Sum is unary and numeric-collection only
The system SHALL treat `sum` as a unary function with exactly one argument that MUST be a numeric collection.

#### Scenario: Sum numeric list
- **GIVEN** a DSL expression `sum(context.triggeringEvent.DamageValues)`
- **WHEN** the expression is evaluated
- **THEN** it SHALL return the total of the numeric list

#### Scenario: Sum legacy signature rejected
- **GIVEN** a DSL expression `sum(cards, "CurrentCost")`
- **WHEN** it is parsed or validated
- **THEN** it SHALL report an unsupported signature

### Requirement: Max is unary and numeric-collection only
The system SHALL treat `max` as a unary function with exactly one argument that MUST be a numeric collection.

#### Scenario: Max numeric list
- **GIVEN** a DSL expression `max(context.triggeringEvent.DamageValues)`
- **WHEN** the expression is evaluated
- **THEN** it SHALL return the maximum numeric value in the list

#### Scenario: Max legacy signature rejected
- **GIVEN** a DSL expression `max(cards, "CurrentCost")`
- **WHEN** it is parsed or validated
- **THEN** it SHALL report an unsupported signature

### Requirement: Min is unary and numeric-collection only
The system SHALL treat `min` as a unary function with exactly one argument that MUST be a numeric collection.

#### Scenario: Min numeric list
- **GIVEN** a DSL expression `min(context.triggeringEvent.DamageValues)`
- **WHEN** the expression is evaluated
- **THEN** it SHALL return the minimum numeric value in the list

#### Scenario: Min legacy signature rejected
- **GIVEN** a DSL expression `min(cards, "CurrentCost")`
- **WHEN** it is parsed or validated
- **THEN** it SHALL report an unsupported signature
