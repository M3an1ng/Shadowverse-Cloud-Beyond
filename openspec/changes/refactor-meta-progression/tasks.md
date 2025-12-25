## 1. Design & Scope Lock
- [x] 1.1 明确“元进度”能力边界：对局结算、任务推进、奖励发放、排行/资料更新的职责拆分。
- [x] 1.2 确认与现有 replay/journal 的关系：是否使用 `Seq` 作为幂等游标，或以 `GameSession`/奖励账本做幂等。
- [x] 1.3 定义对外合约：API（查询/领取/刷新）与可选 SignalR 推送事件类型。

## 2. Data Model & EF Core Refactor
- [x] 2.1 修复歧义外键与影子外键：统一以 `UserId` 建立关系，避免 `UserEconomyId` shadow FK 与“双 FK”结构。
- [x] 2.2 添加幂等约束：对局结算、任务领奖、每日奖励、开包等关键路径的唯一约束/索引。
- [x] 2.3 编写迁移与数据修复：为既有数据提供可重复执行的迁移/修复脚本或启动时修复逻辑。

## 3. Service Layer Restructure
- [x] 3.1 拆分 `EconomyService`：将“余额/流水”“卡包”“任务”“排行”“每日奖励”按聚合拆分为可组合服务或模块，并用事务服务统一管理事务。
- [x] 3.1.1（实现）用 `ITransactionService` 统一事务边界，移除 `EconomyService` 内部嵌套 `BeginTransactionAsync`。
- [x] 3.2 引入 `IMetaProgressionService`：以事件序列为输入，输出状态更新与推送事件 DTO。（MVP：仅处理 GameEnd 结算）
- [x] 3.3 实现幂等存储：支持按 `GameId + Seq/EventId` 或 “每局结算记录”保证不会重复发奖/重复推进。（MVP：`GameSettlements`）

## 4. Game Loop Integration
- [x] 4.1 在 `GameActionTransactionService` commit 成功后调用元进度服务（广播前）。
- [x] 4.2 在“对局结束”建立单一结算入口：设置 `GameStatus.Finished`、写入 `GameSession`、更新 Profile/Ranking/Coins/Quests。
- [x] 4.3 确保 AI/非用户参与者不会污染用户进度（显式标记与过滤）。

## 5. API & Client Wiring (MVP)
- [x] 5.1 API：查询用户金币/任务/排行/每日奖励状态；领取任务奖励；领取每日奖励；（可选）开包与收藏更新。
- [x] 5.2 前端：在主菜单/个人页展示金币与任务入口；对局结束弹出结算与任务进度变化（可先用事件日志）。

## 6. Validation
- [x] 6.1 后端测试：幂等性（重复结算不重复发奖）、任务推进正确性（典型事件映射）、迁移后关系正确性。（MVP：结算幂等）
- [ ] 6.2 集成验证：打一局对战->结算落库->客户端看到更新（API/SignalR）。
