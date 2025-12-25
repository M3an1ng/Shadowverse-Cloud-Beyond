# Change: 提升能力编辑器 UX（重点：DSL 可视化编辑）并确保能力配置可靠有效

## Why
- 当前能力编辑器面向“普通玩家/策划”仍偏开发者导向：大量配置需要理解 DSL、类型系统、事件/效果参数结构，学习成本高、易填错。
- 现有后端已暴露较丰富的元信息（Effect UI metadata、DSL type metadata、函数签名、事件参数 scope），但前端未充分利用，导致：
  - 效果参数仍需要用户手动选择值类型（数字/文本/布尔）与返回类型；
  - 公式 DSL 的 `expectedType` 常被硬编码，无法随参数类型自适配；
  - 事件参数访问仍缺少“可选字段列表/插入器”，用户不知道可访问哪些参数。
- 缺少统一的“可靠性校验门槛”：配置可能在保存时看似成功，但运行期触发后才暴露语法/类型/缺参问题，反馈链路过长。

## What Changes
- 将能力编辑器升级为“元信息驱动的向导”：
  - 在效果参数、目标查询、条件 DSL、效果 DSL 中自动推断并约束类型；
  - 提供可视化的参数选择器与模板，减少手写 DSL 的需求；
  - 保存前进行严格校验并给出可操作的错误提示，确保“写出来的能力可用”。
- 对后端元信息做必要增强（只增量，不破坏现有）：让前端能无需猜测地渲染控件与推断 `expectedType`。

## Non-Goals
- 不在本变更中重写 DSL 语言本身或引入全新的脚本系统。
- 不追求一次性覆盖所有复杂能力；优先让“常见能力”在低学习成本下可正确配置与运行。

## Impact
- Affected code (planned):
  - Frontend: `src/components/DslEditor.js`, `src/components/ValueConfigEditor.js`, `src/components/EffectConfigEditor.js`, `src/components/TargetConfigEditor.js`, `src/components/ConditionConfigEditor.js`
  - Backend: `Server/Services/EffectUIMetadataService.cs`, `Server/Services/AbilityEditorMetadataService.cs`（必要时扩展 DTO）
- Affected specs (new): `ability-editor-ux`, `dsl-editor-ux`, `effect-ui-metadata`

