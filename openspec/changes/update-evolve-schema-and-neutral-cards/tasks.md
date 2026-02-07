## 1. Implementation
- [x] 1.1 Remove evolve_id from card data JSON (card_data.json, card_data_neutral.json) and update conversion output.
- [x] 1.2 Update DTOs/entities/mappings to drop EvolveId (CardDefinition, CardUpsertDto, CardDefinitionDto, CardDefinitionProfile).
- [x] 1.3 Add EF Core migration to drop CardDefinitions.EvolveId and update seeders/snapshots.
- [x] 1.4 Refactor CardInstance to stop using EvolveDefinitionId and align CanEvolve/evolution checks with fixed evolution rules.
- [x] 1.5 Update AI logic/tests/docs/editor UI that reference evolve_id.
- [x] 1.6 Update Detective's Magnifying Glass to Act-based activation (EventType.Act), replacing CountdownFinished.
