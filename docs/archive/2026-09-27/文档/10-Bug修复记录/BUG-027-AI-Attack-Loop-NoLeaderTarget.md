# BUG-027: AI 进化后无可攻击目标导致攻击循环

- **发现日期:** 2025-12-28
- **状态:** Implemented (待验证)
- **严重性:** High
- **影响范围:** AI 回合攻击逻辑、进化后攻击主战者判定

## 1. 问题描述

AI 随从进化后，场上没有可攻击随从且**进化并不能攻击主战者**时，仍反复进入攻击逻辑，导致死循环。

## 2. 根本原因分析 (RCA)

1. `CanAttackLeader` 辅助逻辑错误地将 `IsEvolved` 视为可攻击主战者的条件，导致 AI 误判。
2. `AiService.SelectAttackTarget` 在场上无随从时未先校验攻击者是否可以攻击主战者，
   仍返回主战者目标，导致持续提交无效攻击。

## 3. 修复方案

- 修正主战者攻击判定：进化不解除攻击主战者限制，仅允许 Storm 或场上存活回合数 > 0。
- AI 选择主战者目标前，校验攻击者是否允许攻击主战者；不允许则返回 null，结束攻击阶段。

## 4. 代码变更

- `Server/Domain/Entities/CardInstance.cs`
  - 主战者攻击判定维持“Storm 或 FieldTurn > 0”。
- `Server/Domain/Extensions/CardInstanceExtensions.cs`
  - `CanAttackLeader` 逻辑与服务端判定对齐（Storm / FieldTurn > 0）。
- `Server/Services/AiService.cs`
  - 无随从场景下，先检查 `CanAttackLeader`，不满足则跳过主战者目标。

## 5. 验证建议

1. AI 随从当回合进化后，场上无其他目标时尝试攻击主战者。
2. 观察 AI 不再提交无效攻击，回合可正常结束。
3. 进化随从可正常攻击主战者（符合规则设计）。
