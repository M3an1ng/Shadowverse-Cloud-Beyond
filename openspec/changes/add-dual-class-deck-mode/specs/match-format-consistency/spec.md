## ADDED Requirements

### Requirement: Match start requires same environment and mode
The system SHALL require all participants in a match to use the same `environment` and `mode` before match start.

#### Scenario: Reject environment mismatch
- **WHEN** host and opponent submit deck selections with different environments
- **THEN** the system rejects match start with a format mismatch error

#### Scenario: Reject mode mismatch
- **WHEN** host and opponent submit deck selections with different modes
- **THEN** the system rejects match start with a format mismatch error

### Requirement: AI fallback follows host-selected format
When AI fallback is enabled, the AI deck selection SHALL use the same environment and mode constraints as the host-selected format.

#### Scenario: Reject AI mode mismatch
- **WHEN** host selection and AI selection use different modes
- **THEN** the system rejects the deck selection or match start with a format mismatch error

#### Scenario: Reject AI environment mismatch
- **WHEN** host selection and AI selection use different environments
- **THEN** the system rejects the deck selection or match start with a format mismatch error

### Requirement: Runtime player state reflects resolved format
After successful match initialization, each player runtime state SHALL carry the resolved environment and mode values used for validation.

#### Scenario: Initialize runtime format context
- **WHEN** a match starts successfully
- **THEN** player runtime states contain the resolved environment and mode for downstream game logic and analytics
