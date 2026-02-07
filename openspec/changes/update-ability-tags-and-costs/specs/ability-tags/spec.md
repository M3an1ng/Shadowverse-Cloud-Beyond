## ADDED Requirements
### Requirement: Ability keyword list
The system SHALL allow an ability to declare multiple ability keywords.

#### Scenario: Multiple keywords declared
- **WHEN** an ability is configured with multiple keywords
- **THEN** the runtime exposes all configured keywords

### Requirement: Triggering decoupled from keywords
Ability triggering SHALL be controlled only by TriggerConfig, not by ability keywords.

#### Scenario: Keyword does not affect trigger
- **WHEN** a keyword is added or removed
- **THEN** trigger evaluation does not change unless TriggerConfig changes

### Requirement: Keyword parameters for costs
The system SHALL allow specific keywords to carry numeric parameters (Enhance, Act, Necromancy, Sacrifice).

#### Scenario: Keyword parameter is available
- **WHEN** an ability declares Necromancy with amount N
- **THEN** the runtime exposes N for cost evaluation
