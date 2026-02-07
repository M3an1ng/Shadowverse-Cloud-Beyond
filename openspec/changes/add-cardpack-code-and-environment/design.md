## Context
Card packs are referenced by a free-form string (card_set) and lack a structured identifier, abbreviation, or format scope. The economy system already models pack definitions but does not capture these attributes.

## Goals / Non-Goals
- Goals: Provide a normalized card pack identifier (region + sequence), abbreviation, and environment linkage using DeckEnvironment. Use pack definitions as the source of truth.
- Non-Goals: Redesign card rarity/odds, change purchase flows, or introduce new formats beyond existing DeckEnvironment values.

## Decisions
- Decision: Keep CardPackDefinition.Id as a stable internal key; add Region and Sequence fields and derive a 3-digit PackCode for display and ordering.
- Decision: Use DeckEnvironment for pack environment scoping to align with existing deck format logic.
- Decision: Keep card_set in API responses as a compatibility field derived from pack definition name.

## Risks / Trade-offs
- Risk: Legacy card_set values may not map to any pack definition; mitigate via a Legacy pack fallback and logging.
- Trade-off: Additional migration and mapping complexity in exchange for normalized identifiers.

## Migration Plan
1) Add new columns and constraints for CardPackDefinition and CardDefinition.
2) Seed card pack definitions (including legacy placeholder).
3) Backfill CardDefinition.CardPackDefinitionId from card_set using name/abbreviation match.
4) Update API/DTOs and front-end filters to use pack definitions.

## Open Questions
- Should PackCode be stored as a persisted column (int/string) or computed from Region/Sequence at runtime?
- How should unknown legacy card_set values be surfaced (fallback pack vs validation error)?
