# Change: Add per-card scenario coverage

## Why
We converted multiple card databases into `card_data_*.json`, but only a subset has executable scenario tests. We need deterministic, per-card scenario coverage to validate that each converted card actually resolves its intended effects in real game flow.

## What Changes
- Add a scenario catalog document listing executable test setups for every converted card across all crafts.
- Define a requirement that every card in `card_data_*.json` must have at least one scenario test.
- Document scenario harness usage (run command, action lifecycle, choice keys, neutral example).
- Expand scenario suites to include every craft and every converted card.

## Impact
- Impacted specs: card-scenario-tests
- Impacted code: `Server.Tests/Scenarios/*`, `Server.Tests/CardScenarioTests.cs`, new per-craft scenario files
- Data dependencies: `data/card_data_*.json`
