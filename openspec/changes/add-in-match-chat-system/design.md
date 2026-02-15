## Context
当前项目已基于 `GameHub` 完成对战与观战实时事件广播，连接鉴权、分组路由与重连流程都在该 Hub 内。新增聊天应优先复用现有机制，避免引入第二条实时通信链路。

## Goals / Non-Goals
- Goals:
  - 提供同房间（同 `GameId`）实时聊天能力，覆盖双方玩家与观战者。
  - 保障房间隔离、基础风控和可读错误反馈。
  - 保持改动可渐进上线，先内存实现再考虑持久化。
- Non-Goals:
  - 不实现私聊、全局大厅聊天、频道管理。
  - 不实现复杂审核系统（仅基础清洗和限流）。
  - Phase 1 不把聊天写入回放/持久化数据库。

## Decisions

### 1) 通信层：复用 `GameHub`
- 在 `GameHub` 增加 `SendChatMessage`，并通过 `Clients.Group(SignalRGroupNames.Game(gameId))` 广播。
- 不新增 `ChatHub`，减少鉴权和连接状态重复实现。

### 2) 身份模型
- 发送者类型：
  - `Player`: 使用现有 `playerId + token` 连接路径。
  - `Spectator`: 使用现有 `spectate=1` 连接路径（以及已启用时的 `joinCode` 校验）。
- 在 `OnConnectedAsync` 中写入连接上下文：
  - `ParticipantType`（Player/Spectator）
  - `ParticipantId`（玩家 Guid 或观战连接标识）
  - `DisplayName`（优先用户昵称，缺省回退为玩家/观战者标签）

### 3) 数据模型
- `SendChatMessageDto`
  - `ClientMessageId`（可选，前端去重/回执关联）
  - `Content`
- `ChatMessageDto`
  - `MessageId`（服务端生成）
  - `GameId`
  - `SenderType`
  - `SenderId`
  - `SenderName`
  - `Content`
  - `SentAtUtc`
- `ChatHistoryDto`
  - `Messages`（最近 N 条，按时间升序）

### 4) 聊天服务
- 新增 `IGameChatService`：
  - `AppendMessage(gameId, message)`
  - `GetRecentMessages(gameId, limit)`
  - `CanSend(participantKey)`（限流）
  - `Clear(gameId)`（房间关闭后清理）
- Phase 1 使用内存有界队列（每房间 100~200 条）。

### 5) 安全与风控
- 服务端强校验：
  - 去除首尾空白后为空：拒绝。
  - 长度上限：例如 200 字符。
  - 控制字符剔除或替换。
- 基础限流：
  - 建议默认 `3条/5秒/参与者`。
- 前端渲染仅使用文本节点（禁止 innerHTML 插入聊天正文）。

### 6) 生命周期
- 连接建立：
  - 加入游戏组后立即下发最近历史。
- 房间关闭：
  - 拒绝新发送，返回统一错误码/错误消息。
  - 清理该 `GameId` 的聊天缓存。

### 7) 顶部弹幕播报（一次性）
- 触发时机：
  - 客户端收到 `ChatMessage` 实时事件后触发弹幕播报（包含自己发送成功后的回流消息）。
- 表现规则：
  - 弹幕显示在对战界面顶部区域，单条消息从左到右滚动一次后自动消失。
  - 弹幕为“增强展示层”，不替代聊天列表；聊天列表仍是完整可回看记录。
- 队列策略：
  - 若短时间多条消息到达，前端按到达顺序排队播放，避免重叠不可读。
- 生命周期约束：
  - 仅在对战页面激活时播放；离开对战页后清空弹幕队列与 DOM，防止跨页面残留。

## API / Event Flow
1. 客户端调用 `SendChatMessage(dto)`  
2. Hub 解析连接上下文并执行校验/限流  
3. Hub 构造 `ChatMessageDto` 并写入 `IGameChatService`  
4. Hub 向 `Game_{gameId}` 广播 `ChatMessage`  
5. 新连接在完成初始校验后收到 `ChatHistory`
6. 客户端在收到 `ChatMessage` 后将消息入弹幕队列，执行一次顶部滚动动画

## Risks / Trade-offs
- 风险：聊天与战斗高频事件共用同一 Hub，峰值下可能竞争。
  - 缓解：限制消息体大小、限流、避免复杂后处理。
- 风险：仅内存存储导致重启丢失历史。
  - 缓解：Phase 1 接受该行为，后续可扩展持久化实现。
- 风险：高频消息导致弹幕排队过长影响可读性。
  - 缓解：限制弹幕最大排队长度，超出后丢弃最旧未播放项或合并提示。

## Migration Plan
1. 添加 DTO 与 `IGameClient` 方法签名。  
2. 实现 `IGameChatService` 并注入 DI。  
3. 扩展 `GameHub` 聊天入口与历史同步。  
4. 前端对战页接入聊天面板与 SignalR 事件。  
5. 前端接入顶部弹幕队列与动画播放，绑定路由生命周期清理。  
6. 联调并验证限流/隔离/观战/弹幕场景。

## Open Questions
- `DisplayName` 是否直接复用用户系统昵称，还是统一使用“玩家1/玩家2/观战者”。
- 是否要求 Phase 1 把聊天记录写入回放流水（当前建议不做）。
