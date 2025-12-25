## ADDED Requirements

### Requirement: UserId And PlayerId MUST Be Distinct
联机/房间/用户身份 MUST 使用跨会话稳定的 `UserId`；局内事件/回放/座次 MUST 使用每局稳定的 `PlayerId`。系统 MUST NOT 在局内事件管线中使用 `UserId` 作为 `PlayerId`。

#### Scenario: Room join assigns a per-game PlayerId
- **GIVEN** two different users join the same room
- **WHEN** a game instance is created
- **THEN** each user MUST be assigned a distinct `PlayerId` (e.g., `P-1` and `P-2`)
- **AND** all subsequent game events MUST reference `PlayerId` (not `UserId`) in source/targets

