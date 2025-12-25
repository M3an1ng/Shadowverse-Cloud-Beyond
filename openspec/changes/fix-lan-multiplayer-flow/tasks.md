## 1. Implementation
- [x] 1.1 Stop auto game init in multiplayer route
- [x] 1.2 Add match polling and gating to Started
- [x] 1.3 Prevent single-player from reusing PVP handshake
- [x] 1.4 Clear handshake/polling on exit
- [x] 1.5 Gate GameState creation on Started in hub
- [x] 1.6 Validate build and smoke test
- [x] 1.7 PVP 全量状态水合不得覆盖 `localPlayerId`（握手 `playerId` 为准）
- [x] 1.8 UI 渲染时若 `localPlayerId` 不存在于 Players，必须输出错误并拒绝用 IsAI/索引推断座位
- [x] 1.9 增加房间退房接口（/api/match/{id}/leave）并返回 closedReason
- [x] 1.10 退出多人大厅/退出对战页时调用退房并清理握手，避免单人误用等待对手
- [x] 1.11 退房后对端提示：大厅轮询显示 Closed；对战页接收 ROOM_CLOSED 并回到主菜单
- [x] 1.12 调度按钮状态重置：进入新对局时确认按钮必须恢复为“确认调度”，避免跨对局遗留“等待对方...”

## 2. Validation
- [ ] 2.1 双人联机：两端进入对战后手牌不同，且调度完成状态互不影响（各自只影响自己）
- [ ] 2.2 退房回归：一端返回主菜单后，另一端大厅/对战页收到“房间已解散/对手离开”提示；退房后进入单人对战自动创建 AI 房间不再等待对手
- [ ] 2.3 单人调度回归：从多人退出后进入单人对局，调度确认按钮可用且文案正确（不会卡在“等待对方...”）
