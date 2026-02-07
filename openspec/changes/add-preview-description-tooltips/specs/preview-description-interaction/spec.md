## ADDED Requirements

### Requirement: Keyword references are interactive in preview descriptions
The system SHALL render keyword references in preview descriptions as interactive elements that open a keyword info tooltip.

#### Scenario: Keyword tooltip displays definition
- **WHEN** a preview description contains "【守护】" and the user clicks it
- **THEN** a tooltip shows the keyword name and its description
- **AND** the brackets remain visible but are not highlighted or clickable

#### Scenario: Keyword keeps numeric suffix while highlighting base
- **WHEN** a preview description contains "【吟唱_2】"
- **THEN** the rendered keyword text keeps "_2"
- **AND** only "吟唱" is highlighted and used for keyword lookup

#### Scenario: Keyword keeps non-numeric suffix while highlighting base
- **WHEN** a preview description contains "【爆能强化_X】"
- **THEN** the rendered keyword text keeps "_X"
- **AND** only "爆能强化" is highlighted and used for keyword lookup

#### Scenario: Keyword reference without metadata
- **WHEN** a keyword reference cannot be resolved
- **THEN** the tooltip shows a fallback message indicating missing metadata

### Requirement: Derived-card references are interactive in preview descriptions
The system SHALL render derived-card references in preview descriptions as interactive elements that open a derived-card detail image.

#### Scenario: Derived-card detail image displays
- **WHEN** a preview description contains "『妖精』" and the user clicks it
- **THEN** a centered detail image modal appears for the derived card

#### Scenario: Derived-card reference requires ID disambiguation
- **WHEN** a derived-card reference resolves to multiple cards with the same name
- **THEN** the UI shows a message asking the user to add an ID suffix (e.g., "『妖精（1010402）』")

#### Scenario: Derived-card reference without detail art
- **WHEN** a derived-card reference cannot resolve to a detail image
- **THEN** the UI shows a fallback message indicating the detail image is missing

### Requirement: Tooltip lifecycle in preview UI
The system SHALL allow users to dismiss preview tooltips.

#### Scenario: Dismiss tooltip
- **WHEN** a tooltip is open and the user clicks outside the tooltip or presses Escape
- **THEN** the tooltip closes and no preview tooltip remains visible

### Requirement: Preview description interactions apply to both preview contexts
The system SHALL support the interactive references in the battle card preview and the card editor preview.

#### Scenario: Battle preview interaction
- **WHEN** a user clicks a keyword reference in the battle preview
- **THEN** the tooltip displays in the battle UI

#### Scenario: Card editor preview interaction
- **WHEN** a user clicks a derived-card reference in the card editor preview
- **THEN** the tooltip displays within the editor UI
