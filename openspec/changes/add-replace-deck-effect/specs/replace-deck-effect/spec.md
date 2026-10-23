## ADDED Requirements

### Requirement: Replace Deck Effect Rebuilds Target Deck
The system SHALL provide a ReplaceDeck effect that replaces each targeted player's current deck with newly created card instances from an ordered list of card definition IDs.

#### Scenario: Replace a targeted player's deck with a new ordered list
- **WHEN** ReplaceDeck resolves for a targeted player with a non-empty replacement list
- **THEN** every card currently in that player's deck SHALL be moved to the `Removed` zone
- **AND** the player's deck SHALL be repopulated with newly created card instances whose base definition IDs match the provided list in the same order
- **AND** no original deck card SHALL remain in the player's deck after resolution

#### Scenario: Replace multiple targeted players independently
- **WHEN** ReplaceDeck resolves with a player scope that targets multiple players
- **THEN** each targeted player's existing deck SHALL be removed and rebuilt independently using the same provided replacement list

### Requirement: Deck Replacement Removal Is Not Vanish
The system SHALL treat removal caused by ReplaceDeck as a dedicated deck replacement move semantic rather than `Vanish`.

#### Scenario: Replaced deck cards do not emit vanish semantics
- **WHEN** a card is removed from a player's deck because ReplaceDeck resolved
- **THEN** the system SHALL record a dedicated deck replacement move reason for that card
- **AND** the system SHALL NOT emit a `Vanish` event for that card
- **AND** the system SHALL NOT apply `Vanish` resist or `LeaveField` routing to that card

### Requirement: Replace Deck Requires A Usable Replacement List
The system SHALL fail fast when ReplaceDeck is configured without a usable replacement list.

#### Scenario: Empty or invalid replacement list is rejected
- **WHEN** ReplaceDeck resolves with an empty definition ID list or a list that contains no positive definition IDs
- **THEN** the effect SHALL fail without changing any targeted player's deck
