# Design: add-devtools-match-lab

## Overview
Provide a Devtools Match Lab entry and a devtools-only API that starts AI or local two-player matches with configurable starting parameters. The override only applies to devtools flows.

## Backend Design
- DTO: `DevMatchConfig`
  - Example fields: `startingPp`, `maxPp`, `startingHp`, `startingHandSize`, `ppBoost`, `seed`
- API: `POST /api/devtools/matches/start`
  - Input: both decks, mode (AI/Local), `DevMatchConfig`
  - Auth: devtools/login only
- Injection point: apply `DevMatchConfig` during match initialization
  - Ensure normal matches are unaffected

## Frontend Design
- Add a Match Lab tool-card to devtools home
- Route: `/dev-tools/match-lab`
- Page UI:
  - Mode selection (AI / local two-player)
  - Deck selectors for both sides
  - Parameter form (starting PP, max PP, starting HP, starting hand size, seed)
  - Presets (standard / high cost / high HP)
- On success, navigate to the match

## Validation
- Server-side range validation
- Client-side min/max input constraints

