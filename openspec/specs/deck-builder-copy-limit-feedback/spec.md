## Purpose

Provide deck-builder warnings and save feedback that are consistent with backend copy-limit validation.

## Requirements

### Requirement: Deck builder must load effective copy-limit policy by format
The deck builder SHALL retrieve and cache copy-limit policy for the currently selected `(environment, rule)` and use it for local warning rendering.

#### Scenario: Refresh policy after changing rule
- **WHEN** the user changes deck rule while editing a deck
- **THEN** the deck builder refreshes effective copy limits for the new `(environment, rule)` combination before computing warnings

### Requirement: Warning panel must list all copy-limit violations
When a deck has one or more copy-limit violations, the warning UI MUST show all violation reasons and MUST support multi-line display.

#### Scenario: Multiple violating cards show multi-line warnings
- **WHEN** two or more cards exceed their effective max copies
- **THEN** the warning panel renders each violation as a separate visible line

### Requirement: Save feedback must align with backend validation reasons
When save fails due to copy-limit validation, the UI SHALL present backend-provided reason text without dropping card-specific limit details.

#### Scenario: Save failure surfaces server reason
- **WHEN** backend returns a copy-limit validation error during save
- **THEN** the save feedback area shows the server reason including failing card and effective limit

### Requirement: Default visual behavior remains unchanged when no violations
If no copy-limit violations exist under current format policy, the new feedback flow MUST NOT introduce extra warning noise.

#### Scenario: No violation shows no additional warning
- **WHEN** all cards are within effective limits
- **THEN** no copy-limit warning message is displayed
