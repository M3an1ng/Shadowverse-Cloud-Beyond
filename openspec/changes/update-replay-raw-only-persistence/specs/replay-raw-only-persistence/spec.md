## ADDED Requirements
### Requirement: Replay journal MUST support raw-only persistence mode
The system MUST support a replay persistence mode where committed replay rows are persisted with `RawEventJson` as the canonical payload while omitting `ClientDtos` by default.

#### Scenario: Raw-only mode skips DTO persistence
- **WHEN** replay persistence mode is configured as raw-only
- **THEN** newly written replay rows include `RawEventJson` and metadata, and do not persist `ClientDtos`

### Requirement: Replay catch-up and seek MUST work without persisted ClientDtos
Replay read operations MUST remain functional when `ClientDtos` are absent by projecting outbound replay messages from `RawEventJson`.

#### Scenario: CatchUp on raw-only rows
- **WHEN** a client requests catch-up for records with empty `ClientDtos` and populated `RawEventJson`
- **THEN** the server projects replay messages from raw events and sends an ordered stream equivalent to recorded facts

#### Scenario: Seek on raw-only rows
- **WHEN** a client requests seek to `targetSeq` on raw-only replay data
- **THEN** the server restores snapshot and replays projected raw events from `snapshotSeq+1` to `targetSeq`

### Requirement: Replay reconstruction MUST be apply-only
Replay reconstruction MUST NOT re-run domain `Commit()` execution for recorded events.

#### Scenario: Parent and child recorded events are not duplicated during replay
- **WHEN** replay data includes both a parent event and its committed child events in sequence
- **THEN** reconstruction applies each recorded event once by sequence and does not generate additional child events

### Requirement: Legacy replay records MUST remain readable
The system MUST remain backward compatible with legacy replay rows that include persisted `ClientDtos`.

#### Scenario: Legacy DTO rows still replay
- **WHEN** replay read encounters rows containing legacy `ClientDtos`
- **THEN** the system can continue serving replay using legacy DTO payloads or equivalent projection without breaking clients
