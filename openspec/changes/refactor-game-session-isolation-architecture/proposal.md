# Change: Refactor game session isolation architecture

## Why
- 生产日志显示对局上下文获取失败与状态读取不一致并存，说明当前运行时存在“多状态源”与时序竞争风险。
- 部分私有对局信息被发送到对局公共组，存在信息泄露与“卡牌跨对局跳跃”误感知风险。
- 关键日志字段存在占位符错位，导致排障链路不可依赖，故障定位成本高。
- 现有架构缺少可灰度、可回滚、可量化验收的升级路径，无法在不停服前提下稳妥演进。

## What Changes
- 引入以 `GameId` 为边界的单写者会话模型：每个对局由顺序命令队列串行处理，禁止同局并发写。
- 建立统一状态源（SSOT）：移除并禁止第二份可变对局状态容器，`GameService`、`GameContextFilter`、Hub 全部经同一状态管理接口读写。
- 重构消息广播为可见性分层：公共消息仅发 `Game_<gameId>`；私有可见性（手牌可用性、私有 zone 细节）仅发 `Player_<playerId>_<gameId>`。
- 增加运行时护栏与审计：统一结构化日志字段、跨局不变量检测（`cardUid`/`entityId`）、异常广播审计、告警与自动回滚开关。
- 采用分阶段升级：先止血再替换内核，按指标门禁灰度推进，确保每阶段都可独立回滚。

## Impact
- Affected specs: `match-session-serialization`, `game-state-single-source`, `match-message-privacy`, `match-runtime-guardrails`
- Affected code (planned): `Server/Services/GameService.cs`, `Server/Hubs/GameHub.cs`, `Server/Hubs/Filters/GameContextFilter.cs`, `Server/Infrastructure/Persistence/*GameStateManager*`, SignalR group routing and logging pipeline
- Runtime impact: 命令处理路径、状态存储边界、实时广播通道与日志告警体系均会调整

