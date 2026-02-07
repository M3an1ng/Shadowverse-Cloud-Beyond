## ADDED Requirements

### Requirement: Card art assets can be saved as optional preview/detail images
The system SHALL allow a card definition to store optional artPreview and artDetail image paths.

#### Scenario: Upload preview art
- **WHEN** a user uploads a preview image for a card
- **THEN** the system MUST store the file and persist the artPreview path

#### Scenario: Upload detail art
- **WHEN** a user uploads a detail image for a card
- **THEN** the system MUST store the file and persist the artDetail path

### Requirement: Preview and deck builder use the stored art paths

#### Scenario: Card editor preview uses artPreview
- **GIVEN** a card definition has artPreview
- **WHEN** the card preview renders
- **THEN** the preview MUST display the artPreview image

#### Scenario: Deck builder opens artDetail on demand
- **GIVEN** a card definition has artDetail
- **WHEN** a user clicks the detail action in deck builder
- **THEN** the detail image MUST open in a modal view
