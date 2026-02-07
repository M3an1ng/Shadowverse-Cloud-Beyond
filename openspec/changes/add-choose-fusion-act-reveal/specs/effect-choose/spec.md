## ADDED Requirements

### Requirement: Choose MUST Execute Exactly One Branch
当配置了 `Choose` 分支能力时，系统 MUST 请求玩家选择其中一个分支，并且 MUST 只执行被选中的分支（其它分支 MUST NOT 执行）。

#### Scenario: Choose selects one of two branches
- **GIVEN** an ability includes a Choose step with 2 branch abilities (A and B)
- **WHEN** the player chooses branch A
- **THEN** the system MUST execute only branch A effects
- **AND** branch B effects MUST NOT be executed

### Requirement: Choose Options MUST Be Displayable To The Player
Choose 的分支选项 MUST 具备可供客户端展示的信息（至少包含标题，建议包含描述），以便玩家可以理解选择后果。

#### Scenario: Branch options include title and description
- **GIVEN** a Choose step is presented to the player
- **WHEN** the server sends the choice request to the client
- **THEN** each option MUST include a display title
- **AND** each option SHOULD include a short description

### Requirement: Choose MUST Be Presented As A Temporary-Zone Player Choice (Not Special UI)
Choose 的交互 MUST 被建模为一次标准的 Player Choice：候选项以“临时区域（Temporary Zone）”条目呈现，并复用通用选择 UI，而不是引入专用的 Choose 弹窗/控件。

#### Scenario: Choose uses temporary-zone entries for options
- **GIVEN** the system requests a choice between multiple branches
- **WHEN** the server constructs a `PlayerChoiceRequest`
- **THEN** each branch option MUST be represented as a renderable temporary-zone entry (via `SelectableCardInstances`)
- **AND** the DTO `Zone` for those entries SHOULD be `CardZone.Extra` (presentation-only)

### Requirement: Choose Cancellation MUST Be Deterministic
当 Choose 请求允许取消时（`IsCancellable = true`），玩家取消选择时，系统 MUST 以确定性的方式处理中断（例如中止该能力/动作），并且 MUST NOT 产生部分提交的状态变更。

#### Scenario: Cancelling a cancellable Choose aborts without partial commit
- **GIVEN** a Choose step is cancellable
- **WHEN** the player cancels the choice
- **THEN** the current transaction MUST be cancelled or aborted deterministically
- **AND** no partial branch execution MUST occur
