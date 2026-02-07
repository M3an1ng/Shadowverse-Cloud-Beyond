## 1. Spec / Design
- [x] 1.1 明确 Replay Authority Id Set：列出必须稳定的 ID（Player/Card/Buff）与 runtime-only ID（AbilityRuntime/Effect/Choice）。
- [x] 1.2 明确 `UserId` 与 `PlayerId` 分层与映射：联机/房间使用 UserId，事件/回放使用 PlayerId。
- [x] 1.3 定义 InGameId 格式约束与解析规则（前缀/数字/大小写/容错），并规定“Output RawEvent 禁止 Guid”。
- [x] 1.4 定义 RawEvent schemaVersion 升级与兼容窗口（旧 Guid Journal 的读取策略与失败诊断）。

## 2. Implementation (planned, not started here)
- [ ] 2.1 将 RawEventEnvelope 的 `EventId/SourceId/TargetIds` 从 Guid 迁移为 InGameId（字符串或强类型），并更新校验器/注册表/rehydrator。
- [ ] 2.2 为局内实体创建稳定 ID 分配器（按类型递增），并确保在“创建实体的事实事件”中写入该 ID。
- [ ] 2.3 改造调试指令入口：只接受 InGameId；必要时提供短期 Guid 兼容开关（仅 dev），但输出仍不得包含 Guid。
- [ ] 2.4 联机入口拆分 `UserId` 与 `PlayerId`：修复任何把 UserId 当作 PlayerId 的路径，并补充关键日志。

## 3. Validation
- [ ] 3.1 单测：Output RawEvent 不包含 Guid（schema 校验失败即拒绝写入）。
- [ ] 3.2 集成：离线回放跨进程重建（重启服务后读取同一 Journal 能复现相同结果）。
- [ ] 3.3 手动：调试控制台可用 `C-000123`/`P-1`/`B-000045` 触发常用事件，且日志可定位。
