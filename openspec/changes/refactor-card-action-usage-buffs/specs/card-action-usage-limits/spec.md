## ADDED Requirements

### Requirement: Card action usage state SHALL be tracked independently from usage limits
The system SHALL track per-turn used counts for card actions independently from their per-turn limits. `Attack`, `Act`, and `Fusion` SHALL share one conceptual model for used-count tracking even if compatibility properties remain available.

#### Scenario: Attack consumption increases only the attack used count
- **WHEN** a follower successfully commits an attack action that consumes attack usage
- **THEN** the card's attack used count increases by `1`
- **AND** the card's Act and Fusion used counts remain unchanged

#### Scenario: Turn start resets used counts without rewriting limits
- **WHEN** a player's turn-start reset runs for cards in relevant zones
- **THEN** the system resets the card's used counts for the supported action types
- **AND** the system does not rewrite the card's per-turn action limits to hard-coded defaults

### Requirement: Per-turn action limits SHALL be derived from action-usage limit buffs
The system SHALL derive per-turn limits for `Attack`, `Act`, and `Fusion` from a unified action-usage limit buff model instead of treating those limits as directly mutable runtime fields.

#### Scenario: Attack limit is raised by a usage-limit buff
- **WHEN** a card has an active action-usage limit buff for `Attack` that sets or increases its per-turn limit
- **THEN** attack availability checks use the derived attack limit from the active buff set

#### Scenario: Act and Fusion limits use the same limit-derivation model
- **WHEN** a card has active action-usage limit buffs for `Act` or `Fusion`
- **THEN** the corresponding action request and commit flow uses the derived limit from the same unified limit model

#### Scenario: Expired usage-limit buff no longer contributes to the limit
- **WHEN** an action-usage limit buff expires or is removed
- **THEN** the derived per-turn limit is recomputed without that buff

### Requirement: Action quota evaluation SHALL remain separate from action-specific permission checks
The system SHALL use the unified action usage model only for per-turn quota evaluation and MUST keep action-specific preconditions in their existing action flows.

#### Scenario: Act ability suppression blocks Act even with remaining quota
- **WHEN** a card still has remaining `Act` quota for the turn
- **AND** all of its Act abilities are suppressed or unavailable
- **THEN** the system rejects the Act action according to ability validation rules

#### Scenario: Attack target validation remains outside the quota model
- **WHEN** a follower still has remaining `Attack` quota for the turn
- **AND** the chosen target is illegal under attack targeting rules
- **THEN** the system rejects the attack without relying on the quota model to explain the targeting failure

### Requirement: SetAttackTimes SHALL apply attack usage-limit buffs rather than directly mutating card state
The system SHALL treat `SetAttackTimes` as an effect that changes the attack usage limit through the unified action-usage limit buff model.

#### Scenario: SetAttackTimes grants persistent multi-attack through a derived limit
- **WHEN** a card effect resolves `SetAttackTimes` with `MaxAttacksPerTurn = 2`
- **THEN** the target follower's attack limit becomes `2` through the action-usage limit model
- **AND** subsequent attack availability checks read the derived limit rather than a directly written runtime max-attack field

#### Scenario: SetAttackTimes removal restores the derived limit
- **WHEN** the effect instance that raised a follower's attack limit is removed, expires, or is silenced according to its duration semantics
- **THEN** the follower's effective per-turn attack limit is recomputed from remaining active sources

### Requirement: Existing outward action-state semantics SHALL remain stable during the refactor
The system SHALL preserve existing client-visible action-state behavior while moving server-side usage tracking and limit derivation to the unified model.

#### Scenario: Attack availability updates still reflect derived attack quota
- **WHEN** a follower consumes its final remaining attack for the turn under the unified usage model
- **THEN** the outward attack-state update still indicates that the follower can no longer attack

#### Scenario: Act and Fusion state updates still report used and limit values
- **WHEN** a card consumes an `Act` or `Fusion` use under the unified usage model
- **THEN** the outward state update still reports the current used count and the current derived per-turn limit for that action
