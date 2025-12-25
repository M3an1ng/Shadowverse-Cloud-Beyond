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

### Requirement: Client MUST Render Act Button For Eligible Cards
客户端 MUST 为当前可启动的场上卡牌提供 Act 按钮（或等效交互入口），并在不可用时禁用/隐藏以避免误导。

#### Scenario: Act button appears and becomes disabled after use
- **GIVEN** a card on the battlefield is eligible to use Act
- **WHEN** the player uses Act successfully
- **THEN** the client MUST update the card UI to show Act is unavailable for the rest of the turn

