# Change: Add Replace Deck Effect

## Why

当前效果系统可以向牌堆加牌、把牌洗回牌堆、以及让卡牌消失或破坏，但缺少“直接用一组卡牌定义 ID 重建整副牌堆”的原生能力。把这类行为硬套到 `Vanish` 语义上是错误的，因为被替换的旧牌虽然会移出游戏，但它们并不属于“消失”。

## What Changes

- 新增 `ReplaceDeck` 效果，按 `PlayerScope` 对目标玩家执行整副牌堆替换。
- 效果参数接受有序的卡牌定义 ID 列表，并按该顺序重建目标牌堆。
- 目标玩家当前牌堆中的旧牌全部移出游戏，进入 `Removed` 区域，并使用专用的 deck replacement move reason。
- 旧牌移出过程不得复用 `Vanish` / `LeaveField` 语义，不得产生 `Vanish` 事件。
- 补充效果编辑器元数据与测试，覆盖顺序、范围和语义边界。

## Capabilities

### New Capabilities
- `replace-deck-effect`: Provide a dedicated effect that replaces a targeted player's entire deck with newly created cards from an ordered definition ID list.

### Modified Capabilities
- None.

## Impact

- Affected code: `EffectType`, effect params/handler registration, `EffectUIMetadataService`, `CardMoveReason`, deck movement logic, and server tests.
- Affected systems: effect execution pipeline, card factory-backed card creation, card move semantics for `Removed` zone.
- No external API or persistence migration is required.
