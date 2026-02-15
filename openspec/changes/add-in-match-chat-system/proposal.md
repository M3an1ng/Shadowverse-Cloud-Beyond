# Change: Add In-Match Chat System

## Why
当前对局中仅有系统战斗日志，没有玩家/观战者交流通道。多人对战与观战场景需要实时聊天能力以支持协作、互动与观赛体验。

## What Changes
- 新增“房间级聊天”能力：同一 `GameId` 下双方玩家与观战者可实时收发消息。
- 在现有 `GameHub` 中新增聊天方法与事件，不新增独立 Hub。
- 新增聊天消息 DTO 与房间聊天服务（内存历史 + 基础限流 + 内容清洗）。
- 新增前端对战页聊天窗口，支持历史加载、实时追加与发送失败反馈。
- 新增“顶部弹幕播报”：聊天消息成功发送并广播后，在对战界面顶部执行一次从左到右滚动动画。
- 明确聊天与战斗日志分离：聊天消息不混入系统战斗记录。

## Impact
- Affected specs: in-match-chat
- Affected code:
  - `Server/Hubs/GameHub.cs`
  - `Server/Hubs/ClientInterfaces/IGameClient.cs`
  - `Server/Dtos/SignalR/*`（新增聊天 DTO）
  - `Server/Services/*`（新增聊天服务与注册）
  - `src/main.js`, `src/gameManager.js`, `src/ui.js`, `src/styles/game.css`
