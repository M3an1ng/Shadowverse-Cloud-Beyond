# add-devtools-match-lab

## ADDED Requirements

### Requirement: Devtools Match Lab entry
Match Lab SHALL 作为开发者工具中的入口卡片存在，用于进入可配置对战页面。
#### Scenario: Developer opens devtools and sees a Match Lab tool-card
- Given the user is logged in and has devtools access
- When the devtools home renders
- Then a Match Lab tool-card is visible
- And clicking it navigates to `/dev-tools/match-lab`

### Requirement: Devtools match configuration
Match Lab MUST 允许开发者配置并启动 AI 或本地双人对战，并将配置应用到开局参数。
#### Scenario: Developer configures a custom match and starts it
- Given the user opens Match Lab
- When the user selects AI or local two-player mode
- And provides a configuration (starting PP, max PP, starting HP, starting hand size, seed)
- Then the configuration is sent to the devtools start-match API
- And the resulting match uses those parameters at game start

### Requirement: Access control
Devtools 专用对战启动接口 MUST 受登录与权限控制保护。
#### Scenario: Non-devtools user attempts to call start-match API
- Given the user lacks devtools permission
- When the user calls the devtools start-match API
- Then the request is rejected with an authorization error

### Requirement: Safety validation
对战启动配置 MUST 进行参数范围校验，避免非法配置导致游戏状态异常。
#### Scenario: Invalid parameters are provided
- Given the user submits a config with values outside allowed ranges
- When the devtools start-match API validates the request
- Then it returns a validation error and does not start the match

