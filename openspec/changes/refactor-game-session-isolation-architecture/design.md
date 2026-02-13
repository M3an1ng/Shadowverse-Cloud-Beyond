## Context
当前线上问题集中在三个维度：
1. 状态源分裂：运行时存在不止一个可变状态容器，导致上下文读取与命令处理看到的状态不一致。
2. 可见性边界不清：部分仅应对本玩家可见的数据通过对局组广播，造成隐私/公平性风险。
3. 观测信号失真：关键日志字段错位，跨链路排障困难，难以快速判断是否真实跨局污染。

## Goals / Non-Goals
- Goals:
  - 在不停服条件下实现“单局单写者 + 单一状态源 + 严格可见性边界”。
  - 建立可量化门禁（错误率、上下文失败率、违规广播数）与自动回滚能力。
  - 为后续事件溯源与回放体系预留兼容路径。
- Non-Goals:
  - 本提案不要求一次性引入分布式 Actor 集群。
  - 本提案不覆盖卡牌 DSL 语义改造。
  - 本提案不改变对战规则本身。

## Decisions
- Decision: 以 `GameId` 建立会话单写者（in-process actor/queue）
  - Reason: 直接消除同局并发写导致的顺序竞争，收益最大且可渐进迁移。
  - Alternatives considered:
    - 继续依赖锁分散在各服务：实现碎片化且容易漏锁。
    - 全量上分布式 Actor 平台：收益高但迁移成本与复杂度超出当前窗口。

- Decision: 统一状态源到 `IGameStateManager`（或等价抽象），禁止双写
  - Reason: 从根因上消除 split-brain 与读取漂移。
  - Alternatives considered:
    - 保留双缓存并做同步：同步时序复杂，仍有一致性窗。

- Decision: 消息按 Public/Private 显式分层并做字段白名单
  - Reason: 通道与载荷双保险，防止私有字段误发到公共组。
  - Alternatives considered:
    - 仅靠调用约定：人肉约束不可靠，回归风险高。

- Decision: 引入结构化日志+不变量检测+特性开关门禁
  - Reason: 支持线上快速判责、灰度放量与失败自动回滚。

## Architecture Overview
- Command ingress: Hub/API 仅接收请求并附带 `gameId`, `playerId`, `clientCommandId`。
- Session router: 按 `gameId` 路由到 `GameSessionActor`，同局命令串行执行。
- State access: Actor 内通过统一状态仓读取/写入，写入要求版本单调递增。
- Event fanout: 根据事件可见性标签路由到 `Game_<gameId>` 或 `Player_<playerId>_<gameId>`。
- Guardrails: 广播前执行 payload lint；提交后执行跨局不变量检查并记录审计事件。

## Migration Plan
### Phase 0 - 线上止血与观测修复（1-3 天）
- 修复关键日志字段错位；统一 `gameId/playerId/cardUid/commandId/seq` 字段。
- 将已知私有事件改为玩家组广播；新增违规广播告警。
- 门禁指标：
  - `cannot_create_game_context` 降至 0
  - `private_payload_to_game_group` 降至 0

### Phase 1 - 状态源统一（3-5 天）
- 移除 `GameService` 内部第二状态字典，全部改为统一状态管理接口。
- `GameContextFilter` 与命令执行读同一来源，加入版本号校验。
- 灰度策略：按房间 ID 哈希 10% -> 50% -> 100%。

### Phase 2 - 单写者会话队列（5-8 天）
- 引入 `GameSessionActor`，所有写命令改为入队串行执行。
- 增加 `clientCommandId` 幂等去重，处理重试与重复提交。
- 压测验收：并发命令下对局状态不出现逆序版本。

### Phase 3 - 全量护栏与回滚自动化（3-5 天）
- 完成广播白名单 lint 与跨局实体不变量检测。
- 特性开关支持一键回退到上一阶段稳定路径。
- 预案演练：模拟违规广播/状态不一致并验证告警与回滚。

## Risks / Trade-offs
- 风险: 单写者队列可能增加峰值延迟
  - Mitigation: 对长耗时操作拆分异步副作用，保留核心状态提交短路径。
- 风险: 一次性替换状态源可能触发兼容问题
  - Mitigation: 双读对账（只读）窗口 + 哈希比对 + 灰度放量。
- 风险: 广播白名单过严导致消息丢失
  - Mitigation: 先告警后阻断，两阶段启用策略。

## Rollback Strategy
- 每阶段均通过 feature flag 独立开关控制。
- 任一门禁指标连续 5 分钟超阈值即自动降级到前一稳定阶段。
- 回滚后保留审计与日志增强，确保可继续定位。

## Open Questions
- 是否在 Phase 2 同步引入 Redis 作为跨进程会话路由媒介（当前默认单进程）。
- spectator/observer 模式的可见性矩阵是否需要在本次一并冻结。

