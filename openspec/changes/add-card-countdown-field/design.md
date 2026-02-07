## Context
Countdown currently piggybacks on Health for amulets (and some crests). This conflates two different concepts and makes it harder to validate or evolve card data.

## Goals / Non-Goals
- Goals:
  - Separate countdown from health at the data and runtime levels.
  - Preserve existing countdown gameplay behavior after migration.
  - Keep countdown optional and explicit in card data.
- Non-Goals:
  - Redesign countdown rules or tick cadence.
  - Change ability trigger timing or effect resolution.

## Decisions
- Add CardDefinition.Countdown (nullable int) as the single source of truth for countdown values.
- Remove the implicit mapping from Health to countdown.
- Initialize countdown only when the card has the Countdown keyword and Countdown is provided.

## Risks / Trade-offs
- Breaking data schema: existing amulets/crests must be updated.
- Requires UI/DTO updates to display countdown separately from health.

## Migration Plan
1. Add Countdown to data schema and DTOs.
2. Update countdown initialization to use Countdown.
3. Update card data JSON to set Countdown values for all Countdown cards.
4. Validate countdown behavior with existing tests plus targeted countdown cases.
