# UI Design: Daily Activity Quests in Top Navigation

## Context

当前主菜单顶部导航使用右上角 `.top-nav-bar` 承载轻量入口。现有模式包括：

- `.icon-btn` 作为顶部图标按钮入口。
- `.user-card-popup` 作为悬停/固定弹出面板，宽度约 `316px`，黑色半透明背景、细边框、blur、轻微上滑进入。
- 用户信息卡内已有 `.user-header-resources` 和金币资源项，金币增长通过 `.resource-coin.is-gain-pulse` 展示 `+N` 浮动提示。
- 现有 `#meta-progression-panel` 已能展示元进度和任务，但更像开发/调试面板：需要手动输入 UserId，信息密度和文案不适合作为正式日常任务入口。

本 UI 设计面向 `add-activity-quest-system`：非对局行为不再直接给金币，而是推进任务进度。因此顶部导航需要从“金币即时到账反馈”转为“任务进度、任务完成、可领取奖励”的反馈模型。

## Goals

- 在 `top-nav-bar` 中提供日常任务的稳定入口。
- 让用户无需打开完整元进度面板，也能知道是否有任务可领取。
- 对评论、点赞、登录、观战、对战等行为提供清晰但不打扰的任务反馈。
- 保持当前 HUD 风格：黑色玻璃、技术字体、青色信息态、金币色奖励态、紧凑布局。
- 动画只在状态变化时出现，不使用持续跳动或常驻闪烁。

## Non-Goals

- 不设计大型任务中心页面。
- 不引入新前端框架或组件库。
- 不替换现有用户信息卡、金币资源位和对局结算日志。
- 不把任务说明写成新手教程；界面只显示当前状态和必要操作。

## Recommended Top Nav Structure

在 `.top-nav-bar` 中新增独立任务 widget，位置建议在用户头像入口左侧或金币/用户入口相邻处：

```text
top-nav-bar
  [battle customization] [daily quest] [about] [user]
```

推荐独立入口，而不是塞进用户卡片，原因：

- 任务有“可领取”强提醒需求，隐藏在用户卡内会降低可发现性。
- 任务状态会被社区、登录、观战、对局等行为频繁更新，需要独立反馈锚点。
- 金币资源位只表达账户余额，不应承载任务完成状态，避免“行为直接给金币”的旧心智回流。

### Trigger Button

任务入口使用与现有 `.icon-btn` 一致的按钮骨架：

- 图标：使用 SVG checklist/clipboard/check-circle 风格，不使用 emoji。
- 默认态：低亮青色描边或白色弱透明。
- 有进度更新态：短暂青色 pulse。
- 有可领取任务态：右上角数字 badge，使用金币色或暖黄色。
- 全部完成且无可领取态：图标变为 check-circle，但不持续发光。

状态优先级：

```text
loading > claimable > just-completed > in-progress > empty/error
```

Badge 规则：

- `0` 不显示。
- `1-9` 显示数字。
- `10+` 显示 `9+`，避免撑开按钮。
- Badge 必须不改变按钮尺寸。

## Daily Quest Panel

任务面板沿用 `.user-card-popup` 的弹出模式，不使用全屏 modal。建议宽度 `360px`，移动端 `min(360px, 92vw)`。

```text
┌──────────────────────────────────────┐
│ DAILY QUESTS              [刷新]     │
│ 今日进度 2/3       重置 05:42        │
├──────────────────────────────────────┤
│ 可领取                                │
│ [✓] 每日互动        +80 金币 [领取]   │
│     10/10  ██████████                │
├──────────────────────────────────────┤
│ 进行中                                │
│ [投票] 赞/踩不同卡牌 +80 金币         │
│        6/10  ██████░░░░              │
│ [评论] 发表评论     +100 金币         │
│        1/3   ███░░░░░░░              │
└──────────────────────────────────────┘
```

### Header

Header 内容：

- Kicker：`DAILY`
- 标题：`日常任务`
- 次要状态：`今日 2/3`、`可领取 1`
- 重置时间：`重置 05:42`
- 操作：小型 `刷新` 按钮

设计规则：

- `可领取` 数量比 `今日 2/3` 更重要，使用金币色。
- 重置时间使用 mono 字体，弱化显示。
- 刷新按钮保留现有 `.meta-btn` 视觉，但尺寸收敛。

### Quest Row

每个任务是一条紧凑 row，而不是大卡片。字段顺序：

```text
icon / title / reward
description or source hint
progress bar + current/target
action/status
```

状态文案：

- 未开始：`0/10`
- 进行中：`6/10`
- 刚更新：`6/10 +1`
- 已完成未领取：`已完成`
- 已领取：`已领取`
- 已过期：`已过期`

按钮规则：

- 未完成：不显示按钮，显示进度。
- 已完成未领取：显示 `领取`。
- 领取中：按钮 disabled，显示 `领取中`。
- 已领取：不显示按钮或显示弱化 `已领取` 标签。

### Progress Bar

进度条应是固定高度、固定宽度，不因数字变化导致布局跳动。

建议样式：

- Track：`rgba(255,255,255,0.08)`
- Fill 默认：青色 `var(--menu-accent-cyan)`
- Fill 可领取：金币色 `#f5cf6e`
- 高度：`4px`
- radius：`2px`
- 动画：只在数值变化时从旧宽度过渡到新宽度，`220ms ease-out`

## Animation and Feedback

### 1. Progress Updated

触发条件：评论、点赞、登录、观战、对局等行为使任务进度增加，但任务未完成。

反馈：

- top-nav 任务按钮执行一次青色 pulse，持续 `520ms`。
- panel 已打开时，对应任务 row 右侧显示短暂 `+1`。
- panel 未打开时，在任务按钮附近显示小型 toast：`任务进度 +1`。

动效：

```text
0ms     badge/trigger 轻微亮起
80ms    +1 label 出现，opacity 0 -> 1，translateY(4px -> 0)
520ms   trigger pulse 结束
900ms   +1 label 淡出
```

### 2. Quest Completed

触发条件：任务进度达到目标，但奖励未领取。

反馈：

- top-nav 任务按钮 badge 增加。
- 任务按钮使用金币色边缘闪光一次。
- toast 文案：`任务完成，可领取奖励`
- panel 已打开时，该任务 row 移动或标记到 `可领取` 分组，领取按钮高亮。

动效：

- 使用 `transform` 和 `opacity`，避免改变 layout 属性。
- 不使用循环 bounce。
- 总时长控制在 `900ms` 内。

### 3. Reward Claimed

触发条件：用户点击任务 row 的 `领取`，后端返回成功和新金币余额。

反馈：

- 任务 row 从可领取态切换到已领取态。
- 顶部金币资源位复用现有 `.resource-coin.is-gain-pulse`，显示 `+N`。
- 任务按钮 badge 减少。
- toast 文案：`已领取 +80 金币`。

注意：金币动画只在领奖成功后触发；评论、点赞等普通行为不得触发金币增长动画。

### 4. Error / Duplicate / No Progress

重复行为被去重时不显示错误，也不显示“+1”。如果 panel 已打开，可在任务 row 下方短暂显示弱提示：

- `该卡今日已计入`
- `今日登录已计入`
- `该对局已计入`

接口失败：

- 刷新失败：panel header 下显示 `任务加载失败，稍后重试`。
- 领取失败：按钮恢复，row 下显示错误文本。
- 不使用 alert。

## Accessibility

- 任务入口按钮必须有 `aria-label`，例如 `日常任务，1 个奖励可领取`。
- Badge 变化应同步更新 `aria-label`，避免只靠视觉提示。
- Panel 打开时，触发按钮 `aria-expanded="true"`。
- 任务进度条使用 `role="progressbar"`，并设置 `aria-valuemin`、`aria-valuemax`、`aria-valuenow`。
- 领取按钮必须可键盘聚焦，focus ring 使用青色边框。
- 动画尊重 `prefers-reduced-motion: reduce`：
  - 禁用 pulse、float、row reorder animation。
  - 保留状态文本和数字变化。

## Responsive Behavior

Desktop：

- panel 从任务按钮右对齐弹出。
- 最大高度不超过视口高度减去顶部边距，任务列表内部滚动。

Mobile `<= 480px`：

- panel 宽度 `calc(100vw - 24px)`。
- 右侧对齐改为 `right: 12px`。
- 任务 row 中标题和奖励允许换行，但按钮保持最小 `44px` 触控高度。
- 重置时间可折叠到第二行。

## Visual Tokens

保持现有风格，不引入大面积新色：

- Background: `rgba(10, 10, 10, 0.86)` or existing popup background.
- Border: `rgba(255,255,255,0.10)`.
- Primary info: `var(--menu-accent-cyan)`.
- Reward: `#f5cf6e` / `#ffed9a`.
- Success: `rgba(134, 239, 172, 0.94)`.
- Warning: `rgba(251, 191, 36, 0.96)`.
- Error: `rgba(255, 120, 120, 0.9)`.
- Body font: existing `Rajdhani` / `JetBrains Mono`.
- Radius: `3px` to `6px` for controls and rows; avoid large rounded cards.

## Data Contract Needed by UI

任务入口和面板至少需要：

```json
{
  "dailyQuestSummary": {
    "total": 3,
    "completed": 1,
    "claimable": 1,
    "resetAtUtc": "2026-07-03T16:00:00Z"
  },
  "quests": [
    {
      "id": "guid",
      "name": "每日互动",
      "description": "赞/踩 10 张不同卡牌",
      "activityType": "Vote",
      "category": "Daily",
      "currentProgress": 6,
      "targetValue": 10,
      "coinReward": 80,
      "isCompleted": false,
      "isRewardClaimed": false,
      "isExpired": false,
      "periodKey": "2026-07-03"
    }
  ]
}
```

行为响应建议携带轻量进度摘要：

```json
{
  "questProgress": {
    "updatedQuestIds": ["guid"],
    "completedQuestIds": [],
    "claimableCount": 1,
    "message": "任务进度 +1"
  }
}
```

领奖响应建议携带：

```json
{
  "userQuestId": "guid",
  "coinReward": 80,
  "currentCoins": 1280,
  "claimedAtUtc": "2026-07-03T10:20:00Z"
}
```

## Implementation Notes

- 可以复用 `.user-card-popup` 的进入/退出模式，新增 `.daily-quest-card` 做宽度和内部布局覆盖。
- 不建议继续扩展现有 `#meta-progression-panel` 作为正式任务 UI；它可以保留为开发/调试面板。
- 金币增长动画应继续由统一的金币更新函数触发，但触发来源必须是任务领奖成功，而不是社区行为成功。
- 任务 toast 应挂在 top-nav 附近，避免遮挡卡牌详情、对战按钮或主菜单路线卡。
- 若同一时间多个任务更新，toast 合并显示：`任务进度已更新`，panel 内逐项显示具体变化。

## Acceptance Checklist

- [ ] 顶部任务入口在无任务、进行中、可领取、加载、错误状态下都有明确视觉反馈。
- [ ] 评论/点赞成功只显示任务进度反馈，不触发金币增长动画。
- [ ] 任务完成时可领取 badge 更新，并提供非动画文本反馈。
- [ ] 领奖成功后金币资源位显示 `+N`，任务 badge 同步减少。
- [ ] panel 在 375px、768px、1024px、1440px 宽度下不溢出屏幕。
- [ ] 所有交互控件支持键盘访问和可见 focus。
- [ ] `prefers-reduced-motion` 下仍能理解状态变化。
