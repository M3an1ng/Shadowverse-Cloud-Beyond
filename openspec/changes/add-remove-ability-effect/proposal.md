# Change: Add Remove Ability Effect (Suppression-Based)

## Why
当前仅支持 GrantAbility，缺少对卡牌实例能力的反向移除能力。新增 removeAbilityEffect 以支持按 Ability.DefinitionId、能力关键字或全部移除能力，并允许按持续时间恢复。

## What Changes
- 新增 RemoveAbility 效果，参数支持 RemoveMode=ID/Keyword/ALL。
- 使用“能力抑制 Buff”实现移除语义（不销毁能力实例），到期自动恢复。
- ALL 模式需清除被抑制能力产生的 aura 效果。
- 触发、Act/Fusion 可用性、以及 UI 能力摘要需改为使用“有效能力列表”。

## Impact
- 受影响 specs: ability-effects
- 受影响代码: AbilityManager、Buffs、Effect handlers/params、AuraManager、Act/Fusion 请求与可用性、CardInstanceProfile、效果编辑器与本地化枚举
