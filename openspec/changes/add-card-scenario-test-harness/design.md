## Context
Card behavior validation will expand rapidly as new cards and mechanics are added. Current tests are ad-hoc and not scalable for per-card, full-flow assertions.

## Goals / Non-Goals
- Goals:
  - Provide a data-driven scenario model for card tests.
  - Execute full action flow (request, validation, commit, event chain) in tests.
  - Ensure deterministic resolution of choices and randomness.
  - Make adding a new card test require only scenario data, not new runner logic.
- Non-Goals:
  - Replace integration tests unrelated to card behaviors.
  - Build a full gameplay simulator for AI or UI automation.

## Decisions
- Decision: Implement a scenario runner in Server.Tests with a structured scenario model (setup, actions, expectations).
  - Rationale: Keeps tests close to domain logic and reuses existing runtime types.
- Decision: Use deterministic choice strategies (explicit target ids or seeded selection).
  - Rationale: Avoid flaky tests and support reproducibility.
- Decision: Load abilities from card_data_*.json for scenario execution.
  - Rationale: Ensures tests validate the same data used by the game runtime.

## Risks / Trade-offs
- Additional test infrastructure increases upfront work but reduces per-card test effort.
- Some complex cards will still require custom assertions or helper actions.

## Migration Plan
- Introduce scenario model and runner.
- Add Forestcraft scenarios (1 per card) to validate coverage.
- Iterate with additional crafts once the pattern stabilizes.

## Open Questions
- Preferred default selection strategy when multiple valid targets exist.
- Whether scenarios should live in C# code or JSON fixtures for easier authoring.
