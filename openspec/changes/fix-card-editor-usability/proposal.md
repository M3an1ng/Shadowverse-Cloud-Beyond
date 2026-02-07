# Change: 修复“卡牌编辑器”可用性并补齐端到端数据链路（Card + Ability）

## Why
- 当前主界面“卡牌工坊/卡牌编辑器”无法形成闭环：前端可渲染 UI，但缺少关键 API 与持久化实现，导致新增/保存/删除、能力关联等关键动作不可用或返回占位数据。
- 前后端对枚举序列化/大小写约定不一致（例如 `Follower` vs `follower`），会导致 UI 判断与下拉默认值错配，出现“随从属性不显示/无法正确回显”等问题。
- 前端存在明显的实现断点（未绑定保存按钮事件、调用不存在的方法、关键字枚举加载方法名不一致），使编辑器在交互路径上出现运行时错误。

## What Changes
- 定义并落地“卡牌编辑器 MVP”的最小可用链路：
  - 卡牌：列表读取、详情读取、编辑、保存（更新）、另存为（创建新卡）、删除。
  - 能力：按卡牌查看能力列表、创建能力（持久化）、编辑能力（持久化）、将现有能力关联/解除关联到卡牌。
- 统一/兼容编辑器所需的枚举序列化与本地化选项，使 UI 能正确回显并进行逻辑判断（允许兼容不同大小写来源，避免破坏性全局修改）。
- 补齐前端缺失的按钮/事件绑定与明显的运行时错误点，确保关键交互路径可通。

## Impact
- Affected specs (new): `card-editor`
- Affected code (planned):
  - Frontend: `src/cardEditor.js`, `src/metadataManager.js`,（以及能力编辑相关组件在需要时）
  - Backend: `Server/Controllers/CardsController.cs`, `Server/Controllers/AbilitiesController.cs`（必要时增加 DTO/Repository/测试）
- Data migration required: 视能力创建落库方式而定（若复用现有 `CardAbilities` 表则通常不需要结构迁移；仅需确认字段与约束）。

