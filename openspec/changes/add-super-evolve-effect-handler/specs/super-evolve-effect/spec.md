## ADDED Requirements
### Requirement: SuperEvolve Effect
系统 SHALL 支持 SuperEvolve 效果类型，使目标随从执行超进化流程（消耗超进化点并应用超进化状态）。

#### Scenario: SuperEvolve on follower
- **WHEN** 执行 SuperEvolve 效果且目标为随从
- **THEN** 系统应调用超进化流程并生成超进化相关事件

#### Scenario: Invalid target
- **WHEN** SuperEvolve 效果目标缺失或不是有效随从
- **THEN** 系统应返回失败并提供错误信息