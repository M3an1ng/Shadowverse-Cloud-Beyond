# Change: Add In-Match Emote System

## Why

当前对战聊天只能发送文本消息，表达情绪和快速互动的成本较高。对战与观战场景需要接近 QQ/LINE 贴图体验的表情能力，并且表情资源应能通过固定目录动态维护，而不是每次改动都修改前端代码。

## What Changes

- 在对战聊天中新增表情消息类型，允许玩家和观战者从表情面板选择表情并发送到当前房间。
- 新增服务端表情目录清单能力：从固定路径读取一级子文件夹，按文件夹名自动形成表情分组。
- 表情清单只暴露受支持图片文件的元数据和静态 URL，不读取或传输图片二进制内容。
- 扩展现有聊天发送与广播载荷，使文本消息和表情消息可以共享房间广播、历史同步、发送者信息和限流机制。
- 在 `game-chat-content` 聊天界面新增表情入口、分组 tab 和表情网格；点击表情后立即发送，不需要再点击文本发送按钮。
- 聊天列表和弹幕展示应能区分文本消息和表情消息：文本继续以安全文本渲染，表情以受信任清单中的图片 URL 渲染。
- 表情功能第一版明确不包含：
  - 最近使用分组。
  - 收藏、搜索、排序管理。
  - 用户上传表情。
  - 数据库持久化表情清单。
  - 将表情图片二进制通过 SignalR 传输。

## Capabilities

### New Capabilities

- `in-match-emotes`: 为对战聊天提供基于固定资源目录的分组表情清单、表情选择 UI、表情消息发送、实时广播、历史回放和失败回退能力。

### Modified Capabilities

- None.

## Impact

- Affected backend:
  - `Server/Controllers/*` 或新增表情清单控制器
  - `Server/Services/*` 或新增表情清单服务
  - `Server/Dtos/SignalR/Actions/SendChatMessageActionDto.cs`
  - `Server/Dtos/SignalR/Events/ChatMessageEventDto.cs`
  - `Server/Hubs/GameHub.cs`
  - `Server/Hubs/ClientInterfaces/IGameClient.cs` 如需调整客户端事件契约
- Affected frontend:
  - `src/gameManager.js`
  - `src/ui/logPanel.js`
  - `src/styles/game.css`
  - Potentially a new `src/emoteCatalog.js`
- Affected static assets:
  - New fixed directory convention such as `assets/emotes/<group-name>/<file-name>`
  - Supported image formats should be explicitly whitelisted.
- Affected user experience:
  - 对战聊天输入区新增表情入口。
  - 表情面板按文件夹自动分组显示。
  - 表情消息在聊天列表和弹幕中以图片形式展示。
- Security and validation impact:
  - 服务端必须校验 `EmoteId` 来自当前表情清单，不能信任客户端提交的任意 URL。
  - 目录扫描必须防止路径穿越，并过滤隐藏目录、不支持扩展名和空目录。
  - 前端不得把表情名、文件名或聊天正文作为 HTML 注入。
- Performance impact:
  - 服务端可缓存表情清单元数据，但不应缓存图片二进制内容。
  - 图片加载应继续走静态文件服务、HTTP 缓存和浏览器图片缓存。
