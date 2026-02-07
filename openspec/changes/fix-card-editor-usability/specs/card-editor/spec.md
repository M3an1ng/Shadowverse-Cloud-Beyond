## ADDED Requirements

### Requirement: 卡牌编辑器提供可用的卡牌 CRUD 闭环
系统 MUST 允许用户在“卡牌编辑器”中创建、读取、更新、删除卡牌定义，并将修改持久化到后端。

#### Scenario: 加载卡牌列表并选择卡牌
- **WHEN** 用户进入卡牌编辑器页面
- **THEN** 系统 MUST 从 `GET /api/cards` 加载并显示卡牌列表
- **AND** 用户点击某张卡牌后系统 MUST 从 `GET /api/cards/{id}` 加载详情并渲染到表单与预览区

#### Scenario: 更新卡牌并保存
- **WHEN** 用户修改卡牌基础字段并点击保存
- **THEN** 系统 MUST 调用 `PUT /api/cards/{id}` 持久化修改
- **AND** 保存成功后 UI MUST 回显最新数据

#### Scenario: 另存为新卡牌
- **WHEN** 用户在已选中卡牌基础上点击“另存为”
- **THEN** 系统 MUST 调用 `POST /api/cards` 创建新卡牌定义
- **AND** 创建成功后 UI MUST 自动切换到新卡牌并可继续编辑

### Requirement: 卡牌编辑器提供可用的能力管理闭环
系统 MUST 允许用户在卡牌编辑器中查看、创建、编辑能力，并将能力与卡牌进行关联/解除关联且可持久化。

#### Scenario: 加载卡牌关联能力列表
- **WHEN** 用户切换到“能力管理”标签页
- **THEN** 系统 MUST 调用 `GET /api/cards/{cardId}/abilities` 并显示真实能力数据（非占位）

#### Scenario: 创建能力并持久化
- **WHEN** 用户通过编辑器创建新能力并确认保存
- **THEN** 系统 MUST 调用 `POST /api/abilities` 并将能力持久化
- **AND** 创建成功后能力 MUST 出现在该卡牌的能力列表中（通过关联或创建时的 cardId/AbilityIds 策略实现）

#### Scenario: 编辑能力并持久化
- **WHEN** 用户编辑某个已存在能力并保存
- **THEN** 系统 MUST 调用 `PUT /api/abilities/{id}` 持久化能力配置（含 `jsonConfig`）
- **AND** 保存成功后卡牌能力列表 MUST 刷新并回显最新能力信息

#### Scenario: 关联/解除关联现有能力
- **WHEN** 用户选择一个现有能力并将其关联到卡牌
- **THEN** 系统 MUST 调用 `POST /api/cards/{cardId}/abilities/{abilityId}` 完成关联
- **WHEN** 用户移除某能力
- **THEN** 系统 MUST 调用 `DELETE /api/cards/{cardId}/abilities/{abilityId}` 完成解除

### Requirement: 编辑器对枚举序列化差异具备兼容性
编辑器 MUST 能兼容后端返回的枚举大小写/命名差异，并确保 UI 回显与逻辑判断一致。

#### Scenario: CardType 大小写不一致时仍可正确显示随从属性
- **GIVEN** 后端返回的 `type` 可能为 `Follower` 或 `follower`
- **WHEN** 编辑器渲染卡牌表单
- **THEN** 随从（Follower）卡牌的攻击/生命值编辑区域 MUST 正确显示

