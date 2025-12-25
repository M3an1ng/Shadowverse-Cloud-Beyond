## ADDED Requirements

### Requirement: Player Choice MUST Support Multi-Selection With Server Validation
当能力/效果请求玩家从一组可选目标中选择多个目标时，系统 MUST 支持多选，并且服务端 MUST 对选择结果进行强校验（数量与集合包含关系）。

#### Scenario: Player selects multiple hand cards within min/max bounds
- **GIVEN** the server requests a player choice with `MinSelectionCount = 2` and `MaxSelectionCount = 3`
- **AND** the choice provides a selectable target set of hand card instance ids
- **WHEN** the player submits exactly 2 selected target ids
- **THEN** the server MUST accept the submission and resume the paused transaction

#### Scenario: Too few selections are rejected
- **GIVEN** the server requests a player choice with `MinSelectionCount = 2`
- **WHEN** the player submits 1 selected target id
- **THEN** the server MUST reject the submission (no state change committed)

#### Scenario: Out-of-range selections are rejected
- **GIVEN** the server requests a player choice with `MaxSelectionCount = 2`
- **WHEN** the player submits 3 selected target ids
- **THEN** the server MUST reject the submission (no state change committed)

#### Scenario: Submitting ids outside the selectable set is rejected
- **GIVEN** the server requests a player choice with a selectable target set `S`
- **WHEN** the player submits a selected id that is not in `S`
- **THEN** the server MUST reject the submission (no state change committed)

### Requirement: Player Choice MUST Support Cross-Zone Card Selection
玩家选择系统 MUST 支持从多个区域选择卡牌目标（至少包含：手牌、战场、墓地；以及“从牌堆候选清单中选择”的场景）。

#### Scenario: Player selects a graveyard card via a selection panel
- **GIVEN** the server requests a player choice whose selectable targets are cards in the requesting player's graveyard
- **AND** those cards are not directly clickable in the default battlefield UI
- **WHEN** the client displays a selection panel listing the selectable cards
- **AND** the player selects 1 valid card and confirms
- **THEN** the server MUST accept the submission and resume the paused transaction

#### Scenario: Player selects from a deck candidate list (search/discover style)
- **GIVEN** the server requests a player choice with a selectable target set representing a deck candidate list
- **AND** each selectable item includes sufficient data for the client to render card faces
- **WHEN** the player selects 1 candidate and confirms
- **THEN** the server MUST accept the submission and resume the paused transaction

### Requirement: Choice Requests MUST Provide Renderable Info For Non-Clickable Targets
当选择目标不在客户端当前可点击的区域（例如墓地、牌堆候选清单、其他隐藏区域）时，服务端 MUST 在选择请求中提供足够信息以渲染选择面板（至少包含目标实例 ID 与卡面渲染所需信息）。

#### Scenario: Server includes renderable card face info for selection panel items
- **GIVEN** the server requests a player choice with selectable targets not currently visible/clickable in the client
- **WHEN** the client receives the choice request
- **THEN** the request MUST include data to render each selectable target as a card entry in a panel (not only raw ids)

### Requirement: Choose UI MUST Reuse The Generic Player Choice UI (Temporary Zone)
Choose 的前端呈现 MUST NOT 引入“专用/特殊化 UI”。客户端 MUST 将 Choose 理解为一种“临时区域（Temporary Zone）”上的选择，并复用通用的 Player Choice UI（高亮/面板/确认/取消）。

该“临时区域”在 DTO 层 SHOULD 使用 `CardZone.Extra` 表达（参考 `Server/Dtos/Enums/GameEnum.cs`），作为**仅用于呈现**的区域：
- 临时区域中的条目不是实际对局中的卡牌实体，不会进入 `GameState`；
- 条目只存在于一次 `PlayerChoiceRequest` 的 payload 内；
- 该请求 MUST 仅发送给需要做出选择的玩家（避免泄露选项/隐藏信息）。

#### Scenario: Choose options are delivered as temporary-zone selectable entries
- **GIVEN** the server needs a player to choose one of 2 Choose branches
- **WHEN** the server sends a `PlayerChoiceRequest` to the requesting player
- **THEN** the request MUST include `SelectableTargetIds` for the 2 options
- **AND** the request MUST include `SelectableCardInstances` entries for each option (title and optional description)
- **AND** each option entry SHOULD be represented as a card-like item whose `Zone` is `Extra` (temporary/presentation-only)

#### Scenario: Client renders Choose options using the same selection panel and submits the selected id
- **GIVEN** a `PlayerChoiceRequest` that includes 2 selectable entries in the temporary zone (`CardZone.Extra`)
- **WHEN** the client displays the standard choice overlay/panel (no dedicated Choose modal)
- **AND** the player selects one entry and confirms
- **THEN** the client MUST submit the selected id via the same choice submission pipeline
- **AND** the server MUST resume the paused transaction based on that selected id

### Requirement: Client MUST Provide a Choice UI With Confirm/Cancel
客户端 MUST 提供用于目标选择与选项选择的 UI，并支持确认与取消；取消行为 MUST 与服务端请求的 `IsCancellable` 保持一致。

#### Scenario: Client confirms selection then clears highlight state
- **GIVEN** the client is awaiting a player choice
- **WHEN** the player confirms a valid selection
- **THEN** the client MUST submit the choice result to the server
- **AND** the choice UI MUST exit and clear all selection highlights

#### Scenario: Cancel is only allowed when IsCancellable is true
- **GIVEN** the client is awaiting a player choice with `IsCancellable = false`
- **WHEN** the player attempts to cancel
- **THEN** the client MUST NOT submit a cancellation and MUST keep the choice UI active
