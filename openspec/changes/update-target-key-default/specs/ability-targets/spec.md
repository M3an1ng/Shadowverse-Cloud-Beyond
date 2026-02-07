## ADDED Requirements
### Requirement: Single target key uses target1
When target configurations are supplied as an array with a single entry, the system SHALL map the target to the key `target1`.

#### Scenario: Parse single target array
- **WHEN** a target configuration array contains exactly one target
- **THEN** the resulting dictionary uses the key `target1`