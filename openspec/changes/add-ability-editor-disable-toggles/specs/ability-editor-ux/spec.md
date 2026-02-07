## ADDED Requirements
### Requirement: Temporary Disable for Targets and Effects
The ability editor SHALL allow authors to temporarily disable individual targets and effects without deleting their configuration.

#### Scenario: Disabled target is ignored
- **WHEN** a target configuration is disabled
- **THEN** target resolution and playability checks ignore that target

#### Scenario: Disabled effect is skipped
- **WHEN** an effect configuration is disabled
- **THEN** the ability execution skips that effect

### Requirement: Header Toggle Controls
The ability editor SHALL expose a compact enable/disable control in each target and effect header.

#### Scenario: Toggle updates configuration
- **WHEN** the author toggles the header control
- **THEN** the corresponding target or effect `isEnabled` value updates in the saved configuration
