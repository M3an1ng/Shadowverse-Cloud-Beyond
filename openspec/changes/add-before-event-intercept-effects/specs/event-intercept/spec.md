## ADDED Requirements
### Requirement: Before-event intercept pipeline
The system SHALL allow a before-event pipeline to rewrite event parameters or replace events before commit.

#### Scenario: Rewrite damage amount
- **WHEN** a before-event intercept rewrites a damage event amount
- **THEN** the committed damage uses the rewritten amount

#### Scenario: Replace event
- **WHEN** a before-event intercept replaces an event with another event
- **THEN** only the replacement event is committed

#### Scenario: Destroy replaced by vanish
- **WHEN** a destroy event is replaced by a vanish event in the Before phase
- **THEN** destroy-related triggers do not fire and vanish-related triggers do

### Requirement: Intercept scope 与安全改写
Before-event intercepts SHALL be eligible for any event type, but parameter rewrites MUST target `GameEventParameter` properties with `Input` or `Both` scope and MUST NOT mutate game state directly.

#### Scenario: Unsupported parameter
- **WHEN** an intercept attempts to rewrite a non-rewritable parameter
- **THEN** the rewrite fails safely without mutating the event or game state

#### Scenario: No state side effects
- **WHEN** an intercept effect executes
- **THEN** no direct state mutation occurs during the Before phase

### Requirement: Deterministic ordering and conflict resolution
Intercept effects SHALL be executed in deterministic order, and parameter rewrites SHALL apply in execution order with last-write-wins semantics.

#### Scenario: Multiple rewrites of the same field
- **WHEN** two intercept effects rewrite the same event parameter
- **THEN** the later executed rewrite value is used

#### Scenario: Multiple replacements in the same iteration
- **WHEN** two intercept effects attempt to replace the same event
- **THEN** the first executed replacement is used for that iteration

### Requirement: Re-evaluation loop with maximum iterations
The system SHALL re-evaluate Before intercepts after an event is rewritten or replaced until no further changes occur or a maximum iteration limit is reached.

#### Scenario: Re-evaluation after replace
- **WHEN** an intercept replaces the event type
- **THEN** intercept eligibility is re-evaluated against the new event

#### Scenario: Maximum iteration reached
- **WHEN** the intercept loop reaches the configured maximum iterations
- **THEN** the latest event state is committed and a warning is logged

#### Scenario: Immediate execution on discovery
- **WHEN** a Before intercept is discovered and executed
- **THEN** the event state is re-evaluated before any remaining intercepts for the prior state execute

#### Scenario: Ability intercepts only once per event ID
- **WHEN** an ability intercepts an event and the event is rewritten
- **THEN** the same ability is not allowed to intercept the same event ID again during the intercept cycle

### Requirement: Configurable leave-field intercept ability
Cards SHALL be able to define a Before-triggered ability that replaces their own leave-field events (Destroy/Return/Tuck) with Vanish by executing ReplaceEvent.

#### Scenario: Destroy replaced by Vanish
- **WHEN** a card with the configured leave-field intercept ability is destroyed
- **THEN** the destroy event is replaced by Vanish before commit

#### Scenario: Return replaced by Vanish
- **WHEN** a card with the configured leave-field intercept ability is returned to hand
- **THEN** the return event is replaced by Vanish before commit

#### Scenario: Tuck replaced by Vanish
- **WHEN** a card with the configured leave-field intercept ability is tucked into deck from the field
- **THEN** the tuck event is replaced by Vanish before commit

#### Scenario: No LeaveVanish keyword
- **WHEN** the leave-field intercept ability is used to replace leave-field events
- **THEN** no card keyword is required to enable the behavior
