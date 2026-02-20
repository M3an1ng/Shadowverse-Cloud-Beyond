## 1. Implementation
- [x] 1.1 新增 `LeaveFieldEvent` 与必要的事件元数据（`EventType`、参数、提交逻辑）。
- [x] 1.2 在 `LeaveFieldEvent.Commit` 中实现统一路由规则：`Destroy/Vanish/Return/Tuck/NoOp`。
- [x] 1.3 将破坏免疫判定集中到 `LeaveFieldEvent`，并清理分散前置判定（`DestroyEffectHandler`、`CombatDestroyResolutionEvent` 等）。
- [x] 1.4 将消失免疫判定（`VanishResist`）集中到 `LeaveFieldEvent`，调整 `VanishEffectHandler` 不再自行短路。
- [x] 1.5 将所有直接构造 `DestroyEvent/CardVanishedEvent/ReturnEvent/TuckEvent` 的业务入口迁移为构造 `LeaveFieldEvent`（见提案中的调用点清单）。
- [x] 1.6 保持 `Destroy/Vanish/Return/Tuck` 现有提交语义（继续负责发出 `CardMovedEvent`），只去除已迁移的业务判定责任。
- [x] 1.7 评估并改造 `RawEventGameEventRegistry` 的 `destroy` 注入路径（兼容 `LeaveField` 或保留 legacy 兜底策略）。
- [x] 1.8 按需更新事件处理与回放映射，确保 `LeaveFieldEvent` 可控可见（默认建议系统/隐藏）。

## 2. Validation
- [x] 2.1 新增测试：`Ability` 来源离场 + `DestroyResist`/`VanishResist` 判定。
- [x] 2.2 新增测试：`LeaveField -> Destroy/Vanish/Return/Tuck` 分流正确，且 legacy 事件仍按原语义产出 `CardMoved`。
- [x] 2.3 新增测试：冲突场景“无法被能力消失 + 离场转消失”应路由为正常破坏，不发消失事件。
- [ ] 2.4 回归测试：`Destroy` 相关触发、`Return/Tuck` 相关触发、墓地计数与移动派生事件不回归。
