## ADDED Requirements

### Requirement: Per-card scenario coverage
The system SHALL provide at least one executable scenario test for every card defined in `data/card_data_*.json`.

#### Scenario: Scenario exists for a card
- **WHEN** a card id appears in `card_data_*.json`
- **THEN** the scenario suite includes at least one scenario referencing that card id.

### Requirement: Scenario harness documentation
The system SHALL document how to run scenario tests and how to author scenarios, including the action lifecycle, choice key rules, and a neutral example scenario.

#### Scenario: Developer consults scenario documentation
- **WHEN** a developer reads the scenario test documentation
- **THEN** it includes the run command, action lifecycle mapping, choice key rules, and a neutral example scenario.

#### Documentation details (informative)
- Run command: `dotnet test Server.Tests/ShadowverseBeyond.Server.Tests.csproj -c Release --filter FullyQualifiedName~CardScenarioTests`
- Action lifecycle mapping:
  - PlayCard: `PlayCardRequestedEvent` -> `PlayCardEvent` -> summon/move -> ability triggers in Before/On/After.
  - Evolve: `EvolveRequestedEvent` -> `EvolveEvent` -> stat updates -> `EventType.Evolve` triggers.
  - SuperEvolve: `SuperEvolveRequestedEvent` -> `EvolveEvent` + `SuperEvolveEvent` -> `EventType.SuperEvolve` triggers.
  - Act: `ActRequestedEvent` -> `ActInvokedEvent` -> `EventType.Act` triggers.
- Choice keys:
  - `target1` for single-target selections.
  - `choose_{effectIndex}` for Choose effects (0-based).
  - Lookup order: `{AbilityName}.{TargetKey}` -> `{CardKey}.{TargetKey}` -> `{TargetKey}`.
- Neutral example (card id 1401301):
  - Setup: P1 has 3 PP, leader at 17 HP, card 1401301 in hand, any drawable card in deck.
  - Action: Play card 1401301.
  - Expectations: card moves to graveyard, leader heals to 20, hand count increases by 1.
