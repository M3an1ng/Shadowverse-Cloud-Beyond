## 1. Domain & Service Contract
- [x] 1.1 在 `MatchRoom` 增加 `RoomId` / `ActiveGameId` / `Round`
- [x] 1.2 更新 `IMatchService`：房间方法统一改为 `roomId` 语义
- [x] 1.3 增加 `gameId -> roomId` 反查接口（`GetByGameId` / `TryGetRoomIdByGameId`）

## 2. MatchService Implementation
- [x] 2.1 `Create` 生成 `roomId`（不再把房间ID写入 `gameId` 语义字段）
- [x] 2.2 `Start(roomId, ...)` 生成新 `activeGameId` 并提升回合计数
- [x] 2.3 维护内存映射：`activeGameId -> roomId`
- [x] 2.4 `Close/Cleanup` 时清理对应映射，避免脏引用

## 3. API & Hub Wiring
- [x] 3.1 `MatchController` 响应新增 `RoomId` / `ActiveGameId`，保留旧 `GameId` 兼容
- [x] 3.2 `GameHub` 连接改为优先读取 `roomId`（兼容 `gameId`）
- [x] 3.3 `GameHub` 动作入队改为使用 `activeGameId`
- [x] 3.4 `Context.Items` 明确保存 `RoomId` 与单局 `GameId`

## 4. Runtime & Replay Integration
- [x] 4.1 `GameService.TryGetActiveRoom` 改为通过 `gameId -> roomId` 反查
- [x] 4.2 `ReplayController.HasReplayReadAccess` 改为通过反查 room 进行 joinCode 校验
- [x] 4.3 `DevToolsMatchesController` 返回 `RoomId` / `ActiveGameId`

## 5. Compatibility & Cleanup
- [x] 5.1 过渡期保持旧字段兼容（输入/输出）
- [ ] 5.2 前端切换完成后删除旧 `GameId`（房间语义）分支
- [x] 5.3 更新注释与日志文案，明确 room/game 语义

## 6. Validation
- [ ] 6.1 新建房间成功返回 `roomId`，开局后返回新 `activeGameId`
- [ ] 6.2 同一房间连续开两局，`activeGameId` 不同且均可正常结算
- [ ] 6.3 replay、结算、胜率统计继续按单局 `gameId` 生效
- [ ] 6.4 断线重连与房间关闭提示链路无回归
