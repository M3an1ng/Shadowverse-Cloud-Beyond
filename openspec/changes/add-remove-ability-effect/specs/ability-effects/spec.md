## ADDED Requirements

### Requirement: Remove Ability Effect
The system SHALL provide a RemoveAbility effect that suppresses abilities on a target card without destroying the ability instances.

#### Scenario: Remove by DefinitionId
- **WHEN** RemoveAbility resolves with RemoveMode=ID and a list of Ability DefinitionIds
- **THEN** the target card SHALL suppress abilities whose DefinitionId matches the list
- **AND** suppressed abilities SHALL not trigger and SHALL not be available for Act/Fusion requests

#### Scenario: Remove by Keyword
- **WHEN** RemoveAbility resolves with RemoveMode=Keyword and AbilityKeyword=Lastword
- **THEN** the target card SHALL suppress abilities that include the specified keyword
- **AND** suppressed abilities SHALL not trigger and SHALL not be available for Act/Fusion requests

#### Scenario: Remove All
- **WHEN** RemoveAbility resolves with RemoveMode=ALL
- **THEN** the target card SHALL suppress all abilities it owns, including base and granted abilities

### Requirement: Suppression Duration and Aura Handling
The system SHALL apply ability suppression via a duration-capable buff and restore abilities when suppression ends.

#### Scenario: Suppression ends
- **WHEN** the suppression buff expires or is removed
- **THEN** previously suppressed abilities SHALL be restored to normal triggering and availability

#### Scenario: Aura removal during suppression
- **WHEN** a suppressed ability has active aura effects
- **THEN** those aura effects SHALL be removed while suppression is active
- **AND** if suppression ends, aura effects SHALL be restored according to aura refresh rules
