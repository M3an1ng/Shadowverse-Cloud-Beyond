# Proposal: Ability-Related Guid Stability For Cross-Process Replay

## Why
当前 Ability 系统内部存在多类 `Guid`（`Ability.RuntimeId`、`Effect.Id`、`PlayerChoiceRequest.PendingChoiceId`、`Buff.Id/SourceAbilityId` 等），其语义混杂，容易在“回放/调试/跨进程重建”场景中被误用：
- 有些用于 **运行期关联/索引**（例如 Buff 反查来源 Ability），天然不需要跨进程稳定。
- 有些用于 **客户端交互恢复**（PendingChoiceId），只需要在同一局/同一服务进程内可靠。
- 也存在“无来源能力”的系统路径被迫生成随机 Guid（例如系统路径创建 Buff 时 `Guid.NewGuid()` 伪造来源），使日志/诊断噪声更大。
- 进一步地，若未来目标升级为 **离线回放/跨进程重建**，需要明确哪些 Guid 必须可重放（稳定/可注入），哪些必须保持为运行期临时标识（不进入回放权威）。

本变更旨在：
1) 明确 Ability 相关 Guid 的 **运行期 vs 可重放** 边界；
2) 给出“跨进程稳定”所需的最小改造指引（尤其是 Buff/Ability/Effect 关联）；
3) 为“无来源 Ability”的系统路径提供明确的 No-Source 标识。

## What Changes
- 输出“Guid 稳定性矩阵”（哪些需要跨进程稳定、哪些必须运行期临时、替代方案是什么）。
- 增加“局内可读 ID”要求：当需要替换/补充 `Guid` 作为回放/调试依赖时，新 ID 必须具备可规律生成、易手输、且可从格式上识别对象类别（便于测试与日志分析）。
- 明确回放/录制的权威依赖：
  - **事实事件（Output RawEvent）** + 稳定身份（例如 `Ability.DefinitionId`、卡牌定义 ID、玩家 ID）
  - 避免把运行期临时 Guid 当作回放依赖（尤其是 `Ability.RuntimeId`、`Effect.Id`、`PendingChoiceId`）。
- 统一 “No-Source” 语义：
  - 允许 `Buff.SourceAbilityId == Guid.Empty` 表示 **System/No-Source**；
  - `Buff.SourceAbility` 在该情况下必须为 `null`；
  - 系统路径不再制造随机来源 Guid。
- 为未来“跨进程重建”列出实现任务：
  - Buff 实例标识（`Buff.Id`）与 Ability 来源标识（当前 `RuntimeId`）在跨进程重建中如何保持一致（例如：可注入 ID 生成器、或替换为稳定键）。

## Non-Goals
- 不在本变更中一次性实现完整“跨进程离线回放”的全量确定性 ID 体系（只定义边界与最小落地路径）。
- 不在本变更中替换 `PlayerId` 等稳定身份 Guid。
- 不在本变更中改变现有 ability/target/queue 的业务行为（仅语义与约束更清晰）。

## Impact
- 兼容性：`Buff.SourceAbilityId` 的默认值语义变化可能影响少量依赖“非空即有来源”的代码路径，需要通过单测覆盖。
- Debug/诊断：系统路径不再制造随机来源 GUID，日志更干净，定位更准确。
- 为后续跨进程回放提供明确的改造清单，避免“盲目替换所有 Guid”导致屎山。
