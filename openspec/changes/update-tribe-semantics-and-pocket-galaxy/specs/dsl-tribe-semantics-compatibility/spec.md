## ADDED Requirements

### Requirement: Existing DSL tribe equality syntax remains valid
The system SHALL preserve existing DSL tribe equality expressions while routing evaluation through tribe semantics.

#### Scenario: Equality matches canonical tribe directly
- **WHEN** a DSL expression evaluates `self.Tribe == "<tribe>"`
- **THEN** the evaluator SHALL treat the comparison as a semantic tribe membership check
- **AND** the expression SHALL return true when the target card effectively has the canonical `tribe`

#### Scenario: Equality matches configured alias
- **WHEN** a DSL expression evaluates `self.Tribe == "<alias>"`
- **THEN** the evaluator SHALL normalize the alias through the tribe registry
- **AND** the expression SHALL return true when the target card effectively has the corresponding canonical `tribe`

#### Scenario: Equality honors special match-all tribe
- **WHEN** a card effectively has a configured match-all special `tribe`
- **THEN** `self.Tribe == "<normal-tribe>"` SHALL evaluate to true for every normal `tribe` covered by that special behavior

### Requirement: Contains function uses tribe semantics for tribe collections
The system SHALL evaluate `contains(<tribe-collection>, "<tribe>")` through the same semantic layer used by tribe equality.

#### Scenario: Contains matches canonical tribe
- **WHEN** `contains(self.Tribe, "<tribe>")` is evaluated against a card with that effective canonical `tribe`
- **THEN** the function SHALL return true

#### Scenario: Contains matches alias through normalization
- **WHEN** `contains(self.Tribe, "<alias>")` is evaluated and the alias maps to the card's canonical `tribe`
- **THEN** the function SHALL return true

#### Scenario: Contains honors special match-all tribe
- **WHEN** `contains(self.Tribe, "<normal-tribe>")` is evaluated against a card with a configured match-all special `tribe`
- **THEN** the function SHALL return true
