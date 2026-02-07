# Change: Update guest deck retention across browser refresh

## Why
Guest decks are specified as temporary session storage, but players expect a browser refresh to keep the same guest id and deck list until the server restarts.

## What Changes
- Clarify that guest decks remain available after browser refresh when the same guest id is reused.
- Define guest session end conditions as server restart or guest id reset/clearing.
- Keep guest decks in non-persistent server memory (no database persistence).

## Impact
- Affected specs: deck-management
- Affected code: src/userSession.js, Server/Services/GuestDeckStore.cs, Server/Services/DeckService.cs
