# Change: Add registered user accounts

## Why
Guest-only accounts prevent persistence across servers and make it hard to attach decks, resources, and match history to a stable identity.

## What Changes
- Add account registration with username + password.
- Add login flow for registered accounts.
- Persist account identity and bind resources/decks/match history to it across servers.
- Add a frontend registration entrypoint in user-info-card.

## Impact
- Affected specs: user-accounts (new)
- Affected code: Server authentication/persistence layers, SignalR session identity, frontend user-info-card UI
