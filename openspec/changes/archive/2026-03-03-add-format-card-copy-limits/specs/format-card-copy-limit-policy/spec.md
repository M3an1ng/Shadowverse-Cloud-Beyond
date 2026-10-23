## ADDED Requirements

### Requirement: Format-scoped card copy limit key model
The system SHALL resolve card copy limits by the composite key `(DeckEnvironment, DeckRule, CardDefinitionId)`.

#### Scenario: Resolve exact environment and rule key
- **WHEN** a deck in `Open + Standard` contains a card with an explicit policy row for that exact combination
- **THEN** the system uses that row as the effective max copy limit for the card

### Requirement: Default fallback limit
If no policy row exists for a card under a given environment and rule, the system SHALL fall back to the default copy limit of `3`.

#### Scenario: Missing policy row falls back to 3
- **WHEN** a card has no configured policy for the deck's environment and rule
- **THEN** the effective max copy limit is treated as `3`

### Requirement: Policy value range and ban semantics
Policy `MaxCopies` MUST be an integer in range `0..3`, where `0` SHALL represent "banned in this environment/rule combination".

#### Scenario: Reject invalid policy value
- **WHEN** an administrator submits a policy value outside `0..3`
- **THEN** the system rejects the request with a validation error

#### Scenario: Zero-copy policy means banned
- **WHEN** a card policy is configured as `MaxCopies = 0` for an environment and rule
- **THEN** the effective policy marks the card as not legal to include in decks under that combination

### Requirement: Unique policy row per key
The policy store MUST enforce uniqueness for `(DeckEnvironment, DeckRule, CardDefinitionId)` to ensure deterministic resolution.

#### Scenario: Prevent duplicate key row
- **WHEN** a second policy row is created for an existing `(environment, rule, card)` key
- **THEN** the system updates the existing row or rejects duplication instead of storing two active rows

### Requirement: Policy management operations
The system SHALL support querying policies by environment and rule, upserting policy rows, and deleting policy rows to restore default fallback behavior.

#### Scenario: Delete policy row restores fallback
- **WHEN** a configured policy row is deleted
- **THEN** subsequent evaluations for that card/environment/rule use the default limit `3`
