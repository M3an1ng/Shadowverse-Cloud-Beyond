# Proposal: Replay Authority Uses Stable In-Game Ids (No Guid In Journal)

## Why
当前回放/调试注入/事件管线仍以 `Guid` 作为 RawEvent 与实体引用的核心标识（source/target、buff、card 等）。这带来两个关键问题：
- **跨进程离线回放不稳定**：进程重启后运行期生成的 Guid 无法与旧 Journal 对齐，导致事件引用无法重建。
- **身份语义混杂**：联机/房间/用户侧的 `UserId`（跨会话）与局内 `PlayerId`（座次/阵营）没有作为不同层级进行约束，容易在分配与回放中被误用。

本变更把“可稳定回放”作为第一目标，将 **Output RawEvent** 的权威标识从 Guid 迁移到 **稳定的局内 ID（InGameId）**，并明确 `UserId` 与 `PlayerId` 的职责边界。

## What Changes
- 定义 **Replay Authority Id Set**：离线回放所需最小稳定 ID 集合，以及哪些 ID 是调试指令高频需要手工输入的。
- 约束 Output RawEvent：**Journal 中禁止出现 Guid**；所有对象引用必须使用稳定 InGameId（带类别前缀，易手输）。
- 明确身份分层：
  - `UserId`：跨房间/跨会话稳定（联机/房间唯一用户标识）
  - `PlayerId`：局内稳定（座次/阵营，回放/事件管线使用的玩家标识）
- 定义迁移策略（schemaVersion）：支持一段时间读取旧 Guid Journal，但新写入仅输出 InGameId。

## Non-Goals
- 不要求一次性移除服务内部所有 Guid：允许内部使用运行期 Guid 做索引/性能优化，但 **不得进入 Output RawEvent**。
- 不在本变更中重写所有网络/数据库协议，只定义回放权威与事件引用的最小契约，并据此拆分后续任务。

## Impact
- 需要升级 RawEvent schema 与校验器/注册表/rehydrator（读写路径均受影响）。
- 需要为局内实体（Card/Buff/Player 等）建立可重建的稳定 ID 生成与解析策略。
- 需要在联机入口明确 `UserId -> PlayerId` 的映射，避免座次/阵营被错误复用。

