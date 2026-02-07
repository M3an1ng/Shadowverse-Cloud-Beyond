## ADDED Requirements
### Requirement: Card Detail Modal Overlay
The system SHALL render a card detail modal overlay using the card-detail mockup layout and MUST NOT require a dedicated route.

#### Scenario: Open from deck builder
- **WHEN** the user opens card details from the deck builder
- **THEN** the card detail modal appears without a route change.

#### Scenario: Dismiss modal
- **WHEN** the user clicks the close control or presses Escape
- **THEN** the card detail modal closes and returns to the underlying screen.

### Requirement: Card Data Binding
The system SHALL bind card fields in the modal to card data, including name, class, type, cost, attack, defense, description, and preview art.

#### Scenario: Preview art present
- **WHEN** the card provides `artPreview`
- **THEN** the main art panel displays `artPreview`.

### Requirement: Inspect Detail Art
The system SHALL provide an inspect action that opens the detail art viewer using `artDetail` when available.

#### Scenario: Detail art available
- **WHEN** the user activates the inspect action and `artDetail` is available
- **THEN** the detail art viewer displays `artDetail`.

#### Scenario: Detail art missing
- **WHEN** the card has no `artDetail`
- **THEN** the inspect action is disabled or hidden.

### Requirement: Preview Entry Points
The system SHALL open the card detail modal from preview entry points (preview name interactions) and replace the legacy image-only modal.

#### Scenario: Open from preview name
- **WHEN** the user activates a preview name detail link
- **THEN** the card detail modal is shown for that card.

### Requirement: Ability Token Interactions
The system SHALL make ability text tokens interactive in the card detail modal, matching preview behavior for keywords and derived cards.

#### Scenario: Keyword tooltip
- **WHEN** the user clicks a keyword token in the detail modal
- **THEN** a keyword tooltip is shown for that token.

#### Scenario: Derived card jump
- **WHEN** the user clicks a derived card token in the detail modal and the token resolves to a different card
- **THEN** the detail modal opens for the derived card.

#### Scenario: Derived card is the current card
- **WHEN** the user clicks a derived card token that resolves to the same card shown in the detail modal
- **THEN** the detail modal remains on the current card.
