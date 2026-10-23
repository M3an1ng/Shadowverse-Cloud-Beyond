## Why

当前卡牌动作次数模型将 `Attack`、`Act`、`Fusion` 的“已使用次数”和“每回合上限”分别散落在 `CardInstance`、事件提交、回合重置和效果处理器中，导致同一类规则被实现三次，且 `SetAttackTimes` 仍直接写实例状态而非进入 Buff 派生链。随着“常驻多次攻击”和更多次数修正效果出现，系统需要一个统一且边界清晰的动作次数 Buff 模型，避免继续在实体和效果层堆叠补丁。

## What Changes

- 引入统一的卡牌动作次数能力模型，覆盖 `Attack`、`Act`、`Fusion` 三类每回合有限次数动作。
- 将卡牌实体中的“每回合已使用次数”收口为统一的动作使用状态，由实体负责读写与回合重置。
- 将“每回合动作上限”改为由 Buff 派生计算，新增专用的动作次数上限 Buff 效果，而不是继续把它视为普通 stat 或直接写实例字段。
- 将攻击、启动、融合的校验与消耗流程改为通过统一动作次数接口读取上限并消耗次数，保留各动作自身的业务前置条件与目标合法性判断。
- 将 `SetAttackTimes` 从“直接修改卡牌状态”调整为“施加攻击次数上限 Buff”的语义，为后续同类效果复用同一模型。
- 保持客户端现有动作可用性语义稳定；本次变更不要求同时重构动作按钮、能力句柄或通用权限系统。

## Capabilities

### New Capabilities
- `card-action-usage-limits`: 定义卡牌动作次数的统一状态边界、Buff 派生上限、回合重置和效果集成规则。

### Modified Capabilities
- None.

## Impact

- Affected code:
  - `Server/Domain/Entities/CardInstance.cs`
  - `Server/Domain/Entities/PlayerState.cs`
  - `Server/Domain/Buffs/*`
  - `Server/Domain/Events/AttackEvent.cs`
  - `Server/Domain/Events/ActInvokedEvent.cs`
  - `Server/Domain/Events/FusionEvent.cs`
  - `Server/Domain/Events/ActionRequests/ActRequestedEvent.cs`
  - `Server/Domain/Events/ActionRequests/FusionRequestedEvent.cs`
  - `Server/Domain/Abilities/Effects/Handlers/SetAttackTimesEffectHandler.cs`
  - `Server/Domain/Events/AttackTimesUpdatedEvent.cs`
  - related DTO / mapping / tests
- Affected behavior:
  - Attack/Act/Fusion per-turn quota evaluation
  - turn-start usage reset
  - effect-driven attack-times updates
- Compatibility:
  - Existing client-facing action semantics remain unchanged
  - Internal state and Buff responsibilities are refactored to a unified model
