## ADDED Requirements

### Requirement: Preview description highlights keyword markers
The system SHALL render text wrapped in 【】 inside card preview descriptions using the keyword highlight style.

#### Scenario: Keyword segment highlighted
- **WHEN** a card description contains "【疾驰】"
- **THEN** the preview renders "疾驰" with the keyword highlight style
- **AND** the surrounding brackets are not shown in the preview output

#### Scenario: Unmatched keyword marker falls back to plain text
- **WHEN** a card description contains an unmatched "【" or "】"
- **THEN** the preview renders the description without applying keyword highlight styles to that segment

### Requirement: Preview description highlights derived-card markers
The system SHALL render text wrapped in 『』 inside card preview descriptions using the derived-card highlight style.

#### Scenario: Derived-card segment highlighted
- **WHEN** a card description contains "『妖精』"
- **THEN** the preview renders "妖精" with the derived-card highlight style
- **AND** the surrounding brackets are not shown in the preview output

### Requirement: Highlight styles are reusable for future tooltip expansion
The system SHALL expose parsed spans (plain, keyword, derived-card) so the UI can attach tooltips in a later change without re-parsing.

#### Scenario: Token stream available to preview rendering
- **WHEN** a card description is parsed for preview rendering
- **THEN** the parser returns an ordered list of spans with type metadata and raw text