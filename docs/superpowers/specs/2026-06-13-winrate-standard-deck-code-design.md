# Standard Deck Code Export From Winrate Preview

## Goal

Allow the winrate dashboard deck preview to copy a deck code for standard decks reconstructed from historical match snapshots.

## Scope

- Support `DeckRule.Standard` only.
- Keep dual-class deck export disabled.
- Do not add database columns or migrate historical data.
- Generate codes on the server with the existing `IDeckCodeService`.

## Backend

`GET /api/devtools/winrate-stats/deck-composition` returns a nullable `deckCode` field. For a standard deck, the controller encodes the snapshot's class, environment, rule, and card counts as an `SVB2` code. For any non-standard rule, `deckCode` is null.

## Frontend

The winrate preview stores the returned code while the preview is open. The export button is enabled only when the code is non-empty. Clicking it copies the code to the clipboard and reports success or failure in the existing dashboard message area.

## Tests

- Controller test verifies a standard snapshot returns a decodable code with the expected metadata and cards.
- Controller test verifies a dual-class snapshot does not return a code.
- Jest tests verify code normalization and clipboard copy behavior.

