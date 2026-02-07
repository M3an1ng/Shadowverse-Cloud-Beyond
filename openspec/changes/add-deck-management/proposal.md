# Change: Add deck management and match deck selection

## Why
The frontend already exposes deck editing UI, but there is no backend to store decks, enforce rules, or feed decks into matches. This change adds user-scoped deck persistence, deck code import/export, and match setup that binds selected decks to in-game player IDs.

## What Changes
- Add persistent user decks with CRUD, validation (40 cards, max 3 copies, class rules), and rename.
- Add deck environment + class selection during deck creation and persist environment metadata.
- Filter deck editor card pools by environment rules and deck class (Neutral + class only).
- Add deck code export/import using a custom, versioned format.
- Extend match setup to accept deck selections for host/guest and AI, and build game state decks from those selections.
- Add default deck bootstrap for users/guests and a deck availability check for match entry gating.

## Impact
- Affected specs: deck-management, match-deck-selection
- Affected code: Server/Domain (new deck entities), Server/Infrastructure/Data (EF config + migrations), Server/Services (deck service, deck code service), Server/Controllers (deck API, match flow updates), Server/Services/GameService.cs (deck initialization), src/deckBuilder.js (deck editor filtering)
