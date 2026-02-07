# Change: Update Evolve Schema And Neutral Card Data

## Why
The current game rules no longer use evolve-id based card replacement, but the data, schema, and tooling still expose evolve_id. This causes mismatches between runtime behavior and card definitions, and blocks completing the neutral card data conversion.

## What Changes
- Remove evolve_id from card data JSON and DTO/editor schema (**BREAKING**).
- Stop relying on EvolveId/EvolveDefinitionId in runtime entities; evolution uses fixed +2/+2 and immediate attack.
- Update the neutral card data so Detective's Magnifying Glass uses the Act ability instead of CountdownFinished.
- Update database schema, mappings, seeders, and tests to align with the new structure.

## Impact
- Affected specs: card-data
- Affected code: data/card_data_neutral.json, data/card_data.json, Server/Domain/Entities/CardInstance.cs, Server/Domain/Entities/CardDefinition.cs, Server/Infrastructure/Persistence, Server/Server/Infrastructure/Data, Server/Dtos, src/cardEditor.js, Server/Services/AiService.cs, tests and docs referencing evolve_id.
