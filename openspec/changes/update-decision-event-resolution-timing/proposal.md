# Change: Update Decision Event Resolution Timing

## Why
当前 Choose/Random 的决策发生在事件生成之前，导致 RewriteEventParamsEffect 只能改写事件记录，无法影响真实决策。

## What Changes
- 决策事件（Choose/Random）先生成并允许拦截改写输入参数，再在事件处理阶段生成选择结果。
- 分支能力的执行时机后移到决策事件完成之后。
- **BREAKING**：决策结果的产生时机与可拦截范围发生变化。

## Impact
- 受影响的代码: Server/Services/GameActionTransactionService.cs, Server/Domain/Abilities/Ability.cs, Server/Domain/Events/ChooseEvent.cs, Server/Domain/Events/RandomEvent.cs
- 受影响的能力: 决策事件（Choose/Random）与事件拦截效果（RewriteEventParamsEffect）
