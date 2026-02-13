# Change: refactor-frontend-zone-movement-cardmoved

## Why
当前前端区域同步由多个事件共同驱动（如 HandUpdated、UnitDestroyed、CardVanished、CardReturned），存在职责重叠与竞态风险：
- 同一张卡可能被多个 handler 重复迁移，导致“幽灵卡”或短暂回弹。
- `handleHandUpdated` 承担了跨区域清理职责，语义不单一。
- 新增事件时容易再次复制迁移逻辑，维护成本持续升高。

为了保证状态一致性与可维护性，需要将“区域迁移”收敛到单一事实事件 `CardMoved`。

## What Changes
- 定义前端状态同步规则：区域变化 MUST 由 `CardMoved` 事件唯一驱动。
- 将 `Return/Destroy/Vanish/Draw` 等事件降级为语义事件，仅用于日志、动画和提示，不直接改 zone 集合。
- 将 `HandUpdated` 回归为手牌快照同步职责，不再执行跨区域清理。
- 增加迁移期兼容策略，支持灰度切换并避免联机期间状态抖动。

## Impact
- Affected specs: frontend-zone-movement
- Affected code:
  - Server/Domain/Events/EventProcessor.cs
  - Server/Dtos/SignalR/Events/*
  - Server/Services/SignalRClientMethodMapper.cs
  - Server/Hubs/ClientInterfaces/IGameClient.cs
  - src/main.js
  - src/gameManager.js

## Risks
- 事件乱序或重复投递可能导致客户端重复迁移。
- 部分历史 handler 仍可能保留状态写入逻辑。

## Mitigations
- 引入基于 EventId/Sequence 的幂等保护。
- 在迁移期保留全量快照（ReceiveGameState/HandUpdated）作为校准兜底。
- 增加端到端场景测试覆盖 Return/Destroy/Vanish/Draw。
