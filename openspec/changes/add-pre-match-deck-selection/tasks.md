## 1. Implementation
- [x] Review current multiplayer panel and singleplayer entry flow.
- [x] Add deck list fetch + selection state shared by panels.
- [x] Add singleplayer panel UI (match style of multiplayer panel).
- [x] Add deck selector UI inside multiplayer panel (pre-room selection).
- [x] Add AI deck selector for singleplayer (separate from player deck).
- [x] Wire selected deck IDs into match creation and deck submission flows.
- [x] Add validation messaging when required selection is missing.

## 2. Tests
- [ ] Add/adjust frontend tests if present for deck selection state.
- [x] Manual verification checklist for singleplayer and multiplayer flows.

### Manual Verification
- Open “进入对战”，选择玩家与 AI 卡组，点击开始对战进入游戏。
- 未选择卡组时点击开始对战，提示缺少卡组。
- 打开“多人对战”面板，选择卡组后创建/加入房间，进入对战成功。
