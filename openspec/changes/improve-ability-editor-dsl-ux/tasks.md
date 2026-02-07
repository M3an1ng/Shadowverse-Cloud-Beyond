## 0. Discovery (Baseline)
- [x] 0.1 梳理 AbilityEditor 的数据流：`AbilityEditor`(wizard) → `TriggerConfigEditor`/`TargetConfigEditor`/`ConditionConfigEditor`/`EffectConfigEditor`，明确各步骤输入/输出结构与保存点。
  - 保存点：`AbilityEditor.buildFinalConfig()` 生成 `jsonConfig`（`CardEditor.createNewAbilityFromConfig` 直接透传 `config.jsonConfig` 到 `/api/abilities`）。
- [x] 0.2 梳理 DSL 编辑入口与 expectedType 来源：
  - `ConditionConfigEditor`（条件表达式，当前 `expectedType='Boolean'`）
  - `TargetConfigEditor`（起始卡池/筛选条件，当前 `expectedType='List'/'Boolean'`）
  - `ValueConfigEditor`（公式值，当前 `expectedType` 被硬编码为 `Int32`）
- [x] 0.3 记录当前能力保存前校验覆盖面与缺口：哪些校验在各子编辑器内做了、哪些依赖 DslEditor 但未汇总到 AbilityEditor 的最终保存门槛。
  - 现状：`AbilityEditor.validateCurrentStep()` 只汇总 trigger/conditions/effects 的“字符串级/浅层校验”，未把 `DslEditor` 的语义校验（类型匹配/AST 完整性/字段合法性）纳入统一保存门槛。

## 1. Specification
- [x] 1.1 定义能力编辑器“可靠性门槛”与保存前校验规则（错误分级：error/warn/info）。
- [x] 1.2 定义 DSL 可视化编辑“结构化节点”（函数调用节点、参数槽位、expectedType 传播）。
- [x] 1.3 定义“触发事件参数选择器”行为：按事件类型+scope 展示可访问字段并插入表达式（不暴露内部函数名）。
- [x] 1.4 定义效果参数 UI 自动适配：按 `controlType + TypeName` 生成控件与默认值，并约束可输入类型。

## 2. Backend (Metadata)
- [x] 2.1 扩展 Effect UI metadata：`EffectParameterMetadataDto` 增加 `TypeName`（并支持泛型/可空的清理表示）。
- [x] 2.2 若 `controlType=Dropdown`，提供 `EnumName` 或 `OptionsProviderKey`，并在 `AbilityEditorMetadata` 中聚合可用选项源。
  - 已完成：Effect 参数 metadata 支持 `EnumName`/`OptionsProviderKey` 字段并在 Dropdown 时推断 enum 名称。
  - 已完成：在 `GET /api/AbilityEditorMetadata` 返回 `enumOptionSources` / `optionsProviderKeys`（标识符列表）。
  - 已完成（占位实现）：新增 `GET /api/abilityEditorOptions/{key}` 以支持 OptionsProviderKey 下拉渲染；目前默认返回空 options（未来如出现非 enum 下拉需求，再实现具体 provider）。
- [x] 2.3 为“触发事件参数访问”提供机器可用的字段标识（name/type/scope/displayName/description），确保前端无需硬编码。
  - 已完成：Event metadata 已提供 `PropertyName/DisplayName/Description/TypeName/Scope`（TypeName 清理与 scope 透传），前端字段选择器已接入（见 3.5）。

## 3. Frontend (Ability Editor UX)
- [x] 3.1 `EffectConfigEditor`：按 `controlType` 渲染参数控件；仅在需要时使用 `ValueConfigEditor`。
  - [x] 3.1.1 调研并列出效果参数的真实 CLR 类型分布（是否为 ValueConfig/标量/复杂对象），明确每种 `controlType` 的数据形态与落库结构。
    - 结论：效果参数类多为标量/枚举（如 `int/bool/string/enum/int?`），但运行期允许配置为 `ValueConfig`（`EffectExecutor` 会在执行时对 `ValueConfig` resolve，否则视为静态值），因此前端可对基础控件直接写入标量值。
  - [x] 3.1.2 实现 `Checkbox/NumberStepper/TextInput/TextArea/Dropdown` 的基础渲染；Dropdown 支持 `EnumName`（本地化 enum endpoint）与 `OptionsProviderKey`（待后端实现）。
    - 已完成：基础控件渲染 + EnumName 下拉选项加载。
    - 已完成：OptionsProviderKey 的后端占位 options endpoint 与前端接入（默认空 options）。
  - [x] 3.1.3 仅当参数类型为 ValueConfig 或需要公式表达式时才使用 `ValueConfigEditor`。
    - 已完成：`controlType=valueConfigEditor` 才使用 `ValueConfigEditor`；其余控件写入标量值（并兼容读取 StaticValueConfig）。
- [x] 3.2 `ValueConfigEditor`：根据 `TypeName` 自动选择“静态值控件类型”，并将公式 DSL 的 `expectedType` 传入 `DslEditor`（移除/降级当前依赖 `typeof Value` 的类型选择器，避免与参数真实类型冲突）。
- [x] 3.3 `DslEditor`：支持“函数调用节点 + 参数槽位”，并让 `expectedType` 在编辑过程中传播与约束可选项（把“函数=一元包装/字符串拼接”的现状升级为可编辑参数槽位）。
  - 已完成：新增 `functionCall` AST 节点与参数槽位渲染/编辑（占位符 + 子编辑保存写回 + 保存时参数类型校验）。
- [x] 3.4 `DslEditor`：为集合编辑提供更精确的 expectedType（从 `List` 升级为 `List<T>`/`IEnumerable<T>`，并在 `.query/.orderBy` 中自动提供 `self:T` 上下文）。
  - 已完成：Target 起始卡池传入 `List<CardInstance>`；DslEditor 支持基于 inner type 的 `self:T` 上下文覆盖。
- [x] 3.5 `DslEditor`：增加“触发事件参数选择器”与插入器（按 scope 过滤），并提供中文字段说明；前端通过显式上下文参数把当前触发事件（类型+时序）传入 DSL 编辑器，避免 DslEditor 内部猜测。
  - 已完成：在条件/目标筛选/效果公式 DSL 中透传 `triggeringEventContext`，并提供 `eventField` 选项（UI 标签不暴露 `eventInt/eventBool/...`，插入表达式仍使用内部函数）。
- [x] 3.6 `AbilityEditor`：把 DslEditor 的语义校验结果汇总到“保存前校验门槛”，替换当前过于宽松的字符串级检查（做到定位到具体 effect/param/condition/target filter）。
- [x] 3.7 增加可读化预览：将 DSL AST 渲染为更接近自然语言的提示文本（不改变表达式本身）。

## 4. Validation
- [x] 4.1 单测/前端自检：不同参数类型下静态/公式输入均能正确约束并生成合法配置。
- [x] 4.2 单测/集成：保存前校验能阻断缺参/语法错/类型错，并给出可操作提示。
  - 已完成：`dotnet test` 覆盖后端（72 tests）。
  - 已完成：前端侧最小自动化验证（Jest）：`dslValidation` 基础用例 + DslEditor elevate 行为；并将需要真实后端的 `card_save_flow` 设为默认 skip（需设置 `API_BASE` 才运行）。
- [~] 4.3 自动化 + 手动：用 3 类“常见能力”验证端到端可用（优先自动化覆盖关键逻辑，手动路径用于体验确认）。
  - [x] 4.3.0 自动化：新增后端“快乐路径”集成测试，覆盖伤害/抽牌/条件触发的核心执行链（Trigger → ResolveTargets → ExecuteEffect → Commit events）。
    - [x] 4.3.0.1 伤害：PlayCard 触发 → 选择敌方随从 → 造成伤害后生命值变化（通过 commit 事件落地）。
    - [x] 4.3.0.2 抽牌：PlayCard 触发 → 目标为 player → 抽牌事件落地（Deck→Hand 迁移）。
    - [x] 4.3.0.3 条件触发：Damage 事件触发 → 条件 DSL 使用 `eventInt("Damage")` 与常量比较 → 条件通过才生效。
  - [ ] 4.3.1 手动（普通玩家路径）伤害：效果选择“造成伤害”，参数“伤害值”用静态数值；目标选择敌方随从；保存并在对战中触发验证扣血。
  - [ ] 4.3.2 手动（普通玩家路径）抽牌：效果选择“抽牌”，参数“抽牌数量”用静态数值；保存并在对战中触发验证抽牌。
  - [ ] 4.3.3 手动（普通玩家路径）条件触发：添加一个条件（Boolean DSL），从“触发事件字段”插入一个数值字段并与常量比较；保存并验证条件生效。

## 5. Bugfix + UX Hardening (Post-Test)
- [x] 5.1 修复 `validateDslExpression` 的 expectedType 判定：类型不匹配必须产出 error（不可再因字段名错误导致静默通过）。
- [x] 5.2 AbilityEditor 保存前校验补齐目标起始卡池 expectedType（应为 `List<CardInstance>`），与 TargetConfigEditor/DslEditor 的期望一致。
- [x] 5.3 DslEditor 保存门槛强化：保存当前层时同时做 AST 语义校验 + 槽位 expectedType 校验（main/filter/take/function arg）。
- [x] 5.4 DslEditor 子表达式编辑修复：点击函数参数/封装表达式时能正确进入槽位编辑，并能正确写回父槽位（避免 `slot=innerAST` 写回失败）。
- [x] 5.5 类型系统一致性修复：将 `Number` 视为数值类型，避免数值表达式完成后后续操作项缺失。
- [x] 5.6 解析体验修复：避免重复括号（如输入 `(5)` 被解析后显示为 `((5))`），并补齐对基础字面量的类型推断（Int32/Double/Boolean/String）。
- [x] 5.7 Jest：新增/更新用例覆盖 expectedType 不匹配报错、`(5)` round-trip、以及子表达式保存/继续编辑的关键路径。
