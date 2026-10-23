## ADDED Requirements

### Requirement: Validation must use dynamic copy limits
Deck legality validation SHALL determine per-card max copies using the format-scoped policy and default fallback, rather than a fixed global hardcoded value.

#### Scenario: Enforce configured limit 1
- **WHEN** a deck includes 2 copies of a card whose effective policy limit is `1` under the selected environment and rule
- **THEN** validation fails with an over-limit error

### Requirement: Validation must enforce banned cards
If the effective limit for a card is `0`, validation MUST reject any positive count of that card for the corresponding environment and rule.

#### Scenario: Reject banned card inclusion
- **WHEN** a deck contains a card with effective limit `0` under the selected environment and rule
- **THEN** validation fails with a banned-card error

### Requirement: Error message must include actionable reason
Copy-limit validation failures SHALL include actionable reason text containing at least the card identity and effective limit.

#### Scenario: Return detailed copy-limit error
- **WHEN** validation rejects a deck for copy-limit overflow
- **THEN** the error text contains the failing card and its effective max copy value

### Requirement: All deck-entry paths must share the same copy-limit validation
All backend paths that accept or resolve deck content MUST use the same copy-limit validation source, including deck create, deck update, deck import, and match preflight/start deck resolution.

#### Scenario: Import path and create path are consistent
- **WHEN** the same deck payload is submitted via deck import and deck create for the same environment and rule
- **THEN** both paths produce the same copy-limit validation result

#### Scenario: Match preflight rejects policy-violating deck
- **WHEN** a selected deck violates copy-limit policy at match preflight/start stage
- **THEN** match start is rejected with copy-limit validation error
