## ADDED Requirements
### Requirement: Evolve Event Super Evolution Flag
EvolveEvent SHALL include a boolean IsSuperEvolution flag to indicate if the evolve was triggered by super evolve.

#### Scenario: Normal evolve
- **WHEN** a card evolves via a normal evolve action
- **THEN** EvolveEvent.IsSuperEvolution is false

#### Scenario: Super evolve
- **WHEN** a card evolves via a super evolve action
- **THEN** EvolveEvent.IsSuperEvolution is true and SuperEvolveEvent is also emitted
