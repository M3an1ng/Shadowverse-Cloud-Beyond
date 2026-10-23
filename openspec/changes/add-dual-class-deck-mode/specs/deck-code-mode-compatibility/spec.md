## ADDED Requirements

### Requirement: Deck code supports mode-aware encoding
The system SHALL support a deck code format version that can encode and decode `environment`, `mode`, primary class, optional secondary class, and card composition.

#### Scenario: Export dual-class deck code
- **WHEN** a valid dual-class deck is exported
- **THEN** the generated deck code preserves mode and secondary-class information for round-trip import

### Requirement: Legacy deck code compatibility
The system SHALL continue to decode legacy deck code formats and map missing format metadata to backward-compatible defaults.

#### Scenario: Import legacy deck code
- **WHEN** a client imports a legacy deck code without mode metadata
- **THEN** the system decodes it successfully and assigns default format values compatible with existing behavior

### Requirement: Import enforces decoded format validity
After decoding any deck code version, the system SHALL validate the decoded deck against the resolved `(environment, mode)` rule set before saving.

#### Scenario: Reject invalid decoded dual-class quota
- **WHEN** a decoded dual-class deck violates 24/16 quotas
- **THEN** the system rejects import with a validation error and does not persist the deck

#### Scenario: Reject malformed mode metadata
- **WHEN** a deck code contains unsupported or inconsistent mode metadata
- **THEN** the system rejects import with a decode/validation error
