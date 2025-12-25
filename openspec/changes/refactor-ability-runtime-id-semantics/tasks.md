## 1. Investigation
- [x] 1.1 全面枚举 Ability/Buff/Effect/PendingChoice 相关 Guid 字段与持久化路径（含 DTO、日志、回放）。
- [x] 1.2 补齐“跨进程稳定性矩阵”：明确哪些 Guid 必须可重放、哪些必须运行期（Ability/Effect/Buff）。
- [x] 1.3 定义“局内可读 ID”格式与生成规则（前缀、长度、字符集、是否兼容 Guid 输入）。

## 2. Implementation (Server)
- [x] 2.1 放宽 `Buff.SourceAbilityId` 约束：允许 `Guid.Empty` 表示 System/No-Source，并更新 `Buff.SourceAbility` 的行为。
- [x] 2.2 将系统路径创建 Buff（例如 `StatChangedEvent`）统一走 `ApplyStatBuff`，由 BuffService 在无 Ability 上下文时使用 `Guid.Empty`（不再 `Guid.NewGuid()`）。
- [x] 2.3 修复系统 Buff 在 Apply/Remove 事件中的 source 依赖：允许 `BuffAppliedEvent/BuffRemovedEvent` 传入 null source。
- [x] 2.4 增加关键日志：系统 Buff 应明确标注 `SourceAbilityId=EMPTY`。

- [ ] 2.5（跨进程）定义并实现 Buff 实例可重放标识：当后续事实事件需要引用 Buff 时，Output RawEvent 必须携带并复用同一 `buffId`。
- [ ] 2.6（跨进程）避免把 `Ability.RuntimeId` 变为持久契约：为 Buff 叠加/替换等来源识别引入稳定键（例如 `DefinitionId + SourceCardInstanceId + SourceBuffId?`）。
- [ ] 2.7（跨进程）确认 `Effect.Id` 不进入回放/协议；若存在引用则改为稳定键或移除。
- [x] 2.8（调试/日志）实现局内可读 ID：生成、解析、日志输出（同时兼容 Guid），并在调试指令入口支持输入可读 ID。

## 3. Validation
- [x] 3.1 单测：系统 Buff 仍能正常生效且 `SourceAbility == null`；并验证移除/叠加不回归。
- [ ] 3.2 手动：触发一个系统路径 Buff（例如直接修改属性的事件），确认日志清晰且不污染随机来源。
- [ ] 3.3（跨进程）集成：回放重建时 BuffApplied/BuffRemoved 能对齐同一个 buffId（若实现 2.5）。
- [ ] 3.4（调试/日志）手动：测试者可直接输入可读 ID（如 `C-000123`）触发调试指令，日志能清晰显示类别前缀。
