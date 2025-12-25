# Change: 全面重构元进度系统（用户/经济/任务）并与主游戏事件流集成

## Why
- 当前用户/经济/任务代码在“领域模型、EF 映射、服务层、主游戏循环”之间缺少一致的边界与落地链路，导致：
  - 对局玩家身份与持久化 `User.Id` 未建立稳定映射，无法可靠结算与推进任务。
  - EF 关系存在歧义（同一 `UserId` 同时作为 `Users` 与 `UserEconomies` 外键等），带来数据一致性与迁移风险。
  - 任务完成与奖励语义不一致（“完成即发奖”与 `IsRewardClaimed` 并存），难以维护与扩展。
  - 经济/任务与游戏主循环未联动，无法最大化复用既有事件驱动与事务提交管线（`GameActionTransactionService`）。

## What Changes
- 引入“元进度（Meta Progression）”的权威处理管线：在 **游戏事务提交完成后**（commit 阶段）基于已提交事件/快照进行统计与结算，统一推进：
  - 对局结算：胜负/场次/时长/排名积分/金币奖励/战斗统计落库。
  - 任务推进：将 `QuestType` 映射为可从领域事件推导的计数器与规则（如胜利、对战、造成伤害等）。
  - 奖励发放：采用可审计且幂等的奖励账本策略，避免重放/重试导致重复发奖。
- 明确并落地 **身份绑定**：对局中的 `playerId` 必须稳定关联到 `User.Id`（AI/游客等非持久化身份需显式标记）。
- 重构持久化模型与 EF 映射：消除歧义外键与影子外键，统一以 `UserId` 作为用户侧聚合锚点；补齐必要的唯一约束与索引以保障一致性与幂等。
- 增补对外接口与事件：提供获取/刷新元进度状态的 API，并在必要时通过 SignalR 向客户端推送经济与任务更新事件，减少轮询。

## Impact
- Affected specs (new): meta-progression
- Affected code (planned): `Server/Domain/Entities/*`（用户/经济/任务相关实体）、`Server/Infrastructure/Data/ApplicationDbContext.cs` 与迁移、`Server/Services/EconomyService.cs`（边界收敛/拆分）、`Server/Services/GameService.cs` 与 `Server/Services/GameActionTransactionService.cs`（结算与任务推进钩子）、相关 Controller/DTO/SignalR 事件。
- Data migration required: 是（需要修复/收敛外键与约束，并提供迁移与必要的数据修复脚本/逻辑）。

