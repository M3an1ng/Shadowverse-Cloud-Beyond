## ADDED Requirements

### Requirement: AbilityTag MUST Be Represented As EffectKeyword
后端在能力配置与运行时能力中，能力标签 MUST 使用 `EffectKeyword`（而非自由 `string`），以确保与前端枚举源一致并提供类型安全。

#### Scenario: AbilityTag round-trips as enum name
- **GIVEN** a card ability is created/updated via API with `abilityTag: "Fanfare"`
- **WHEN** the server loads the ability configuration
- **THEN** the runtime ability MUST have `AbilityTag == EffectKeyword.Fanfare`
- **AND** the API/SignalR payloads MUST continue to serialize the tag as `"Fanfare"` (enum name string)

### Requirement: AbilityTag Storage MUST Remain Backward Compatible
数据库中的能力标签存储 MUST 兼容既有数据（TEXT/string），并在读取到未知值时提供安全降级策略。

#### Scenario: Unknown stored tag does not crash
- **GIVEN** an ability row contains an unknown `AbilityTag` string
- **WHEN** the server loads the ability
- **THEN** the server MUST NOT throw
- **AND** the tag MUST default to `EffectKeyword.None` (or another explicit fallback)

