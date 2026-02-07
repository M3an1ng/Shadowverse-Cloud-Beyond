# Change: Add Random/Choose decision effects

## Why
当前系统缺少可复用的 RandomEffect 处理器，ChooseEffect 仅占位且不支持多选/模式效果，导致“随机/选择分支”无法自然表达。

## What Changes
- 新增 RandomEffectParams 与 RandomEffectHandler，支持按概率分布选择多个分支结果并触发链式能力。
- 扩展 ChooseEffectParams，支持配置可选数量与多选，并把选择结果传递给链式能力。
- 在 Ability.CreateEffectsAsync 中统一决策型效果的分支解析与结果输出。
- 视需要补充 Ability Editor 的配置项（选择数、权重等）。

## Impact
- Impacted specs: decision-effects
- Impacted code: Server/Domain/Abilities, Server/Domain/Abilities/Effects, src/components/AbilityEditor.js
