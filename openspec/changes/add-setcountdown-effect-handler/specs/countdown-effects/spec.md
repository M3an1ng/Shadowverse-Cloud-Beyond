## ADDED Requirements
### Requirement: SetCountdown Effect Processing
系统 SHALL 支持 SetCountdown 效果，对目标护符的倒计时进行增减，并生成对应的倒计时变化事件。

#### Scenario: Apply countdown delta to amulet
- **WHEN** 一个能力对护符目标触发 SetCountdown（ChangeAmount = -1）
- **THEN** 系统应生成 CountdownTickEvent 并应用倒计时变化