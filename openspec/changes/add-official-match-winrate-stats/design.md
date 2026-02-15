## Context
- 胜率统计需要稳定、可追溯、可幂等。
- 当前系统已经在对局结束阶段计算“是否可获得金币”，这是一套在线生效的正式性规则。
- 若统计系统单独实现正式性判定，会带来规则重复与偏差。

## Goals / Non-Goals
- Goals:
  - 用统一口径判定正式对局并驱动胜率统计。
  - 仅统计正式对局。
  - 保持事实表字段最小化，正式性只存 `IsOfficial`。
- Non-Goals:
  - 本变更不引入非正式原因的持久化分析字段。
  - 本变更不改动金币规则本身，只复用其判定结果。

## Decisions
- Decision: 正式对局判定与金币资格完全对齐
  - 使用共享判定逻辑（由结算流程调用），输出布尔值 `IsOfficial`。
  - 语义等价为：`IsOfficial == coinRewardEligible`。

- Decision: 事实表只存 `IsOfficial`
  - `GameParticipantStatsFact` 仅持久化 `IsOfficial`。
  - 不新增 `CoinRewardEligible`、`NonOfficialReason` 等字段，避免冗余与口径分叉。

- Decision: 聚合更新只消费正式局
  - 结算时先写事实行（幂等键：`GameId + PlayerId`）。
  - 仅当 `IsOfficial=true` 时更新卡牌/卡组聚合统计。

- Decision: 按环境隔离统计
  - 所有聚合主键包含 `DeckEnvironment`，不同环境独立计算。

- Decision: 玩家级统计按职业聚合
  - 维护 `UserId + DeckClass` 维度聚合，记录 `TotalGames/TotalWins`。
  - 胜率由 `TotalWins / TotalGames` 计算，不额外冗余存储。

## Data Model
- `GameParticipantStatsFact`（每局每玩家一行）
  - 标识：`GameId`, `PlayerId`（唯一）
  - 结算信息：`IsWinner`, `EndedAtUtc`
  - 统计维度：`Environment`, `DeckShapeHash`, `UserDeckId`（可空）
  - 行为事实：`DrawnCardIds`, `PlayedCardIds`, `InitialDeckCardCounts`
  - 正式性：`IsOfficial`（唯一正式性字段）

- `CardStatsAgg`
  - 键：`Environment + CardDefinitionId`
  - 指标：`DeckGames/Wins`, `DrawnGames/Wins`, `PlayedGames/Wins`

- `DeckStatsAgg`
  - 键：`Environment + DeckShapeHash`
  - 指标：`TotalGames/Wins`

- `UserClassStatsAgg`
  - 键：`UserId + DeckClass`
  - 指标：`TotalGames/Wins`

## Flow
1. 对局结束（`GameEnd`）后进入统计结算。
2. 复用金币资格判定得到 `IsOfficial`。
3. 写入 `GameParticipantStatsFact`（幂等）。
4. 若 `IsOfficial=false`，结束；若 `true`，更新卡牌/卡组/玩家职业聚合。

## Risks / Trade-offs
- 不持久化非正式原因会降低离线诊断粒度。
- 收益是模型更简洁、规则更统一、维护成本更低。
