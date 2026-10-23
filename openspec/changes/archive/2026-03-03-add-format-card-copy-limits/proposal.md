## Why

当前每张卡的可携带上限在前后端都被固定为 3 张，无法按环境/规则组合快速执行禁卡、限 1、限 2 等平衡策略。每次调整都需要改代码并发布，风险高且响应慢。需要将“单卡携带上限”从硬编码升级为可配置规则，并确保构筑、导入、对战可用性判定全部一致。

## What Changes

- 新增“按 `(DeckEnvironment, DeckRule, CardDefinitionId)` 生效的单卡携带上限策略”，支持 `0~3`：
  - `0` 表示该组合下禁用该卡；
  - 未配置时默认回落为 `3`。
- 在后端统一校验链路中接入该策略，替代固定 `MaxCopiesPerCard=3` 判断，并输出可读错误原因（含卡牌与限制值）。
- 将该策略应用到所有依赖 `DeckValidationService` 的入口：
  - 卡组创建/更新、卡组码导入导出校验、开局选卡组与对局启动校验。
- 新增策略管理与查询接口（管理端/开发工具入口），支持读取、批量设置、删除（回落默认值）。
- 前端卡组编辑器改为基于同一策略显示 warning/保存提示，避免“前端可过、后端失败”或相反情况。
- 默认卡组自动生成逻辑改为遵守当前策略，避免生成后立即变成不可用卡组。
- 非目标：
  - 不改变对局内运行时卡牌效果；
  - 不引入按玩家、按段位的个性化禁限；
  - 不在本变更中扩展新环境/新规则枚举。

## Capabilities

### New Capabilities
- `format-card-copy-limit-policy`: 定义并管理按环境+规则生效的单卡携带上限策略（含禁用语义与默认回落）。
- `deck-copy-limit-validation`: 在统一构筑校验中执行动态限张规则，并向调用方返回结构化失败原因。
- `deck-builder-copy-limit-feedback`: 在卡组编辑 UI 中展示与后端一致的限张警告与保存失败原因。
- `default-deck-copy-limit-compliance`: 默认卡组生成遵守当前限张策略，不产出策略违规卡组。

### Modified Capabilities
- None. (No baseline specs currently exist under `openspec/specs/`; this proposal introduces new baseline capabilities.)

## Impact

- Affected backend areas:
  - `Server/Services/DeckValidationService.cs`
  - `Server/Services/DeckService.cs`（默认卡组构建与相关校验调用）
  - 新增禁限策略实体/仓储/控制器与对应 EF 配置、迁移（`ApplicationDbContext` 与 `Infrastructure/Persistence/Migrations`）
- Affected frontend areas:
  - `src/deckBuilder.js`（本地 warning 与保存提示逻辑）
  - 可能新增/调整 devtools 配置页面的数据读取与提交
- API impact:
  - 新增禁限策略管理接口；
  - 现有 deck 相关接口错误消息会新增“超过该环境/规则下卡牌上限”的原因文本。
- Data/storage:
  - 新增策略表与唯一索引（`Environment + Rule + CardDefinitionId`）。
- Compatibility:
  - 未配置策略的组合保持现状（默认 3 张），对旧数据兼容。
- Testing:
  - 增加后端校验用例（限 0/1/2/3 与默认回落）；
  - 增加前后端一致性回归用例（warning 与保存错误覆盖一致）。
