## ADDED Requirements

### Requirement: Fusion MUST Allow Selecting Hand Cards As Materials
当一个能力/效果被配置为 Fusion 时，系统 MUST 允许玩家从己方手牌中选择 1..N 张卡牌作为融合素材（N 由配置指定），并在确认后将这些素材从手牌移除并结算去向。

#### Scenario: Player fuses two cards from hand
- **GIVEN** a Fusion step requests the player to select 2 hand cards as materials
- **AND** the selectable pool is limited to the requesting player's hand
- **WHEN** the player selects 2 valid hand cards and confirms
- **THEN** the selected cards MUST be removed from the player's hand
- **AND** the selected cards MUST be moved to the configured destination (default: removed-from-game)

### Requirement: Fusion MUST Expose Material Results For Chained Abilities
Fusion 结算后，系统 MUST 将“素材数量/素材列表”等结果暴露给后续的链式能力（例如通过 outputs 或 triggering event），以便实现“根据融合数值变化”的效果。

#### Scenario: Fused count is available to downstream effects
- **GIVEN** a Fusion step is followed by a chained ability
- **WHEN** Fusion completes with 3 fused materials
- **THEN** the chained ability MUST be able to read `fusedCount = 3` from the execution context

### Requirement: Client MUST Show Fusion Visual Feedback
当 Fusion 发生时，客户端 MUST 给出明确的视觉反馈（至少包含：素材卡从手牌离开/被消失的动画或等效提示，以及日志文本）。

#### Scenario: Fusion triggers hand material animation
- **GIVEN** the player performs a Fusion with hand cards
- **WHEN** the server commits Fusion results
- **THEN** the client MUST visually indicate the selected hand cards were fused
