## 1. Implementation
- [x] 1.1 Extend CardPackDefinition with Region, Sequence, Abbreviation, Environment, and derived PackCode.
- [x] 1.2 Add CardPackDefinitionId to CardDefinition and enforce FK relationship.
- [x] 1.3 Add migration for new fields, indexes, and constraints.
- [x] 1.4 Seed card pack definitions and backfill CardDefinition.CardPackDefinitionId from legacy card_set.
- [x] 1.5 Update DTOs and mapping logic to expose pack metadata and keep card_set compatible.
- [x] 1.6 Filter card generation by pack definition in EconomyService.
- [x] 1.7 Update card editor/deck builder pack filters to use pack definitions.
- [ ] 1.8 Add/adjust tests for pack code validation and environment filtering.
