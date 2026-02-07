# Change: Add split damage, temporary status, and attack reset effects

## Why
“神眷的棋盘”中包含分配伤害、回合结束前临时属性变化、以及重置攻击状态的能力，当前系统缺少对应的效果能力与元数据支持，导致转换无法准确落地。

## What Changes
- 为伤害效果增加“分配伤害”模式（SplitDamage）。
- 为属性修改效果增加持续时间参数（用于回合结束前的临时效果）。
- 新增“重置攻击次数”效果与事件，用于实现 UnStriked 类能力。

## Impact
- 受影响的 specs: `effects`
- 受影响的代码: 伤害/属性修改效果处理器、事件定义、前端效果元数据