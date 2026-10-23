## Purpose

Ensure default deck generation remains legal under format-scoped copy-limit policies.

## Requirements

### Requirement: Default deck generation must respect effective copy limits
The default deck bootstrap/generation path SHALL construct entries such that each card count does not exceed the effective copy limit for the target environment and rule.

#### Scenario: Generator skips cards with zero limit
- **WHEN** a candidate card has effective limit `0` for the default deck format
- **THEN** the generator excludes that card from generated deck entries

### Requirement: Default deck generation must fail safely when constraints are unsatisfiable
If effective copy limits make it impossible to build a legal default-size deck, the system MUST fail with explicit error and MUST NOT persist a partial invalid deck.

#### Scenario: Insufficient legal pool cannot produce 40 cards
- **WHEN** total legal capacity under effective limits is below required deck size
- **THEN** default deck bootstrap returns failure instead of creating an invalid or partial deck

### Requirement: Default deck behavior must remain backward compatible under no policy config
When no copy-limit policy rows exist for the default format, bootstrap behavior SHALL remain equivalent to current default limit `3` behavior.

#### Scenario: No policy config keeps legacy generation behavior
- **WHEN** default deck bootstrap runs without any matching copy-limit policies
- **THEN** generated deck logic behaves as if every collectible candidate card has max copies `3`
