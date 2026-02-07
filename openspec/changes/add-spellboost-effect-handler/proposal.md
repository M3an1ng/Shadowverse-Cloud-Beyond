# Change: add-spellboost-effect-handler

## Why
当前 DSL 已包含 SpellBoost 效果类型，但后端缺少对应处理器，导致能力无法手动触发魔力增幅事件。

## What Changes
- 新增 SpellBoost 效果处理器：对选定目标触发一次 SpellBoostEvent。
- 新增 SpellBoost 效果参数类型（无参数，仅用于元数据与UI展示）。
- 补充基础测试，覆盖 SpellBoost 效果事件生成与计数增幅。

## Impact
- 受影响的 specs: effect-spellboost
- 受影响的代码: Server/Domain/Abilities/Effects/Handlers, Server/Domain/Abilities/Effects/Handlers/Parameters, Server.Tests
