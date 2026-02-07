## ADDED Requirements
### Requirement: Converted card data files per craft
The system MUST provide a converted card data JSON file per craft in the internal card_data format, with filenames `data/card_data_<craft>.json`.

#### Scenario: Forestcraft conversion output exists
- **WHEN** the Forestcraft database is converted
- **THEN** `data/card_data_forestcraft.json` exists and contains the converted cards

### Requirement: Converted data uses supported DSL and loads successfully
All converted cards MUST use the supported DSL syntax for triggers, conditions, targets, and effects, and MUST be loadable by the runtime parser without errors.

#### Scenario: Converted data passes DSL validation
- **WHEN** the conversion validation runs
- **THEN** all card abilities in the converted file parse without errors

### Requirement: Conversion verification after each craft
After each craft conversion, tests MUST be run to validate the converted data and ability parsing.

#### Scenario: Post-conversion validation
- **WHEN** a craft conversion is completed
- **THEN** the card data validation test suite is executed
