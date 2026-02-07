## MODIFIED Requirements

### Requirement: Guest Deck Session Storage
The system SHALL treat unauthenticated players as guests and SHALL store their deck data in server memory keyed by a guest id supplied by the client. The guest id SHALL remain stable across browser refreshes so that guest decks remain available until the server restarts or the guest id is cleared/reset.

#### Scenario: Guest refresh retains deck
- **GIVEN** a guest creates or edits a deck
- **WHEN** the guest refreshes the browser and reconnects with the same guest id
- **THEN** the deck remains available in the guest deck list

#### Scenario: Guest session ends
- **WHEN** the server restarts or the guest id is cleared/reset
- **THEN** the guest deck data is no longer available
