# Change: Add card scenario test harness

## Why
We need a reusable, data-driven scenario runner to verify card behavior with full game flow and deterministic assertions, so future card tests scale without per-card custom code.

## What Changes
- Add a scenario definition model and test runner to execute full game-flow actions (play/evolve/act/choose) with deterministic choice resolution.
- Add an xUnit data-driven harness that loads scenarios and asserts outcomes.
- Seed initial Forestcraft scenarios (one per card) to validate the approach.

## Impact
- Impacted specs: card-scenario-tests
- Impacted code: Server.Tests/* (scenario runner + card scenarios), test utilities, data loading helpers
