# Design: Interactive abilities (Choose/Fusion/Act/Reveal)

## Goal
在不破坏现有“事件驱动 + EffectQueue + PendingActionService”的事务模型前提下，引入 4 个需要交互的新能力，并补齐缺失的目标选择/分支选择能力，使其可被 DSL/配置稳定表达并被前端完整呈现。

## Key Decisions
1. **优先复用现有“等待玩家选择”的暂停机制**
   - 仍使用 `PendingActionService.WaitForPlayerChoiceAsync` 作为唯一的“等待玩家输入”机制；
   - 选择结果通过现有 `SubmitTargetChoice`（out-of-band）回传，避免把“恢复事务”放回主动作队列导致死锁。

2. **多选：客户端交互 + 服务端强校验**
   - 客户端提供多选与确认 UI（避免“点一下就提交”导致误操作）；
   - 服务端必须验证：
     - 选中数量在 `[MinSelectionCount, MaxSelectionCount]`；
     - 选中项必须来自服务端提供的可选集合；
     - 目标依旧有效（避免等待期间状态变化导致非法）。

3. **选择不局限战场：以“选择面板”承载跨区域卡牌选择**
   - 当前前端交互默认从战场随从点击选择，但本变更将选择 UI 抽象为“可选目标面板”：
     - 若目标在战场/手牌等已渲染区域，允许直接点击卡牌完成选择；
     - 若目标来自牌堆/墓地/其他不常驻可点击区域，客户端 MUST 展示一个选择面板（列表/弹窗）供玩家完成选择与确认。
   - 服务端发起选择请求时，除 `SelectableTargetIds` 外，MUST 为“不可直接点击的目标”提供可渲染信息（至少包含 card instance id 与 card face 所需信息）。
   - “牌堆选择”必须先由服务端构造候选清单（检索/发现式呈现），客户端不应直接对未知牌堆做点击选择。

4. **Choose：分支选择不强依赖异步 effect handler**
   - 由于当前 `IEffectHandler` 为同步接口，且 `EffectExecutor.ExecuteEffect` 为同步调用，`Choose` 的交互不放在 effect handler 内部等待。
   - 选择逻辑放在“生成效果/能力链”的阶段（例如能力执行的预处理阶段），在进入 EffectQueue 前完成分支决策：
     - `Choose` 的候选项来源：`EffectConfig.ChainedAbilities`（作为分支能力列表）；
     - 选择结果：仅将被选中的分支能力注入待执行链路（其他分支不执行）。
   - **UI 不特殊化**：客户端将 Choose 的选项视为“临时区域（Temporary Zone）”中的可选条目，复用通用 Player Choice UI（与手牌/墓地/候选清单的选择面板一致）。
     - DTO 层 SHOULD 使用 `CardZone.Extra` 表达该临时区域（参考 `Server/Dtos/Enums/GameEnum.cs`），仅用于呈现，不进入真实 `GameState`。

5. **Fusion：以手牌多选为首个落地点，机制可复用于其它区域选择**
   - “选择哪些素材”属于玩家选择，放在目标解析阶段完成（可等待玩家输入）；
   - `FusionEffectHandler` 只做确定性结算：
     - 校验选中卡牌属于请求玩家的手牌；
     - 移出手牌并产生相应领域事件（卡牌移动/移出游戏等），素材默认**移出游戏**而非进入墓地；
     - 将“融合素材数量/列表”作为 outputs 暴露给后续链式能力。

6. **Act：基于 Activated 能力类型，新增显式请求入口**
   - 使用 `AbilityType.Activated` 表达“启动型能力”；
   - 客户端对满足条件的场上卡牌渲染 Act 按钮；
   - 新增 Hub 请求（例如 `RequestAct`），服务端验证：
     - 必须是己方回合、卡牌在场、该能力可用、且本回合未使用；
   - 成功后触发能力执行，并广播“Act 已使用/可用性更新”的增量事件以驱动 UI。

7. **Reveal：以新事件显式广播可见信息**
   - `RevealEffectHandler` 产生专用的“卡牌公开”事件（包含被公开的 card instance id 与必要的定义信息）；
   - 前端用专门的动画/展示层呈现，并将该信息并入回放/事件日志。

8. **TargetSelector：补齐 effect 级目标筛选能力**
   - 当前 `EffectConfig.TargetSelector` 在 `Ability.CreateEffectsAsync` 内为 TODO 且返回空列表，会破坏“一个能力多目标组 + 多 effect”的常用建模方式；
   - 本变更把其作为必要前置：提供一个可落地的“从 ResolvedTargets 选择子集”的执行器（最小实现优先）。

## Open Questions (for review)
- `Choose` 是否需要支持“显示分支名称/描述/图标”的编辑器与运行时契约？本变更建议最小：使用 `AbilityConfig.Name/Description`。
- `Fusion` 对“素材是否公开”是否有需求？默认素材离开手牌属于公共信息（可通过移动事件/动画体现）。
- `Reveal` 若支持“仅对自己公开”，需要区分广播范围（to group vs to player group）。
