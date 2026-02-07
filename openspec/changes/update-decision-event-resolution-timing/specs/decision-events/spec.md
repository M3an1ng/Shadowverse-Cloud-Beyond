## ADDED Requirements
### Requirement: Decision Resolution From Event Inputs
Choose/Random 决策结果 MUST 在事件处理阶段基于事件输入参数生成，并在输出参数中记录最终选择。

#### Scenario: Rewrite modifies selection count
- **WHEN** RewriteEventParamsEffect 将 selectionCount 从 1 改为 2
- **THEN** 最终选择结果应包含 2 个选项

### Requirement: Branch Ability Execution After Decision Output
分支能力 MUST 在决策事件输出生成后执行。

#### Scenario: Branch executes after decision
- **WHEN** 决策事件生成选择结果
- **THEN** 分支能力基于该结果执行且不早于事件输出产生
