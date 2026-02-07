# Change: Convert external card databases to internal card data

## Why
Card data is currently stored in an external database format and must be converted into the internal card_data format so the game can load and execute abilities consistently.

## What Changes
- Convert each craft cardDatabase JSON into card_data_<craft>.json using the internal DSL format.
- Add validation coverage to ensure converted data loads and DSL expressions are valid.

## Impact
- Impacted specs: card-data
- Impacted code: data/card_data_*.json, Server.Tests/* (validation)
