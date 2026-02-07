## ADDED Requirements

### Requirement: Ability Editor MUST Enforce Pre-Save Validation
能力编辑器在保存前 MUST 执行一致的校验（必填字段、DSL 语法、类型、scope），并在存在错误时阻止保存；错误信息 MUST 可定位到具体“效果/参数/DSL 节点”。

#### Scenario: Missing required parameter blocks saving
- **GIVEN** an effect has a required parameter
- **WHEN** the user attempts to save the ability
- **THEN** the editor MUST block saving
- **AND** display an actionable message identifying the missing parameter

#### Scenario: Invalid DSL blocks saving
- **GIVEN** a condition DSL contains a syntax error
- **WHEN** the user attempts to save the ability
- **THEN** the editor MUST block saving and highlight the invalid part

### Requirement: Validation Issues MUST Be Classified And Aggregated
能力编辑器在保存前 MUST 将校验结果分级为 `error` / `warn` / `info` 并聚合展示；其中 `error` MUST 阻止保存，`warn` SHOULD 提示风险但允许继续（可选：二次确认），`info` MUST NOT 阻止保存。

#### Scenario: Errors block save while warnings do not
- **GIVEN** validation produces both `error` and `warn` issues
- **WHEN** the user attempts to save the ability
- **THEN** the editor MUST block saving due to `error`
- **AND** display `warn` issues as non-blocking guidance

#### Scenario: Warnings can be acknowledged
- **GIVEN** validation produces only `warn` issues
- **WHEN** the user attempts to save the ability
- **THEN** the editor SHOULD allow saving after user acknowledgement (if configured)

### Requirement: Editor MUST Provide Guided Templates For Common Abilities
编辑器 MUST 提供若干“常见能力模板”（例如伤害、抽牌、恢复、条件触发），模板 MUST 基于元信息生成并保证初始配置合法，可作为普通玩家的起点。

#### Scenario: Create a damage ability without writing DSL
- **WHEN** a player chooses the "Deal Damage" template
- **THEN** the editor MUST generate a valid default ability configuration
- **AND** the player MUST be able to finish it by only selecting targets and entering numbers
