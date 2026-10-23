## ADDED Requirements

### Requirement: Preserve Follower Health Floor After Health Buff Removal
The system SHALL keep a follower alive at 1 current health when removing one or more health-modifying buffs leaves that follower's raw max health at 0 or below.

#### Scenario: Health buff removal leaves raw max health at zero
- **WHEN** a health-modifying buff is removed from a follower and the follower's raw max health after removal is 0
- **THEN** the follower remains on the field with current health 1
- **AND** the buff removal does not generate a destroy or leave-field event solely because raw max health is 0

#### Scenario: Health buff removal leaves raw max health below zero
- **WHEN** a health-modifying buff is removed from a follower and the follower's raw max health after removal is less than 0
- **THEN** the follower remains on the field with current health 1
- **AND** the follower's effective max health for gameplay display and health calculation is at least 1

#### Scenario: Health buff removal leaves positive raw max health
- **WHEN** a health-modifying buff is removed from a follower and the follower's raw max health after removal is greater than 0
- **THEN** the system clamps current health to the lower of the pre-removal current health and the new max health
- **AND** no health floor is applied

### Requirement: Limit Health Floor To Buff Removal Resolution
The system SHALL apply the 1-health floor only as a result of health-modifying buff removal and MUST NOT apply it to newly applied health-reduction buffs or other stat changes.

#### Scenario: New health reduction buff is lethal
- **WHEN** a follower receives a health-reducing buff that makes its projected health 0 or below
- **THEN** the system resolves the follower as destroyed through the normal stat-change lethal path
- **AND** no buff-removal health floor is applied

#### Scenario: Existing floored follower receives lethal health reduction
- **WHEN** a follower that is alive because of a buff-removal health floor receives a further health-reducing buff that makes its raw max health 0 or below
- **THEN** the system resolves the follower as destroyed through the normal stat-change lethal path
- **AND** the existing health floor does not prevent destruction

#### Scenario: Floored follower takes lethal damage
- **WHEN** a follower that is alive because of a buff-removal health floor takes damage that reduces current health to 0 or below
- **THEN** the system resolves the follower as destroyed through the normal damage lethal path

### Requirement: Maintain Non-Negative Damage Semantics
The system SHALL preserve the existing invariant that a card's `Damage` value represents non-negative accumulated damage.

#### Scenario: Health floor is applied after buff removal
- **WHEN** the system keeps a follower alive at 1 health after health buff removal
- **THEN** the follower's `Damage` value is not negative
- **AND** current health is represented by an explicit health-floor mechanism rather than by negative damage

### Requirement: Clear Buff-Removal Health Floor When It No Longer Applies
The system SHALL clear any buff-removal health floor when the follower no longer needs that floor or leaves the battlefield state where the floor was created.

#### Scenario: Raw max health becomes positive
- **WHEN** a follower with a buff-removal health floor later has raw max health greater than 0
- **THEN** the system clears the health floor
- **AND** subsequent health calculations use normal raw max health and non-negative damage

#### Scenario: Follower leaves field or resets identity
- **WHEN** a follower with a buff-removal health floor leaves the field, transforms, or is reset as a new card instance state
- **THEN** the system clears the health floor from that card state
