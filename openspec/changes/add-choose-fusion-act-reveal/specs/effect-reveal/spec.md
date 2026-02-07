## ADDED Requirements

### Requirement: Reveal MUST Broadcast Revealed Card Information
当执行 Reveal 时，系统 MUST 向客户端广播被公开卡牌的信息，使双方玩家都能看到“被公开了什么卡”。

#### Scenario: Reveal a card from hand to both players
- **GIVEN** a card in a player's hand is selected for Reveal
- **WHEN** the Reveal step is committed
- **THEN** both players' clients MUST receive an event that identifies the revealed card instance id
- **AND** the event MUST include sufficient data to render the revealed card face (at least card definition id and name)

### Requirement: Reveal MUST Not Desync Hidden Information
Reveal MUST 只改变“可见性”，不应导致手牌/牌库等隐藏区域的数量或顺序发生非预期变化。

#### Scenario: Reveal does not change hand size
- **GIVEN** a player reveals 1 card from their hand
- **WHEN** Reveal completes
- **THEN** the revealing player's hand size MUST remain unchanged
- **AND** the opponent's hand size MUST remain unchanged

### Requirement: Client MUST Show Reveal Visual Feedback
客户端 MUST 提供公开动画/展示效果（翻面、浮层或高亮等），并能在短时间内让双方玩家清晰感知公开结果。

#### Scenario: Client displays a reveal overlay
- **GIVEN** the client receives a reveal event with card face data
- **WHEN** the event is processed
- **THEN** the client MUST display a reveal UI element (overlay/animation) showing the revealed card

