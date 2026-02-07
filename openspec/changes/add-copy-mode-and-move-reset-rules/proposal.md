# Change: Add copy mode for card placement effects and move reset rules

## Why
Designers need to duplicate existing card instances (not just definitions) when resolving Summon/GetToken/AddDeck. Separately, card movement should follow consistent reset rules so stateful cards behave predictably when changing zones.

## What Changes
- Extend CardSourceMode with a Copy option for Summon/GetToken/AddDeck.
- Add a copy pathway (e.g., CopyCardEvent + CardFactory copy helper) to duplicate existing instances.
- Add reset rules for CardMovedEvent: some zone transitions preserve state, all others reset the card to a fresh instance.

## Impact
- Affected specs: effect-card-source, move-card-reset-rules
- Affected code: CardSourceMode, effect handlers, CardFactory, CardMovedEvent, tests.
- **BREAKING**: None (default behavior unchanged).
