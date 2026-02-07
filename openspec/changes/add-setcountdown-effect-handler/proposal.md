# Change: add-setcountdown-effect-handler

## Why
当前 DSL 已存在 Countdown-1 等语义，但后端缺少 SetCountdown 对应的处理器，导致倒计时类能力无法落地执行。

## What Changes
- 新增 SetCountdown 效果处理器，用于对护符倒计时进行增减。
- 新增 SetCountdown 效果参数配置（变化量）。
- 添加基础测试，覆盖倒计时变化与归零销毁的链路。

## Impact
- 受影响的 specs: countdown-effects
- 受影响的代码: Server/Domain/Abilities/Effects/Handlers, Server/Domain/Abilities/Effects/Handlers/Parameters, Server.Tests
