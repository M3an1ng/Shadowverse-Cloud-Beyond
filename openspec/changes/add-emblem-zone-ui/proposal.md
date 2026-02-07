# Change: Add Emblem Zone UI and Limits

## Why
Players need a dedicated emblem zone in the battle UI with clear capacity and visibility rules.

## What Changes
- Add an emblem zone to player stats with 5 visible slots and preview images.
- Enforce emblem uniqueness in AddEmblemEffect and discard over-capacity additions.

## Impact
- Affected specs: manage-emblem-zone
- Affected code: Server/Domain/Abilities/Effects/Handlers/AddEmblemEffectHandler.cs, Server/Domain/Abilities/Effects/Handlers/Parameters/AddEmblemEffectParams.cs, src/ui.js, src/styles/game.css, index.html.src