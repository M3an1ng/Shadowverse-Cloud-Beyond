# Change: Add SetAttackTimes effect handler

## Why
部分卡牌效果需要修改随从每回合可攻击次数，但当前系统缺少 SetAttackTimes 效果处理器与对应状态更新路径。

## What Changes
- 新增 SetAttackTimes 效果参数与处理器，支持设置目标随从的最大攻击次数。
- 为卡牌实例新增 MaxAttacksPerTurn 状态，并在事件提交时更新。
- 补充必要的事件与映射，确保状态变更可被系统消费。

## Impact
- Impacted specs: attack-times-effects
- Impacted code: Server/Domain/Abilities/Effects/Handlers, Server/Domain/Entities/CardInstance.cs, Server/Domain/Events/*
