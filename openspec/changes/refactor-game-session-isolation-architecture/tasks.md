## 1. Phase 0 - Stabilize and Observe
- [x] 1.1 修复 `PlayCard` 等关键日志字段映射，统一结构化日志键名。
- [x] 1.2 梳理并修正所有私有消息通道：手牌可用性、私有 zone、仅本人可见错误。
- [x] 1.3 增加“私有载荷发送到公共组”实时告警与计数器。
- [x] 1.4 建立阶段门禁看板：`cannot_create_game_context`, `private_payload_to_game_group`, `cross_game_entity_violation`。

## 2. Phase 1 - Single Source of Truth
- [ ] 2.1 删除/禁用 `GameService` 私有状态容器，仅保留统一状态管理接口。
- [ ] 2.2 `GameContextFilter`、Hub、Service 全部切换到同一读写路径。
- [ ] 2.3 引入状态版本字段并在写入时做单调性校验。
- [ ] 2.4 增加回归测试：上下文创建、断线重连、并发读写一致性。

## 3. Phase 2 - Per-Game Single Writer
- [ ] 3.1 实现 `GameSessionActor`（按 `gameId` 路由 + 顺序队列）。
- [ ] 3.2 将核心写命令（PlayCard/Attack/EndTurn/Mulligan）迁移到 actor 入队路径。
- [ ] 3.3 增加 `clientCommandId` 幂等去重，覆盖断线重试场景。
- [ ] 3.4 增加并发压测与顺序性测试（同局乱序请求、跨局并发）。

## 4. Phase 3 - Guardrails and Rollback Automation
- [ ] 4.1 广播前 payload lint：公共事件禁止私有字段。
- [ ] 4.2 运行时不变量检测：同一 `cardUid/entityId` 不得跨 `gameId` 漂移。
- [ ] 4.3 特性开关与自动回滚策略上线并完成演练。
- [ ] 4.4 更新运维 runbook 与故障排查手册。

## 5. Validation
- [ ] 5.1 `openspec validate refactor-game-session-isolation-architecture --strict` 通过。
- [ ] 5.2 灰度期连续 48 小时门禁指标达标。
- [ ] 5.3 生产抽样日志中关键链路字段完备率达到 99.9% 以上。

