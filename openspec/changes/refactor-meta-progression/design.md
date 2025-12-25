# Design: Meta Progression（用户/经济/任务）与主游戏事件流集成

## Goals
- **高效**：复用既有“事务提交 -> 事件链 -> 广播”管线，在 commit 成功后一次性计算与落库，避免分散的数据库写入与重复统计。
- **健壮**：对重试/重放/断线补流具备明确的幂等策略；数据模型无歧义外键；失败可诊断。
- **可维护**：将“对局规则（Domain）”与“元进度规则（Meta）”解耦，以映射层/适配器将领域事件投影为统计信号，避免污染核心战斗逻辑。

## Non-goals (for this change)
- 不引入复杂付费/IAP、真实掉率与稀有度分布等商业化逻辑（卡包开包可先以“可替换策略接口”落地）。
- 不引入完整账号安全体系（如 OAuth、密码重置、邮箱验证流程）；本变更聚焦“稳定身份 + 元进度落地链路”。

## Architecture Overview
### Key Components
- `IMetaProgressionService`
  - 输入：`GameId`、参与者 `UserId` 列表、已提交事件序列（或 bundle/seq 范围）、可选快照/派生统计。
  - 输出：更新后的“元进度视图”（金币/任务/排名/资料等）以及需要推送给客户端的轻量事件 DTO。
- `IProgressionSignalExtractor`
  - 将领域事件（`IGameEvent`）投影为可累加的信号（例如：`GamesPlayed += 1`、`DamageDealt += 5`、`SpellsPlayed += 1`）。
  - 仅依赖事件的稳定字段/标识，不依赖运行态引用对象。
- `IProgressionIdempotencyStore`（可用现有表与唯一约束实现）
  - 记录某个 `GameId`/`Seq`（或 `EventId`）是否已被用于元进度结算，保证重复调用不会重复发奖。

### Hook Point（与主循环结合）
- 在 `GameActionTransactionService.CommitTransactionAsync` 完成对领域事件的递归 commit 后、广播前，拿到 `processedEvents` 与（可选）replay `Seq` 信息，调用 `IMetaProgressionService`：
  - 统一写库（同一事务/或显式 outbox 策略）
  - 产生并广播 `EconomyUpdated`/`QuestUpdated` 等事件（如需要）

## Data Model Principles
- `UserId` 是用户侧事实锚点：经济、任务、排名、每日奖励、对局会话都以 `UserId` 建立关系。
- 禁止同一列同时指向多个聚合（例如同时外键 `Users(UserId)` 与 `UserEconomies(Id)`）。
- 幂等必须由数据层保障：通过唯一索引/约束（例如“每用户每局结算一次”、“每任务奖励只可领取一次”）避免逻辑漏洞。

## Quest Progression Strategy
- 任务推进来源：
  - 对局内：由领域事件投影得到（出牌、召唤、伤害、进化、胜负、场次等）。
  - 对局外：登录/开包等通过经济系统接口产生信号。
- 奖励策略：
  - “完成”与“领取”分离：完成只产生 `IsCompleted=true`，领取才生成金币流水与设置 `IsRewardClaimed=true`。
  - 若保留“自动发放”模式，必须通过幂等账本确保只发一次，并与 `IsRewardClaimed` 语义一致（自动发放即视为领取）。

## Observability
- 记录关键指标：结算耗时、写库耗时、每局任务推进数、幂等命中率、失败原因分布。

