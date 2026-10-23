# BUG-026: AI 提交目标选择时 SourceAbilityRuntimeId 不匹配

- **发现日期:** 2025-12-28
- **状态:** Implemented (待验证)
- **严重性:** High
- **影响范围:** AI 对战目标选择、目标类法术（例如“天使之箭”）

## 1. 问题描述

AI 在对战中触发目标选择（如“天使之箭”）时，服务端抛出异常并导致选择流程中断：

```
Invalid choice submission: SourceAbilityRuntimeId mismatch (expected <guid>, got 00000000-0000-0000-0000-000000000000)
```

## 2. 根本原因分析 (RCA)

`PendingActionService.WaitForPlayerChoiceAsync` 中，AI 提交 `SubmitTargetChoiceActionDto` 时未填写
`SourceAbilityRuntimeId` 与 `TargetKey`，导致 `CompletePlayerChoice` 的一致性校验失败。

## 3. 修复方案

- 在 AI 提交目标选择与取消时，补齐 `SourceAbilityRuntimeId` 与 `TargetKey`。

## 4. 代码变更

- `Server/Services/PendingActionService.cs`
  - AI 选择提交与取消 DTO 增加 `SourceAbilityRuntimeId` / `TargetKey` 赋值。

## 5. 验证建议

1. AI 对战中触发“天使之箭”等需要目标选择的卡牌。
2. 观察服务端日志，确认不再出现 `SourceAbilityRuntimeId mismatch`。
3. 目标选择流程正常完成，事件链不中断。
