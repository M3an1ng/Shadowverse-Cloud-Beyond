# Design: Card Editor MVP 端到端链路

## Goals
- 让主界面“卡牌工坊/卡牌编辑器”具备最小可用闭环：卡牌与能力可读可改可持久化。
- 不依赖“占位/模拟响应”来驱动 UI；API 返回应真实反映数据库状态。
- 避免引入破坏性全局契约变更：通过前端归一化或局部 DTO 约束来解决枚举大小写/序列化不一致。

## Non-Goals
- 不在本变更中实现完整资源（Assets）管理。
- 不在本变更中引入复杂权限/协作编辑/版本控制等高级编辑器功能。

## API Contract (MVP)

### Cards
- `GET /api/cards`：返回 `CardDefinitionDto[]`（已存在）
- `GET /api/cards/{id}`：返回 `CardDefinitionDto`（已存在）
- `POST /api/cards`：创建卡牌（已存在，确认请求 DTO 与前端字段对齐）
- `PUT /api/cards/{id}`：更新卡牌（已存在）
- `DELETE /api/cards/{id}`：（可选）删除卡牌

### Abilities
- `GET /api/abilities`：返回能力列表（已存在）
- `GET /api/abilities/{id}`：返回能力详情（已存在）
- `POST /api/abilities`：创建能力（当前为模拟，需要落库）
- `PUT /api/abilities/{id}`：更新能力（已存在，确认 `jsonConfig` 保存策略）

### Card <-> Ability Link
- `GET /api/cards/{cardId}/abilities`：返回该卡牌关联的能力列表（当前为占位，需要返回真实数据）
- `POST /api/cards/{cardId}/abilities/{abilityId}`：将能力关联到卡牌（需要新增）
- `DELETE /api/cards/{cardId}/abilities/{abilityId}`：解除关联（需要新增）

## Enum/Casing Strategy
- 现状：后端全局 `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` 会输出 `follower` 等；而编辑器 UI/本地化键/大量前端逻辑使用 `Follower`。
- MVP 策略：前端在进入编辑器状态时对枚举值做归一化（case-insensitive），并在提交回后端时使用后端可解析的字符串（建议用原始枚举名；后端解析应不区分大小写）。
- 本地化接口 `GET /api/localization/enums/{EnumName}` 的 key/value 作为 UI 下拉选项来源，前端需能映射当前值到选项 key。

## Data Model Notes
- 卡牌的能力关联以 `CardDefinition.AbilityIds` 为权威来源；能力实体从 `CardAbilities`（或等价表）读取。
- `CardsController.GetCardAbilities` 需要从 AbilityIds 拉取能力实体并返回所需字段（至少：`id/name/description/abilityTag/abilityType/priority/isActive/jsonConfig`）。

