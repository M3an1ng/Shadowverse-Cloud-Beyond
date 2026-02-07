# Change: Add Spellboost and Earth Sigil/Earth Rite systems

## Why
当前系统缺少魔力增幅与土之印/土之秘术机制，导致 Runecraft 卡牌无法完整迁移和结算。

## What Changes
- 新增 Spellboost 计数与触发机制，支持法术使用时对手牌进行增幅。
- 新增 Earth Sigil（土之印）关键字与计数逻辑，支持合并、免疫和归零破坏。
- 新增 Earth Rite（土之秘术）消耗效果，失败时可按 OnFailure 策略跳过链式能力。
- DSL 增加系统计数器访问接口，并支持以点路径访问计数器字典与 spellboost/earthsigil 别名。

## Impact
- Impacted specs: spellboost-earthsigil
- Impacted code: Server/Domain/Entities, Server/Domain/Events, Server/Domain/Abilities, Server/Domain/Constants, src/components
