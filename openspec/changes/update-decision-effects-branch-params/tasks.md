## 1. Implementation
- [x] 1.1 为 Choose/Random 参数新增 AbilityConfigs，并挂载 AbilityListEditor。
- [x] 1.2 Ability.CreateEffectsAsync 使用 AbilityConfigs 作为分支来源；禁止从 chainedAbilities 推断。
- [x] 1.3 确保分支能力先于效果自身 chainedAbilities 执行。
- [x] 1.4 更新 EffectConfigEditor 的参数展示以支持 AbilityConfigs。
- [x] 1.5 迁移现有卡牌/能力数据到 AbilityConfigs（移除旧分支的 chainedAbilities）。
- [x] 1.6 分支能力改为 Effect 本体执行结果（不走 effectRequest.ChainedAbilities）。
- [x] 1.7 EffectCompletedEvent 汇总分支能力生成事件，供真正 chainedAbilities 使用。
- [x] 1.8 连锁能力与分支能力触发时机/有效区域锁定（触发次数限制保持可编辑）。

## 2. QA
- [ ] 2.1 手动：Random/Choose 分支正常选择并执行，连锁能力在分支之后执行。
- [ ] 2.2 手动：旧数据未迁移时，Random/Choose 必须报错或被拒绝。
