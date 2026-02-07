## ADDED Requirements
### Requirement: Convert “神眷的棋盘” card databases
The system MUST convert the “神眷的棋盘” source databases under `data/refer/archive/神眷的棋盘/` into the internal `card_data` format and output them to `data/神眷的棋盘/card_data_<craft>.json`, separate from existing card_data files.

#### Scenario: Forestcraft conversion includes new set cards
- **WHEN** the Forestcraft database for “神眷的棋盘” is converted
- **THEN** `data/神眷的棋盘/card_data_forestcraft.json` includes cards whose `card_set` is “神眷的棋盘”

### Requirement: Post-conversion validation per craft
After each craft conversion, the card data DSL validation and scenario tests MUST be executed.

#### Scenario: Validate after conversion
- **WHEN** a craft conversion completes
- **THEN** the validation suite is executed and reports no errors
