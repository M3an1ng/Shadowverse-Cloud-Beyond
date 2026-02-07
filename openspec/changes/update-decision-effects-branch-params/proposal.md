# Change: Update Decision Effect Branch Params

## Why
当前 Random/Choose 分支依赖 effectConfig.chainedAbilities，导致分支与普通连锁能力耦合，编辑体验与 GrantAbility 参数化不一致，且分支来源不清晰。

## What Changes
- Random/Choose 的分支列表改为参数化 AbilityConfigs（**BREAKING**：不再从 chainedAbilities 推断分支）。
- Random/Choose 的分支能力作为效果本体执行结果返回，EffectCompletedEvent 汇总分支结果供真正的 chainedAbilities 使用。
- Random/Choose 效果自身的 chainedAbilities 仍按常规效果在执行后处理。
- 能力编辑器为 Random/Choose 显示 AbilityConfigs 列表编辑控件。

## Impact
- 受影响的 specs: decision-effects
- 受影响的代码: Server/Domain/Abilities/Ability.cs, Server/Domain/Abilities/Effects/Handlers/Parameters/ChooseEffectParams.cs, Server/Domain/Abilities/Effects/Handlers/Parameters/RandomEffectParams.cs, src/components/EffectConfigEditor.js, data/*.json
