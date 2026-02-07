# Change: Add player temporary variable system

## Why
玩家层目前缺少与卡牌一致的临时变量系统，导致连击数/墓场/血池分散为字段，不利于DSL统一访问与扩展。

## What Changes
- 为 PlayerState 引入临时变量管理系统（Counters），支持读取/写入/增减/消耗。
- 预注册玩家临时变量：连击数、墓场数、血池。
- 新增修改变量效果，用于通过效果修改玩家或卡牌上的变量。

## Impact
- 受影响的 specs: temp-variables
- 受影响的代码: Server/Domain/Entities/PlayerState.cs, Server/Domain/Entities/Counters.cs, Server/Domain/Events, Server/Domain/Abilities/Effects
