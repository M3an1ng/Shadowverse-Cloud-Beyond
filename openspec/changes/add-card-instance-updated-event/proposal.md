# Change: add-card-instance-updated-event

## Why
当前 Buff 触发后前端不会收到最新的卡牌预览信息（如 buffSummaries），导致 UI 与真实状态不同步。需要在事务结束时统一发送受影响卡牌的完整快照，避免重复事件负载并保持客户端状态一致。

## What Changes
- 新增 CardInstanceUpdated 事件，用于在事务结束时推送受影响卡牌的完整 CardInstanceDto。
- 事务处理阶段汇总受影响卡牌并一次性广播，前端据此更新本地实例。

## Impact
- 受影响的 specs: card-instance-updates
- 受影响的代码: Server/Domain/Events, Server/Services, src/gameManager.js
