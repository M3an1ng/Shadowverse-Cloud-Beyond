## ADDED Requirements
### Requirement: Deferred Target Resolution For Non-Player Actions
The system SHALL resolve ability targets at effect execution time for abilities triggered by non-IPlayerActionEvent events.

#### Scenario: Summon-then-buff chain resolves new targets
- **WHEN** a non-player-action trigger creates a Summon effect and a later effect targets allied followers
- **THEN** the target selection includes followers created by the earlier effect

### Requirement: Preserve Primary Ability Pre-Resolution For Player Actions
The system SHALL keep pre-resolution for primary abilities triggered by IPlayerActionEvent to support cancellable player choices.

#### Scenario: PlayCard fanfare with player choice remains pre-resolved
- **WHEN** a player plays a card with a choice-based fanfare
- **THEN** target selection is resolved before commit and can be cancelled
