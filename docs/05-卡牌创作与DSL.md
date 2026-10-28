# 卡牌创作与 DSL

> 核对日期：2026-09-27。编辑器与运行时元数据是字段及可选项的依据；旧 DSL 设计稿只用于历史追溯。

## 从哪里操作

进入大厅的卡牌工坊（`card-editor`），编辑卡牌属性、能力与素材；牌组编辑（`deck-builder`）负责构筑而不是修改卡牌定义。独立 DSL 页面路由为 `dsl-editor-v2`。

操作后至少验证：保存成功 → 重新打开内容一致 → 预览正常 → 在对局或实验室中触发相关规则。界面能显示表达式，不等于服务端能解析或结算它。

## 数据结构

| 概念 | 作用与依据 |
|---|---|
| 卡牌定义 | [CardDefinition](../Server/Domain/Entities/CardDefinition.cs)：目录中的卡牌属性；传输对象见 [CardDefinitionDto](../Server/Dtos/CardDefinitionDto.cs) |
| 卡牌实例 | [CardInstance](../Server/Domain/Entities/CardInstance.cs)：单局中的状态与实例身份，不等同于定义 ID |
| 能力配置 | [AbilityConfig](../Server/Domain/Abilities/AbilityConfig.cs)：结构化 JSON，部分值/条件/目标使用 DSL |
| 能力运行实例 | [Ability](../Server/Domain/Abilities/Ability.cs)、[AbilityFactory](../Server/Domain/Abilities/AbilityFactory.cs)：将配置用于具体上下文 |
| 效果参数 | [参数目录](../Server/Domain/Abilities/Effects/Handlers/Parameters/) 与对应处理器：字段的实际消费方 |

当前 `AbilityConfig` 包含 `AbilityKeywords`、`ActivateCost`、`AlternatePlay`、`Trigger`、`PreConditions`、`Conditions`、字典式 `Targets`、`Properties`、`Effects`、`Auras`、`IsEnabled`、`BroadcastOnTrigger`、`BuffDisplay` 等。`PreConditions` 是出牌前条件，不要与触发时的普通条件混为一谈。

不要照搬历史文档中的整段 JSON 或枚举清单。添加配置字段时同时核对序列化、元数据、编辑器读写和运行时使用，避免出现“保存了但没生效”的字段。

## 解析与编辑链路

1. 浏览器编辑入口为 [DslEditor.js](../src/components/DslEditor.js)，会使用 `dsl_editor` 下的节点、流水线和编辑会话模块。
2. [DslController](../Server/Controllers/DslController.cs) 暴露 `POST /api/dsl/parse`；输入结构与错误响应直接查看控制器和 DTO。
3. 服务端语法、AST、函数、求值逻辑位于 [DSL 目录](../Server/Domain/Abilities/DSL/)。
4. [editing-session.js](../src/components/dsl_editor/editing-session.js) 管理编辑路径与父槽回写；[pipeline-editor.js](../src/components/dsl_editor/pipeline-editor.js) 处理流水线编辑交互。
5. [EffectConfigEditor](../src/components/EffectConfigEditor.js)、[StatusConfigEditor](../src/components/StatusConfigEditor.js) 分别承接效果和状态配置。

编辑器改动应保持主表达式、局部节点与保存值一致。不要在某个 UI 分支创建独立的第二份表达式真值。验证步骤见 [开发与验证](02-开发与验证.md)。

## 元数据与本地化

- [AbilityEditorMetadataController](../Server/Controllers/AbilityEditorMetadataController.cs) 与 [AbilityEditorMetadataService](../Server/Services/AbilityEditorMetadataService.cs)：编辑器元数据入口。
- [DslMetadataService](../Server/Services/DslMetadataService.cs)：DSL 类型与函数元数据。
- [EffectUIMetadataService](../Server/Services/EffectUIMetadataService.cs) / [EventMetadataService](../Server/Services/EventMetadataService.cs)：效果、事件相关信息。
- [LocalizationController](../Server/Controllers/LocalizationController.cs) / [enumLocalizationCache.js](../src/enumLocalizationCache.js)：枚举显示与本地化。

扩展效果时先阅读相邻效果的参数类型、元数据和测试，再接入编辑器。字段存在、可配置、能保存、能执行是四个需要分别验证的环节。

## 修改后最小验证

- 解析：有效表达式输出符合预期，无效输入返回明确错误。
- 编辑：打开、修改子节点、切换视图、回到主表达式，保存值一致。
- 数据：保存后重新获取，卡牌/能力修订通知与缓存更新一致。
- 运行：触发条件、目标玩家、目标区域、持续时间和决策分支至少覆盖本次改动场景。
