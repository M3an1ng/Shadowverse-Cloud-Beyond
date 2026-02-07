# Change: Add SuperEvolve effect handler

## Why
当前 DSL 只能通过进化效果触发进化流程，缺少可复用的超进化效果类型，阻碍 Dragoncraft 等卡牌的转换与表达。

## What Changes
- 新增 EffectType.SuperEvolve，并提供 SuperEvolveEffectParams（与 EvolveEffectParams 对齐）。
- 新增 SuperEvolveEffectHandler，调用 PlayerState.SuperEvolveCard 执行超进化流程。
- 通过 EffectHandler 特性注册新的处理器。

## Impact
- Impacted specs: super-evolve-effect
- Impacted code: Server/Domain/Abilities/Effects, Server/Domain/Entities
