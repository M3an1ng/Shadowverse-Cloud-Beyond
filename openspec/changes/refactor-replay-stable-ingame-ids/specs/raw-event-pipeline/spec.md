## ADDED Requirements

### Requirement: Output RawEvent MUST Use Stable In-Game Ids (No Guid)
Output RawEvent（Journal 权威事实）中所有对象引用（source/targets/以及 payload 内的实例引用字段）MUST 使用跨进程稳定的 InGameId；Output RawEvent MUST NOT 写入任何 Guid。

#### Scenario: Journal facts are guid-free
- **GIVEN** a committed event is recorded to the replay journal
- **WHEN** the server generates Output RawEvent
- **THEN** `sourceId/targetIds` MUST be InGameId strings (e.g., `P-1`, `C-000123`)
- **AND** any payload fields that reference instances MUST also use InGameId
- **AND** the system MUST reject writing Output RawEvent if any Guid is present

### Requirement: InGameId MUST Have A Canonical Format And Be Strictly Validated
InGameId MUST have a canonical, human-friendly format for Output RawEvent, and the system MUST validate it when writing Output RawEvent.

#### Scenario: Output uses canonical ids
- **GIVEN** an Output RawEvent is being written to the journal
- **WHEN** the event references a player/card/buff instance
- **THEN** `PlayerId` MUST use `P-<positive-int>` without leading zeros (e.g., `P-1`)
- **AND** `CardInstanceId` MUST use `C-<6-digit>` (e.g., `C-000001`)
- **AND** `BuffInstanceId` MUST use `B-<6-digit>` (e.g., `B-000001`)

#### Scenario: Invalid ids are rejected
- **GIVEN** an Output RawEvent is being written to the journal
- **WHEN** any referenced id is not a valid InGameId
- **THEN** the system MUST reject the write and emit a diagnostic that includes the expected format

### Requirement: InGameId MUST Be Replay-Rebuildable And Human-Friendly
InGameId MUST be replay-rebuildable (stable across processes for the same journal) and MUST be human-friendly for debug commands and logs (规律生成、可手工输入、可识别类别前缀)。

#### Scenario: Debug inject uses the same id model as replay
- **WHEN** a tester injects an Input RawEvent via debug console
- **THEN** the command MUST accept InGameId for source/targets (e.g., `C-000001`)
- **AND** the injected event MUST be recordable as Output RawEvent without translating to Guid

### Requirement: RawEvent Schema MUST Be Versioned And Support A Guid Read-Compatibility Window
The journal MUST be versioned so the system can distinguish Guid-based legacy events from InGameId-based events. The system MUST provide a time-limited, best-effort read compatibility window for legacy Guid journals.

#### Scenario: New writes are versioned and guid-free
- **GIVEN** the system writes a new Output RawEvent to the journal
- **THEN** the event MUST declare a schema version that represents the InGameId-based format
- **AND** the event MUST remain guid-free

#### Scenario: Legacy guid journal is read via best-effort mapping
- **GIVEN** a legacy journal entry that uses Guid references
- **WHEN** the rehydrator reads it within the compatibility window
- **THEN** the system MUST attempt to map each Guid reference into an InGameId
- **AND** the system MUST reject replay if any required mapping cannot be produced, with a clear diagnostic
