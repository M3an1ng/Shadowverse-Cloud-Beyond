## ADDED Requirements

### Requirement: GameContext exposes resolved target pools
The system SHALL expose resolved ability target pools via `context.resolvedTargets` for DSL access.

#### Scenario: Resolved targets available after target resolution
- **WHEN** an ability finishes resolving targets
- **THEN** context.resolvedTargets contains the resolved target pools for the current ability

### Requirement: No previous target key injection
The system SHALL NOT inject a `previous` target key during target resolution.

#### Scenario: Target resolution does not add previous
- **WHEN** an ability resolves its targets
- **THEN** the resolved target pools do not include `previous`