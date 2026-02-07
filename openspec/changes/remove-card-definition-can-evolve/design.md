## Context
- Evolution no longer swaps card definitions and uses fixed +2/+2 rules.
- A BanEvolve keyword already exists and should act as the opt-out switch.

## Goals / Non-Goals
- Goals: Remove can_evolve from card definitions and derive evolvability from card type/keywords.
- Non-Goals: Change evolution point rules or introduce new evolution modes.

## Decisions
- Decision: Compute evolvability in CardInstance.CanEvolve using follower type and BanEvolve keyword.
- Alternatives considered: keep can_evolve on definitions (rejected due to redundant data and manual upkeep).

## Risks / Trade-offs
- Data migration required to drop the column and remove JSON fields.

## Migration Plan
1) Remove can_evolve from CardDefinition model/DTOs and adjust migrations.
2) Remove can_evolve from card data JSON and editor payloads.
3) Update tests and docs to reflect keyword-based restrictions.

## Open Questions
- None.
