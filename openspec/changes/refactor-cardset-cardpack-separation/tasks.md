## 1. Implementation
- [x] 1.1 Add CardSetDefinition entity with abbreviation, region/sequence, and environment list.
- [x] 1.2 Add CardDefinition -> CardSetDefinition relationship and resolve card_set by name.
- [x] 1.3 Add CardPackDefinition -> CardSetDefinition one-to-one relationship and remove pack environments from legality paths.
- [x] 1.4 Migrate existing data: backfill CardSetDefinitions from card_set values and link CardDefinitions.
- [x] 1.5 Update card editor autocomplete and save flow to use CardSetDefinitions only, matching by name only.
- [x] 1.6 Update deck legality/environment checks to use card set environments.
- [x] 1.7 Update economy pack retrieval to include linked card set metadata.
- [x] 1.8 Add/update tests for set resolution, legality, and migration safety.
