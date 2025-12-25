## Context
The frontend already provides deck selection and editing UI. The backend currently seeds test decks and has no persistent deck storage or deck code support. PlayerId is an in-game identifier and must remain distinct from UserId. Single-player matches require deck selection for both the human player and AI.

## Goals / Non-Goals
- Goals: Persist user decks with validation, support deck code import/export, and bind selected decks to in-game players at match start.
- Non-Goals: Authentication redesign, card crafting economy changes, or new matchmaking tiers.

## Decisions
- Decision: Store decks by UserId (not PlayerId). Match setup passes a mapping of seat -> UserId + DeckId (AI may use a system deck or a deck code). GameState uses PlayerId for in-game ownership only.
- Decision: Use a custom, versioned deck code format: `SVB1` prefix + base64url-encoded JSON payload `{ v:1, class:<CardClass>, cards:[[cardId,count], ...] }`. Cards are sorted by `cardId` to ensure deterministic export.
- Decision: Add a deck-selection step in match flow (e.g., `POST /api/match/{id}/deck` with playerId/token + deckId). For single-player with AI, the host also submits `aiDeckId` (or a deck code) before the match can start.
- Decision: Guests (no login) use in-memory deck storage scoped to the current session and are not persisted.
- Decision: Add a deck bootstrap operation to create a default deck for users/guests with no decks, using the server-defined starter list.
- Decision: Expose a deck availability check for the UI to block match entry when a player has zero decks.
- Decision: Add a deck environment attribute selected before deck editing; default environment allows all card sets and is used for card pool filtering alongside the deck class.

## Risks / Trade-offs
- Deck validation must stay consistent across API and match start to avoid desyncs.
- Introducing a deck-selection step adds more match states to handle and test.

## Migration Plan
1) Add new tables and migrations for decks.
2) Add deck service + API endpoints.
3) Integrate match deck selection and GameState deck building.
4) Update tests for validation and match flow.

## Open Questions
- Should deck code include deck name or only composition?
- Should AI deck always be a system deck, or can it reference a user-owned deck?
- Should deck import create a new deck or update an existing draft?
- How should environment rules be defined and distributed when multiple formats are enabled?
