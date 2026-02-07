## Context
AuraConfig 目前通过 BuffTemplateConfig 生成单个复合 Buff。为了让后续 Aura 变更能够逐个 BuffConfig 产出事件，先将数据模型升级为可组合的 BuffConfig 列表，并在运行期直接按 BuffConfig 生成 Buff。

## Goals / Non-Goals
- Goals:
  - 引入 BuffConfig 作为独立配置单元。
  - AuraConfig 保存复数 BuffConfig，运行时逐个 BuffConfig 生成 Buff（不再合并）。
  - 为后续逐个 BuffConfig 事件化流程预留标识与 effectKey 规则。
- Non-Goals:
  - 不改变 Buff 叠加/移除规则。
  - 不改变当前 Aura 刷新触发时机。

## Decisions
- 采用“数据结构升级 + 逐条 BuffConfig 生成 Buff”的过渡模式。
- BuffConfig 需要稳定标识（Name/Id）以生成 effectKey 前缀。
- 前端按 EffectKey 自动合并显示，无需后端合并 Buff。

## Risks / Trade-offs
- 逐个 BuffConfig 生成 Buff 会增加事件数量，需评估性能与日志噪声。
- 多个 BuffConfig 共用 EffectKey 时需要确保 CanStack 配置一致以避免被过滤。

## Migration Plan
- 新增 BuffConfig 并在 AuraConfig 中并行支持（默认使用 BuffConfigs）。
- 旧的 BuffTemplateConfig 通过适配器转换为单个 BuffConfig。
- 验证现有 Aura 刷新与 Buff 叠加用例不回归。

## Open Questions
- BuffConfig 的稳定标识是否采用 Name 或显式 Id？
- effectKey 规则是否由 AuraConfig 统一生成，还是由 BuffConfig 参与生成？
