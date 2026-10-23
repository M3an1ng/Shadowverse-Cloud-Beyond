## Context

当前系统对卡牌每回合有限次数动作采用“按动作硬编码”的实现方式：

- `CardInstance` 同时持有 `AttacksThisTurn/MaxAttacksPerTurn`、`ActsThisTurn/MaxActsPerTurn`、`FusionsThisTurn/MaxFusionsPerTurn`
- `PlayerState` 在回合开始时分别重置三套字段
- `AttackEvent`、`ActInvokedEvent`、`FusionEvent` 各自消耗次数并发出不同事件
- `SetAttackTimes` 仍通过事件直接写 `card.MaxAttacksPerTurn`

这种设计在动作种类增多时会不断复制同类代码，也使“动作次数上限”游离于 Buff 派生体系之外。另一方面，动作自身是否可执行并不只由次数决定：攻击还要看 Rush/Storm/目标合法性，Act/Fusion 还要看能力是否存在、是否被抑制、区域是否合法。因此，本次设计需要同时明确“次数系统负责什么”和“次数系统不负责什么”。

## Goals / Non-Goals

**Goals:**
- 为 `Attack`、`Act`、`Fusion` 建立统一的每回合动作次数状态模型。
- 明确实体、Buff、动作请求/提交、效果处理器之间的管理边界。
- 将“每回合动作上限”纳入 Buff 派生链，使常驻与临时次数修正都走同一套模型。
- 保持现有动作业务规则与客户端交互语义稳定，降低迁移风险。
- 为后续新增同类动作次数修正提供最小增量扩展点。

**Non-Goals:**
- 不重做通用动作权限系统；动作禁止/限制仍由现有权限/前置条件逻辑负责。
- 不将所有卡牌动作统一成一个超泛化动作框架；本次只覆盖 `Attack`、`Act`、`Fusion`。
- 不同时重构前端动作按钮协议或能力句柄模型。
- 不将“动作是否存在”与“次数是否耗尽”混为一层；能力抑制仍属于能力系统职责。

## Decisions

### 1. `CardInstance` 只持有“已使用次数”，不再持有真实上限状态

**Decision**
- `CardInstance` 作为每张卡的运行时聚合根，只负责保存每回合已消耗的动作次数。
- 实体内部引入统一的动作类型枚举和使用次数存储，例如 `Attack`、`Act`、`Fusion` → `usedCount`。
- 原有 `AttacksThisTurn` / `ActsThisTurn` / `FusionsThisTurn` 可以保留为兼容包装属性，但底层收口到同一套状态。
- `MaxAttacksPerTurn` / `MaxActsPerTurn` / `MaxFusionsPerTurn` 不再是可直接持久写入的真实来源，而改为派生读取接口。

**Rationale**
- 已使用次数天然属于实体运行时状态，必须随事务提交、复制、回合重置而变化。
- 上限则有基础值、常驻能力、临时效果、光环、沉默等派生来源，更适合由 Buff/派生服务统一计算。

**Alternatives considered**
- 将“已使用次数”和“上限”都搬进 Buff：会把事务消耗状态错误地建模成派生效果，难以处理攻击后即时递增。
- 继续维持三套显式字段：实现最省事，但会继续复制逻辑，无法从根上解决扩展成本。

### 2. 新增专用的 `UsageLimitBuffEffect`，而不是复用 `StatBuffEffect`

**Decision**
- 为每回合动作上限引入专用 Buff effect，包含：
  - 动作类型（`Attack` / `Act` / `Fusion`）
  - 修正层级（如 Additive / Set）
  - 数值
- BuffService 新增针对动作上限的派生计算入口，不把它混入 `Attack/Health/Cost` 这类普通数值属性。

**Rationale**
- 动作上限在语义上更接近“能力配额”而不是纯 stat。
- 若直接扩充 `StatType`，会默认复用现有 stat 派生与沉默行为，语义容易混淆。
- 专用 effect 能保持模型精简，同时明确该能力属于“动作使用规则”子域。

**Alternatives considered**
- 复用 `StatBuffEffect` 并新增 `StatType.MaxAttacksPerTurn` 等字段：实现短平快，但会把动作配额混进普通属性体系，边界不清。
- 为每种动作单独建 `AttackTimesBuffEffect` / `ActTimesBuffEffect` / `FusionTimesBuffEffect`：过于分散，重复定义同构结构。

### 3. 动作次数系统只负责“配额”，不负责动作合法性的全部判断

**Decision**
- 次数系统统一回答：
  - 当前动作已使用多少次
  - 当前动作每回合上限是多少
  - 当前动作是否还有剩余次数
- 攻击的召唤失调、目标合法性、护盾/守护校验仍留在攻击流程。
- Act/Fusion 的能力存在性、能力抑制、区域限制仍留在各自请求/提交逻辑。

**Rationale**
- 这能避免把次数模型膨胀成新的“万能动作权限系统”。
- 也能与已有 `RemoveAbilityEffect`、AbilityManager 抑制判断自然协作：能力不存在或被抑制时，动作不可用；即使还有剩余次数也不能执行。

**Alternatives considered**
- 让统一次数服务接管完整动作权限：职责过重，且与已有 action restriction / ability suppression 体系重叠。

### 4. 回合重置只重置“已使用次数”，不重置“上限”

**Decision**
- `PlayerState` 在回合开始时统一重置卡牌动作使用次数。
- 上限不在回合开始写回默认值，而是始终通过派生计算读取当前有效值。

**Rationale**
- 若上限来自 Buff 或静态能力，回合重置不应覆盖其来源。
- 这可以消除当前 `ResetToDefinitionState` / turn-start / direct mutation 之间的隐式耦合。

**Alternatives considered**
- 保留“回合开始重置已使用次数 + 有条件重置上限”的混合模型：规则复杂，且容易再次引入特判。

### 5. `SetAttackTimes` 迁移为“生成使用上限 Buff”，但不强制一次性统一所有效果接口

**Decision**
- `SetAttackTimesEffectHandler` 的职责改为生成攻击动作的 usage-limit buff。
- 原有 `AttackTimesUpdatedEvent` 不再作为直接写实体字段的核心路径，可迁移为兼容事件或由更通用的状态更新事件替代。
- 第一阶段优先完成服务端内部统一，不要求同步发明所有动作的对称 effect handler。

**Rationale**
- 这能先把最明显的状态写入异味移除掉。
- 同时避免本次提案范围扩张为“新增整套 Act/Fusion 次数 effect DSL”。

**Alternatives considered**
- 一次性新增 `SetActTimes`、`SetFusionTimes`、通用 UI 元数据与所有效果编辑器支持：收益明确，但不是完成这次边界重构的前置条件。

### 6. 保持外部事件和 DTO 兼容，优先统一服务端内部模型

**Decision**
- 第一阶段允许继续保留 `AttackStateUpdatedEvent`、`ActStateUpdatedEvent`、`FusionStateUpdatedEvent` 这些对外事件。
- 服务端内部通过统一 helper 生成对应状态更新事件，避免前端协议一起大改。

**Rationale**
- 用户请求的重点是系统边界和 OpenSpec 提案，不是一次性重构客户端协议。
- 保持外部接口稳定能降低应用阶段风险。

**Alternatives considered**
- 立即统一成一个 `CardActionUsageUpdatedEvent`：内部更整洁，但会扩大 DTO / Hub / 前端联动面。

## Risks / Trade-offs

- [Risk] 攻击次数历史上已被多个事件、DTO 和测试直接引用旧字段。  
  → Mitigation: 保留兼容属性名与已有对外事件，先在内部统一实现。

- [Risk] Buff 派生上限与实体已使用次数分离后，复制/重置逻辑容易遗漏。  
  → Mitigation: 明确复制只复制已使用次数；上限统一通过派生读取，不再复制真实上限值。

- [Risk] 沉默对动作次数上限是否生效存在语义争议。  
  → Mitigation: 在实现前固定规则：来自能力/效果的动作上限 Buff 默认受沉默影响；纯动作限制系统不在本次混入。

- [Risk] 若设计过于通用，会演变成新的动作框架。  
  → Mitigation: 将动作类型限定为 `Attack`、`Act`、`Fusion`，不开放任意字符串动作类型。

## Migration Plan

1. 在 `CardInstance` 中引入统一的动作使用状态读写接口，并保留现有公开字段的兼容包装。
2. 在 Buff 层新增动作上限 effect 与派生计算入口。
3. 将攻击、启动、融合的校验与消耗逻辑改为通过统一接口读取上限并更新已使用次数。
4. 将 `SetAttackTimes` 从直接写状态迁移为生成攻击动作上限 Buff。
5. 调整回合开始重置、复制、快照、映射和测试，使其只依赖统一动作使用状态。
6. 在稳定后，再评估是否需要统一对外 usage-state 事件。

## Open Questions

- “卡牌天生每回合可攻击/启动/融合 X 次”是否需要定义层显式基础值，还是通过常驻能力/Buff 统一表达即可？
- 沉默移除后，若原卡本身拥有常驻多次攻击能力，是否应立即恢复原基础上限？
- 是否需要在第一阶段就补一个通用的 `SetActionUsageLimit` 效果，还是先只迁移 `SetAttackTimes` 足够？
