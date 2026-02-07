## Context
目前 isNonStackable 使用 SourceAbilityId 进行替换，无法区分同一 Ability 内多个 Effect 添加的 Buff，导致互相覆盖。

## Goals / Non-Goals
- Goals:
  - 使 isNonStackable 的替换粒度提升到 Effect 级别。
  - 用可复用的 Key 统一替换规则（未设置 Key 的 Buff 默认可叠加）。
- Non-Goals:
  - 不引入 BuffKind 或复杂签名系统。
  - 不改变 Buff 的生效/计算规则。

## Decisions
- Decision: 为 Effect 增加 EffectIndex。
- Decision: isNonStackable 仅保留在效果参数中，用于写入 Buff.NonStackableKey。
- Decision: NonStackableKey 使用 (SourceAbilityId + EffectIndex) 生成，作为替换判定依据。
- Alternatives considered:
  - 使用 Effect.Id 作为 NonStackableKey（不稳定，无法替换）。
  - 引入 BuffKind/EffectSignature（复杂度较高，暂不需要）。

## Risks / Trade-offs
- 需要确保所有非叠加 Buff 创建路径都能写入 NonStackableKey。
- 系统 Buff 或无 Effect 场景默认可叠加，需要上层显式设置 key 才能替换。

## Migration Plan
- 先新增 EffectIndex 与 NonStackableKey，再调整堆叠逻辑；清理 Buff 实体上的 IsNonStackable。

## Open Questions
- 是否需要将 EffectIndex 暴露给调试日志/DSL（暂不需要）。
