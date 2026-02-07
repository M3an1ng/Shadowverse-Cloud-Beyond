## ADDED Requirements
### Requirement: Spellboost Counters
系统 SHALL 在玩家使用法术后，对其手牌中具备 Spellboost 机制的卡牌递增 spellboost 计数。

#### Scenario: Spell cast increments spellboost
- **WHEN** 玩家使用一张法术卡
- **THEN** 其手牌中具备 Spellboost 的卡牌 spellboost 计数增加 1

### Requirement: Earth Sigil Counters
系统 SHALL 为带有 EarthSigil 关键字的卡牌初始化 earthsigil 计数，并在多个土之印同时存在时合并计数。

#### Scenario: EarthSigil initializes on entry
- **WHEN** 带有 EarthSigil 关键字的护符进入战场且计数为 0
- **THEN** 该护符的 earthsigil 计数设置为 1

#### Scenario: EarthSigil merges
- **WHEN** 战场上已有土之印护符且新的土之印护符进入战场
- **THEN** 土之印计数应合并到单一护符，其它土之印护符被移除

### Requirement: Earth Sigil Immunity
系统 SHALL 阻止对手能力选中或以能力破坏方式移除土之印护符。

#### Scenario: Opponent ability cannot target earth sigil
- **WHEN** 敌方能力尝试选中土之印护符
- **THEN** 该目标应被判定为不可选中

### Requirement: Earth Rite Consume Effect
系统 SHALL 提供 EarthRite 效果以消耗土之印计数，失败时可按 OnFailure 跳过链式能力。

#### Scenario: Consume fails and skips chained abilities
- **WHEN** EarthRite 消耗不足并返回失败
- **THEN** 若 OnFailure=SkipEffect，则该效果与其链式能力不会执行

### Requirement: DSL Access to System Counters
系统 SHALL 允许 DSL 读取系统计数器（spellboost、earthsigil）。

#### Scenario: DSL reads system counters
- **WHEN** DSL 使用 count("spellboost") 或 count("earthsigil")
- **THEN** 返回对应计数器数值，缺省为 0

#### Scenario: DSL reads counters via dot-path
- **WHEN** DSL 通过点路径访问计数字典（例如 `self.counters.spellboost`）
- **THEN** 返回对应计数器数值，缺省为 0

#### Scenario: DSL reads system counter aliases
- **WHEN** DSL 通过 `self.spellboost` 或 `self.earthsigil` 访问系统计数器别名
- **THEN** 返回对应计数器数值，缺省为 0
