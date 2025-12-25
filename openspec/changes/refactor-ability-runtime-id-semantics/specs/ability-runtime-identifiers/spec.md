## ADDED Requirements

### Requirement: Ability Runtime Guid MUST Be Runtime-Only
Ability 系统中的运行时关联标识（例如 `Ability.RuntimeId`、`Effect.Id`、`PlayerChoiceRequest.PendingChoiceId`）仅用于同一局/同一进程的内存关联，MUST NOT 被当作回放/存档/跨进程重建的权威依据。

#### Scenario: Runtime ids are not replay authority
- **GIVEN** a replay journal exists for a game
- **WHEN** the system writes committed events
- **THEN** the replay authority MUST be `Output RawEvent` (facts) plus stable identities (e.g., ability definition id, card definition id, player id)
- **AND** runtime-only guids MUST NOT be required to rebuild facts

### Requirement: Buff Instance Id MUST Be Replay-Stable When Referenced
若后续事实事件需要引用某个 Buff 实例（例如移除/替换），系统 MUST 提供跨进程可重建的 Buff 实例标识，并在事实事件（Output RawEvent）中记录并引用该标识。

#### Scenario: BuffApplied/BuffRemoved reference the same buff id
- **GIVEN** a buff is applied and later removed in the same game
- **WHEN** the system records facts to `Output RawEvent`
- **THEN** the applied buff MUST have a `buffId`
- **AND** the later removal MUST reference the same `buffId`
- **AND** replay MUST be able to register and remove the same buff instance using that `buffId`

### Requirement: In-Game Readable Ids MUST Be Human-Friendly
当系统需要为局内对象提供可读的实例标识（例如卡实例、Buff 实例），该标识 MUST 同时满足调试/日志可用性：规律生成、可手工输入，并能从格式上识别对象类别。

#### Scenario: Human-friendly ids are used in logs and debug commands
- **GIVEN** the game is running
- **WHEN** the server logs a stateful object reference (card/buff/etc.)
- **THEN** the log MUST include a human-friendly id with an object-type prefix (e.g., `C-000123`, `B-000045`)
- **AND** the log MAY include the underlying guid as supplemental information
- **AND** debug commands MUST accept the human-friendly id input in addition to guid input

### Requirement: System Buff MUST Use Explicit No-Source Marker
当 Buff 不由任何 Ability 实例产生时，系统 MUST 以明确标记表示“无来源能力”，而不是生成随机 `Guid` 伪造来源。

#### Scenario: System buff has explicit no-source marker
- **GIVEN** a buff is created in a system path without an active ability context
- **WHEN** the buff is constructed
- **THEN** the buff MUST set `SourceAbilityId = Guid.Empty` as the No-Source marker
- **AND** `Buff.SourceAbility` MUST be `null`

### Requirement: Buff Consumers MUST Treat Guid.Empty As No-Source
所有消费方（日志、叠加规则、移除规则、查询）MUST 将 `SourceAbilityId == Guid.Empty` 视为 No-Source，并不得尝试反查来源 Ability 实例。

#### Scenario: Consumers do not resolve no-source ability
- **GIVEN** a buff with `SourceAbilityId == Guid.Empty`
- **WHEN** the system evaluates or logs the buff
- **THEN** the system MUST NOT attempt `GetAbilityByRuntimeId(Guid.Empty)`
- **AND** the output MUST clearly indicate the buff has no source ability
