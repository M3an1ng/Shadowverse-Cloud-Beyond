## ADDED Requirements

### Requirement: Basic Info 显示 Preview Art 并采用紧凑主字段布局
编辑器 MUST 将 Preview Art 从 Assets 区域移动到 Basic Info，并在 editor-main 右侧保持原比例展示。Basic Info MUST 以紧凑布局展示主字段，不再划分 section 区块，字段顺序为 Name、Cost + Atk + Hp、Type、Rarity、Class。

#### Scenario: 渲染 Basic Info 的主信息与预览图
- **WHEN** 用户在卡牌编辑器中选择一张卡牌并打开 Basic Info
- **THEN** Preview Art MUST 出现在 editor-main 右侧并保持原比例
- **AND** Basic Info MUST 按顺序显示 Name、Cost + Atk + Hp、Type、Rarity、Class 且不再划分 section 区块
- **AND** Assets 区域 MUST 不再显示 Preview Art

### Requirement: Type/Rarity/Class 使用可选式控件
编辑器 MUST 将 Type、Rarity、Class 的输入控件从下拉菜单替换为可选式控件（如按钮组或单选式），并在选择后回显选中状态。

#### Scenario: 选择卡牌类型与稀有度
- **WHEN** 用户在 Basic Info 中选择 Type、Rarity、Class 选项
- **THEN** 系统 MUST 更新卡牌数据并在 UI 中回显选中状态

### Requirement: 补充信息在主信息下方全宽展示
编辑器 MUST 将除主字段与 Preview Art 外的其他信息放置在其下方，并占据 editor-main 的完整宽度。

#### Scenario: 展示补充信息区域
- **WHEN** 用户向下查看 Basic Info
- **THEN** 其余字段与说明 MUST 在主信息与 Preview Art 下方以全宽展示

### Requirement: 提供 editor-main 布局测试页面
系统 MUST 提供一个与现有卡牌编辑器样式一致的测试页面，用于验证 editor-main 新布局且不影响正式卡牌编辑器页面。

#### Scenario: 打开测试页面验证布局
- **WHEN** 用户进入测试页面
- **THEN** 页面 MUST 复用现有样式与数据加载行为并呈现新的 editor-main 布局