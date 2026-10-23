## Context

当前服务端已经支持 `AddDeck`、`Shuffle`、`Tuck`、`Vanish` 等牌堆或离场相关效果，但没有“用指定定义列表整体替换牌堆”的能力。此需求既涉及效果层，也涉及卡牌移动语义：旧牌需要移出游戏，但不能被当作 `Vanish` 处理，否则会错误地产生 `Vanish` 事件和相关判定。

## Goals / Non-Goals

**Goals:**
- 新增 `ReplaceDeck` 效果类型、参数模型和专用 handler。
- 让被替换的旧牌进入 `Removed` 区域，但不触发 `Vanish` / `LeaveField` 语义。
- 用输入的卡牌定义 ID 列表按顺序创建新的牌堆实例。
- 对无效输入快速失败，避免半成功半失败的牌堆状态。
- 补充可验证测试，覆盖范围、顺序和事件语义。

**Non-Goals:**
- 不在本次变更中引入新的批量领域事件来替代现有 `CardMovedEvent`。
- 不支持“部分替换牌堆”或“按过滤条件替换”的扩展语义。
- 不修改 `InitialDeck` / `InitialDeckDefinitionIds` 的统计基线含义。
- 不改动现有 `Vanish`、`Destroy`、`Shuffle` 的既有行为。

## Decisions

- Decision: 新增 `EffectType.ReplaceDeck`、`ReplaceDeckEffectParams`、`ReplaceDeckEffectHandler`，并沿用现有效果注册机制。
  - Reason: 这是一个独立的效果语义，强行复用 `AddDeck` 或 `Vanish` 都会把“重建整副牌堆”和“普通移牌”混在一起，边界不清。
  - Alternative considered: 复用 `AddDeck` 先加新牌再清空旧牌。
  - Rejected because: 不能准确表达“整副替换”的原子意图，也难以约束旧牌的移出语义和新牌顺序。

- Decision: 旧牌通过 `CardMovedEvent` 从 `Deck -> Removed` 移动，并新增专用 `CardMoveReason`（例如 `DeckReplaced`）。
  - Reason: `Removed` 区域已经是合法牌区，直接走原子移牌事件可以复用现有重置、牌堆计数和状态维护逻辑。
  - Alternative considered: 走 `LeaveFieldEvent` / `CardVanishedEvent`。
  - Rejected because: 需求明确要求“移出游戏但不是消失”，复用 `Vanish` 会把语义做错。

- Decision: 新牌使用 `ICardFactory` 创建为全新实例，并按输入定义 ID 列表的顺序重建牌堆。
  - Reason: “替换牌堆”本质上是生成一组新卡实例，而不是移动已有实例；顺序必须可预期，否则效果配置的输入列表没有确定含义。
  - Alternative considered: 复用 `AddCardToDeckEvent`。
  - Rejected because: `AddCardToDeckEvent` 只表达插入，不表达“先清空再重建”，也不天然保证整列表的顺序重建语义。

- Decision: `CardDefinitionIds` 为空或仅包含非正数时直接失败，不做任何牌堆改动。
  - Reason: 这类配置没有业务价值，而且“把整副牌堆替换为空”并不是当前已确认需求。

- Decision: 本次实现先接受事件扇出，不额外引入 `ReplaceDeckEvent` 之类的批处理事件。
  - Reason: 当前需求的重点是语义正确性，不是事件压缩；先走最小可行改动，只有在前端推送或性能证明不足时再演进。

## Risks / Trade-offs

- [事件数量增加] 一次整副替换会产生大量 `CardMovedEvent` 和牌堆计数更新事件 -> 先用测试锁定正确性，后续若证明有噪音或性能问题再引入批处理。
- [顺序语义需要明确] 如果实现时不保证输入列表顺序，配置数据会失去确定性 -> 在 spec 和测试里把“按输入顺序建堆”写成显式要求。
- [错误复用 Vanish 语义的风险] 只看 `Removed` 区域而不看 move reason 的代码可能误判 -> 通过专用 move reason 和测试明确区分 deck replacement 与 vanish。

## Migration Plan

- 这是纯增量变更，不涉及数据库或配置迁移。
- 现有卡牌与效果在未使用 `ReplaceDeck` 前不受影响。
- 若后续需要回滚，只需移除新增 effect type、handler 和 move reason 即可。

## Open Questions

- None.
