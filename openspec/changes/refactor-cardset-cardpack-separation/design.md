## Context
The project currently treats card_set, card pack, and pack environments as a single concept, which conflates deck legality with monetization metadata. This change separates those responsibilities while preserving existing editor workflows.

## Goals / Non-Goals
- Goals:
  - Card sets define environments and legality.
  - Card packs are purchasable products that reference exactly one card set.
  - card_set string input resolves to set name.
- Non-Goals:
  - Redesigning pack opening odds or economy balance.
  - Changing card ID generation or card art pipelines.

## Decisions
- Decision: Introduce CardSetDefinition as a first-class entity with abbreviation, region/sequence, and environments.
- Decision: CardPackDefinition references a single CardSetDefinition; packs do not carry environments.
- Decision: Card save auto-registers CardSetDefinition only; no CardPackDefinition auto-creation.
- Decision: Auto-registered CardSetDefinition uses the input string as Name and leaves Abbreviation blank.

## Risks / Trade-offs
- Migration complexity: existing card_set strings must map to abbreviations consistently.
- Compatibility: active changes that auto-register packs must be updated or superseded.

## Migration Plan
1. Create CardSetDefinitions table and link CardDefinitions to sets by matching card_set -> abbreviation (case-insensitive).
2. Add CardSetDefinitionId to CardPackDefinitions and populate from matching abbreviation or name (fallback rule to be defined).
3. Update deck legality to use CardSetDefinition.Environments.
4. Remove/ignore pack environments in runtime legality paths.

## Open Questions
- Do any existing pack definitions need a manual mapping to sets when abbreviations conflict?
