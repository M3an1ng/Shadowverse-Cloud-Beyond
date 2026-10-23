## Context

当前系统中的 `tribe` 由 `CardDefinition.Tribe`、`CardInstance.Tribe`、Buff 附加 `tribe`、编辑器自由输入和 DSL 字符串比较共同驱动，但没有统一的数据源或语义解释层。现状有三个直接问题：

1. 所有 `tribe` 判断都建立在裸字符串比较之上，无法稳定处理别名、规范化和保留值。
2. 特殊 `tribe`（例如“具有所有已知 tribe”）没有统一解释点，如果继续沿用直接字符串比较，会同时破坏 DSL、Buff、历史选择和 UI。
3. 口袋银河这类效果需要从“本局离场时具有类型的从者”中按 `tribe` 去重选牌；当前 `List<string>` 无法直接作为稳定去重键，而且多 `tribe` 卡被选中后如何消费全部 `tribe` 没有统一规则。

本次改动是跨数据存储、运行时语义、DSL 评估和历史选择的交叉修改，适合先收敛成统一设计再实现。

## Goals / Non-Goals

**Goals:**
- 通过数据库配置定义 `tribe` 的规范名、别名和特殊行为，同时允许玩家继续创建新的普通 `tribe`。
- 保持现有卡牌模型和 DSL 表面语法继续使用字符串 `tribe`，避免破坏性语法迁移。
- 引入统一的 `tribe` 语义层，覆盖规范化、包含判断、交集判断、特殊 `tribe` 展开和去重支持。
- 为口袋银河提供基于离场历史快照的 `tribe` 候选展开与多 `tribe` 去重规则。

**Non-Goals:**
- 不将普通 `tribe` 改造成写死枚举或强制外键引用。
- 不重写现有 DSL 查询语法，也不要求现有表达式批量迁移到新函数名。
- 不在本次设计中实现全量历史回滚或通用集合拍平查询语言。
- 不把特殊 `tribe` 在持久化或 DTO 层直接展开成全部普通 `tribe` 列表。

## Decisions

### Decision: 普通 `tribe` 继续以字符串形式存储，数据库只负责注册表与语义配置
- Rationale: 当前卡牌定义、实例、前端编辑器和 DSL 都直接消费字符串 `tribe`。如果改成枚举或外键列表，会同时破坏数据导入、运行时合并、DSL 和编辑器输入链路。
- Approach: 新增 `TribeDefinition` 配置表，字段包含 canonical name、aliases、behavior、isReserved、isEnabled。卡牌本身仍保存 `List<string>`，但写入前统一规范化。
- Alternatives considered:
  - 将 `tribe` 改成枚举：排斥玩家动态新增，不符合需求。
  - 将卡牌 `tribe` 改成外键列表：需要大范围 DTO、ORM、编辑器和 DSL 改造，收益不成比例。

### Decision: 引入 `ITribeRegistry` 和 `ITribeSemantics` 两层
- Rationale: “有哪些 tribe” 与 “这些 tribe 怎么解释” 是两个问题。把两者混在一起，会导致 DSL、Buff、效果和历史选择各自复制规则。
- Approach:
  - `ITribeRegistry` 负责加载数据库配置、别名归一、动态发现未知普通 `tribe`、提供当前已知普通 `tribe` 集合。
  - `ITribeSemantics` 负责 `Contains`、`Expand`、`Overlaps`、`NormalizeMany` 等运行时解释。
- Alternatives considered:
  - 只做一个静态 helper：无法接数据库配置和缓存更新。
  - 每个调用点独立查表：规则分散，难以保持一致。

### Decision: 特殊 `tribe` 以 canonical string 持久化，展开只发生在语义判断阶段
- Rationale: 如果把“全种类”直接存成所有普通 `tribe` 的展开结果，UI 和日志会失真，且后续新增普通 `tribe` 后旧数据无法自动覆盖。
- Approach: 特殊 `tribe` 例如“全种类”仅以自身 canonical string 保存；在 `Contains/Expand/Overlaps` 和历史候选构造时再根据 registry 当前普通 `tribe` 集合做动态展开。
- Alternatives considered:
  - 写入时立即展开：与动态 tribe 集合不兼容，且会污染显示层。

### Decision: DSL 保持原有写法，替换底层 `tribe` 比较实现
- Rationale: 现有 DSL 已大量使用 `self.Tribe == "X"` 和 `contains(self.Tribe, "X")`。修改语法会增加迁移成本，也没有必要。
- Approach:
  - 在 `ExpressionTreeBuilder` 的集合等号特判中，将 `IEnumerable<string> == string` 的 `tribe` 路径改为调用语义层的 `Contains`。
  - 在 `ContainsFunction` 中，对 `tribe` 集合走同一套语义层。
  - 可选新增 `hasTribe` 等函数，但不作为兼容前提。
- Alternatives considered:
  - 强推新 DSL 函数：需要迁移既有表达式，且不会解决旧表达式行为不一致的问题。

### Decision: 口袋银河的 `tribe` 去重走专用历史候选展开流程，而不是扩展通用 `DistinctBy(List<string>)`
- Rationale: 口袋银河需要的是“离场历史卡牌按有效 `tribe` 拆分候选，再按卡牌与 `tribe` 使用状态筛选”，这不是简单的对 `List<string>` 做去重。直接扩展 `DistinctBy` 只会把通用查询语义复杂化。
- Approach:
  - 从 `LeaveFieldEvent`/后续路由快照中读取离场从者名称与 `tribe`。
  - 构建 `HistoricalTribeCandidate(card identity, card name, effective tribe)` 流。
  - 选中一张多 `tribe` 卡后，消费该卡的全部有效 `tribe`，并移除该卡的其余候选。
- Alternatives considered:
  - 只基于墓地或 Destroy 历史：无法覆盖非 Destroy 离场路径。
  - 修改通用 DSL 查询管线支持 `selectMany + distinct(List<string>)`：复杂度高，且只为一个明确业务规则服务，收益不足。

## Risks / Trade-offs

- [Registry 与卡牌字符串数据并存] → 通过统一写入规范化和自动注册机制，确保新写入数据尽量收敛到 canonical name。
- [特殊 `tribe` 基于“当前已知普通 tribe”动态展开，结果可能受配置变更影响] → 将行为明确定义为运行时语义，并在设计与测试中锁定这一点。
- [DSL tribe 判断改走语义层后，旧表达式结果可能比过去“更宽”] → 为 alias 与特殊 `tribe` 场景补集成测试，确认这是期望的兼容增强而不是回归。
- [口袋银河单独走历史候选逻辑，与现有通用 DSL 查询模型分离] → 明确该规则是特定业务选择语义，不把它伪装成通用集合 distinct。
- [新增数据库表与缓存刷新逻辑会扩展 GameDataProvider 责任] → 将 `tribe` 注册/加载接口收敛到独立 provider/registry，并接入现有数据版本刷新。

## Migration Plan

1. 新增 `TribeDefinition` 持久化模型、迁移与仓储访问，并接入 `GameDataProvider` 刷新路径。
2. 从现有 `CardDefinition.Tribe` 数据和种子导入中回填普通 `tribe` 配置，同时插入特殊 `tribe` 配置记录。
3. 在卡牌保存、种子导入与 AddTribe 等入口加入 normalize + auto-register。
4. 在 `CardInstance`、Buff 相关流程和 DSL tribe 判断入口接入 `ITribeSemantics`。
5. 为口袋银河实现专用离场历史候选展开逻辑，并增加多 `tribe`/特殊 `tribe` 场景测试。
6. 完成验证后执行 `openspec validate --strict`，并保留数据库迁移回滚脚本；如需回滚，可先禁用新表读取，再回退语义层调用点。

## Open Questions

- 特殊 `tribe` 的对外 canonical name 是否直接使用“全种类”，还是使用另一保留中文名；该名称一旦确定需要在编辑器与测试中统一。
- 未知普通 `tribe` 的自动注册是否允许任何入口触发，还是仅允许卡牌保存/导入触发而拒绝运行时效果创建新定义。
- 口袋银河最终是否只要求 `tribe` 不重复，还是还要同时约束候选名称不重复；实现前需要与卡面文案解释保持一致。
