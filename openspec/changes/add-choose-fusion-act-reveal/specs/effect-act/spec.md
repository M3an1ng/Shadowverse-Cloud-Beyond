## ADDED Requirements

### Requirement: Act MUST Be Usable Once Per Turn Per Card
当卡牌在战场上具有启动型能力（Act）时，控制者在自己的回合中 MUST 至多发动一次；同回合第二次发动 MUST 被拒绝。

#### Scenario: Act can be used once per turn
- **GIVEN** a follower on the battlefield has an Act ability
- **AND** it is the follower owner's turn
- **WHEN** the owner uses Act successfully
- **THEN** the server MUST mark this card's Act as used for the current turn

#### Scenario: Act cannot be used twice in the same turn
- **GIVEN** the card has already used Act this turn
- **WHEN** the owner attempts to use Act again
- **THEN** the server MUST reject the request
- **AND** no effects MUST be executed

### Requirement: Act MUST Require Explicit Player Request And Server Validation
Act 的发动 MUST 由玩家显式发起请求，服务端 MUST 验证请求合法性（己方回合、卡牌在场、能力存在且可用等）。

#### Scenario: Opponent cannot use your Act
- **GIVEN** a card with an Act ability is controlled by player A
- **WHEN** player B attempts to activate that card's Act
- **THEN** the server MUST reject the request

### Requirement: Act MUST Be Split Into Request And Commit Events
Act 的发动 MUST 使用请求事件与可提交事件分离：请求事件仅做校验，不得修改状态；可提交事件负责消耗费用与标记 Act 已使用，并触发 ActInvokedEvent。

#### Scenario: Act request creates a committed Act event
- **GIVEN** a valid Act request for a card with an Act ability
- **WHEN** the server validates the request
- **THEN** the system MUST create a committable Act event
- **AND** Act usage and PP deduction MUST occur only on commit

#### Scenario: Act request fails without mutation
- **GIVEN** an invalid Act request
- **WHEN** the server validates the request
- **THEN** the system MUST reject the request
- **AND** no Act usage MUST be consumed
- **AND** no PP MUST be deducted

### Requirement: Act MUST Require Act Ability On The Card
玩家只能对具有启动型能力的卡牌发起 Act；没有 Act 能力的卡牌 MUST 被拒绝。

#### Scenario: Act request on card without Act ability
- **GIVEN** a card without any Activated ability
- **WHEN** the player requests Act on the card
- **THEN** the server MUST reject the request

### Requirement: Act MUST Support Cost And Preconditions
Act 的费用与条件 MUST 通过 AbilityConfig 的 PreConditions 与 EnhanceCost（或兼容别名）配置；费用默认 0，且在提交前进行验证。

#### Scenario: ActCost defaults to zero
- **GIVEN** an Activated ability without EnhanceCost configured
- **WHEN** the player requests Act
- **THEN** the system MUST treat ActCost as 0

#### Scenario: Act preconditions fail
- **GIVEN** an Activated ability with PreConditions that evaluate to false
- **WHEN** the player requests Act
- **THEN** the server MUST reject the request
- **AND** no Act usage MUST be consumed
- **AND** no PP MUST be deducted

#### Scenario: Act cost is higher than available PP
- **GIVEN** an Activated ability with EnhanceCost greater than the player's current PP
- **WHEN** the player requests Act
- **THEN** the server MUST reject the request
- **AND** no Act usage MUST be consumed
- **AND** no PP MUST be deducted

### Requirement: Client MUST Render Act Button For Eligible Cards
客户端 MUST 为当前可启动的场上卡牌提供 Act 按钮（或等效交互入口），并在不可用时禁用/隐藏以避免误导。

#### Scenario: Act button appears and becomes disabled after use
- **GIVEN** a card on the battlefield is eligible to use Act
- **WHEN** the player uses Act successfully
- **THEN** the client MUST update the card UI to show Act is unavailable for the rest of the turn
