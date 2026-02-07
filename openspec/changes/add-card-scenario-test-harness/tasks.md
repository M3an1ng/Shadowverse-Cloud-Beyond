## 1. Definition
- [x] 1.1 Define scenario data model (setup, actions, expectations) and deterministic selection strategy.
- [x] 1.2 Document action lifecycle mapping (request -> validation -> commit -> event chain) for play/evolve/act/fusion/choose.

## 2. Test Harness
- [x] 2.1 Build scenario runner utilities for initializing GameState, registering cards/abilities, and executing actions.
- [x] 2.2 Add deterministic choice resolver and RNG seeding for selection-based abilities.
- [x] 2.3 Add assertion helpers for common state checks (zones, HP, keywords, countdown, stats).
- [x] 2.4 Implement data-driven xUnit entry point that iterates ScenarioCase definitions.

## 3. Forestcraft Coverage
- [x] 3.1 Create one scenario per Forestcraft card (card_data_forestcraft.json).
- [x] 3.2 Run scenario suite and fix any gaps in runner or assertions.

## 4. Documentation
- [x] 4.1 Add contributor notes for adding new card scenarios.
