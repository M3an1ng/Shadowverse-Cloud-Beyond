## Context
Random/Choose 分支目前通过 effectConfig.chainedAbilities 表达，导致分支与效果连锁能力语义混用，且编辑器无法与参数化能力列表对齐。

## Goals / Non-Goals
- Goals:
  - 让 Random/Choose 分支来源显式化（AbilityConfigs 参数）。
  - 保留效果自身 chainedAbilities 的常规处理语义。
  - 明确分支执行顺序：分支能力在前，效果 chainedAbilities 在后。
  - 分支能力作为效果本体执行结果，EffectCompletedEvent 汇总分支结果供真正 chainedAbilities 使用。
- Non-Goals:
  - 重写连锁能力执行引擎。
  - 自动兼容旧 chainedAbilities 作为分支来源。

## Decisions
- Decision: Choose/Random 分支列表只读取 AbilityConfigs 参数，chainedAbilities 不再作为分支来源。
- Decision: 分支能力执行在前，效果自身 chainedAbilities 在后。
- Decision: 未提供 AbilityConfigs 时判为配置无效（强制迁移旧数据）。
- Decision: 分支能力不通过 effectRequest.ChainedAbilities 执行，而是作为效果本体执行结果返回。

## Risks / Trade-offs
- Breaking change：旧数据需要迁移，否则效果将失败。
- 编辑器需要暴露新的参数入口以避免误配。
- 需要调整 EffectCompletedEvent 生成路径以合并分支结果。

## Migration Plan
1) 迁移现有卡牌/能力配置：将旧 Choose/Random 的 chainedAbilities 移到 parameters.AbilityConfigs。
2) 清理旧分支 chainedAbilities，保留真实“连锁能力”的配置。
3) 验证 Random/Choose 运行时仅使用 AbilityConfigs。

## Open Questions
- None.
