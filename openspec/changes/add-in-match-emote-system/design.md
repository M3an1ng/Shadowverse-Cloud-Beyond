## Context

当前项目已经有对战内聊天能力，聊天消息通过 `GameHub.SendChatMessage` 进入服务端，并通过房间组广播 `ChatMessage`、连接后同步 `ChatHistory`。前端聊天界面由 `src/ui/logPanel.js` 在 `game-chat-content` 中动态构建，现有结构是消息列表、发送状态和文本输入表单。

表情系统应作为现有聊天能力的扩展，而不是第二套通讯系统。它需要解决两类问题：
- 资源管理：表情图片由固定目录维护，按一级文件夹自动分组，不需要每次新增表情都改代码。
- 消息传输：客户端只能发送受信任清单中的表情标识，服务端校验后广播结构化表情消息，避免任意 URL 注入。

项目当前静态文件服务已经能暴露工作区下的 `assets/` 路径。表情图片应继续走静态文件服务和浏览器缓存；服务端清单 API 只返回元数据，不读取图片二进制。

## Goals / Non-Goals

**Goals:**
- 从固定目录读取表情包，并按一级文件夹名自动分组。
- 提供表情清单 API，返回分组、表情 ID、展示名和静态 URL。
- 在对战聊天 UI 中增加表情入口、分组 tab 和表情网格。
- 点击表情后立即发送表情消息，复用现有房间广播、历史同步、发送者信息和限流机制。
- 聊天列表和弹幕能够展示表情图片，并在图片不可用时给出稳定占位。
- 保持文本消息的安全渲染约束，不把聊天正文或文件名作为 HTML 注入。

**Non-Goals:**
- 不实现最近使用、收藏、搜索、用户上传、排序管理或表情市场。
- 不把表情清单写入数据库。
- 不通过 SignalR 传输图片二进制或 base64。
- 不为表情创建独立 Hub。
- 不把表情消息写入回放或长期聊天持久化，除非后续聊天系统整体引入持久化。

## Decisions

### 1) Use a server-owned emote catalog service

新增一个服务端表情清单服务，例如 `IEmoteCatalogService` / `EmoteCatalogService`。它负责扫描固定目录并返回规范化清单。

建议固定目录：
```text
assets/emotes/
  default/
    smile.png
    angry.webp
  line-style/
    hello.gif
```

扫描规则：
- 只读取 `assets/emotes` 下的一级子目录。
- 一级子目录名即分组 `packId` / `packName`。
- 根目录下散落图片不纳入清单。
- 空目录不返回。
- 支持扩展名白名单：`.png`、`.jpg`、`.jpeg`、`.webp`、`.gif`、`.avif`。
- 分组和文件按自然排序，保证 UI 顺序稳定。

备选方案：在前端维护静态 JSON manifest。  
放弃原因：每次新增表情需要改 manifest 或重新构建，不能满足“动态读取固定路径文件夹”的目标。

### 2) Return metadata only; never load image bytes in the catalog API

新增清单接口，例如：
```http
GET /api/emotes
```

返回结构建议：
```json
{
  "packs": [
    {
      "id": "default",
      "name": "default",
      "items": [
        {
          "id": "default/smile.png",
          "packId": "default",
          "name": "smile",
          "url": "/assets/emotes/default/smile.png"
        }
      ]
    }
  ]
}
```

API 不返回物理路径，不读取图片内容，不生成 base64。图片加载交给静态文件服务、HTTP 缓存、浏览器图片缓存和 Service Worker（生产环境）处理。

### 3) Cache the catalog metadata, not image binaries

服务端可以缓存目录扫描结果，建议初始策略是短 TTL 内存缓存：
- TTL：30-60 秒。
- 缓存内容：分组和文件元数据。
- 不缓存图片 `byte[]` 或 `Stream`。
- TTL 到期后下一次请求重新扫描。

原因：
- 目录扫描通常很快，但聊天面板可能被频繁打开。
- 图片本体缓存交给静态文件系统和浏览器更合适。
- 短 TTL 足以支持“把文件放进目录后较快生效”，不需要第一版引入 `FileSystemWatcher` 的复杂度。

备选方案：启动时生成清单并永久缓存。  
放弃原因：动态维护体验较差，需要重启或手动刷新。

备选方案：监听文件系统变化实时刷新。  
暂不采用原因：实现和跨平台边界更复杂，第一版需求不需要实时秒级生效。

### 4) Treat emotes as structured chat messages

不要把表情伪装成文本内容，也不要让客户端提交图片 URL。扩展现有聊天 DTO，让消息有明确类型。

发送 DTO 建议增加：
- `MessageType`: `"text"` 或 `"emote"`
- `EmoteId`: 客户端选择的表情 ID

广播 DTO 建议增加：
- `MessageType`
- `EmoteId`
- `EmoteName`
- `EmoteUrl`

行为：
- 文本消息继续使用现有 `Content` 校验和清洗。
- 表情消息要求 `EmoteId` 非空，且必须能在服务端当前表情清单中解析。
- 服务端根据 `EmoteId` 填充 `EmoteName` 和 `EmoteUrl`。
- 表情消息的 `Content` 可为空，或设置为表情展示名用于兼容旧 UI；前端应以 `MessageType` 为准。

备选方案：客户端直接发送 URL。  
放弃原因：安全边界差，容易注入任意外链或非表情资源。

### 5) Reuse current chat hub, history, and rate limiting

表情发送应通过现有 `SendChatMessage` 入口或同一 Hub 内的等价入口完成，并复用：
- 房间上下文解析。
- 玩家/观战者身份。
- 房间关闭检查。
- 每参与者限流。
- 内存聊天历史。
- `ChatMessage` 实时广播。
- `ChatHistory` 历史同步。

这能保证表情消息和文本消息拥有一致的房间隔离、重连恢复和 UI 顺序。

是否独立新增 `SendEmoteMessage` 可以在实现时选择，但服务端内部应归并到同一消息创建和广播流程，避免分叉两套历史和风控逻辑。

### 6) Add an emote catalog loader on the frontend

新增轻量前端模块，例如 `src/emoteCatalog.js`：
- `loadEmoteCatalog()` 请求 `/api/emotes`。
- 页面生命周期内缓存成功结果。
- 失败时缓存错误状态一小段时间，避免持续重试。
- 提供按 `id` 查找表情的 helper，供 UI 乐观展示或错误处理使用。

前端缓存只用于减少重复请求；服务端仍是最终可信来源。发送表情时只传 `EmoteId`，不传 URL。

### 7) Embed the picker inside `game-chat-content`

当前聊天区是紧凑战斗面板，表情 UI 不应长期占用消息列表高度。推荐结构：

```text
chat-message-list
chat-send-status
chat-emote-panel      // hidden by default
chat-input-form
  chat-emote-btn
  chat-input
  chat-send-btn
```

交互规则：
- 点击表情按钮打开/关闭面板。
- 首次打开时加载表情清单。
- 默认选中第一个非空分组。
- 分组 tab 直接使用文件夹名。
- 点击表情立即发送，不需要再点击“发送”。
- 发送失败使用现有 `chat-send-status` 展示错误。
- 离开聊天 tab、离开对战页或重渲染时关闭面板并保留文本草稿。

布局规则：
- 桌面端：面板嵌在聊天内容中，固定高度约 180-220px，内部滚动。
- 窄屏/触控端：按钮和表情格子需要接近 44px 触控目标。
- 表情格子固定尺寸，图片 `object-fit: contain`，避免不同图片尺寸造成网格跳动。

### 8) Render emote messages with image elements and safe fallbacks

聊天列表渲染：
- `MessageType === "text"`：沿用现有 `.chat-message-text`，使用文本节点。
- `MessageType === "emote"`：渲染 `.chat-message-emote`，包含 `<img>`。
- `img.src` 必须来自服务端广播的 `EmoteUrl`。
- `alt` 使用 `EmoteName` 或 `EmoteId` 的安全文本。
- `onerror` 后显示一个固定尺寸占位，避免消息行塌陷。

弹幕渲染：
- 表情消息可以显示缩小图片和发送者信息。
- 若图片加载失败，显示表情名占位。
- 弹幕仍受现有队列长度和页面生命周期控制。

### 9) Keep path validation strict on the server

目录扫描必须以工作区根路径下的 `assets/emotes` 为唯一根目录。

服务端路径策略：
- 使用 `Path.GetFullPath` 解析表情根目录和候选文件。
- 确认候选文件完整路径仍以表情根目录为前缀。
- 忽略隐藏目录、系统文件、不支持扩展名和空文件名。
- URL 使用规范化的 `/assets/emotes/<encoded-pack>/<encoded-file>` 形式。
- `EmoteId` 使用 `<pack>/<file>`，不允许客户端传入 `..`、绝对路径、反斜杠路径或 URL。

### 10) Do not introduce new dependencies

第一版可以使用现有 ASP.NET Core、浏览器 DOM API 和现有前端模块组织完成，不需要新增 npm 包或 .NET 包。图标入口优先使用已有按钮/SVG 风格，不引入独立图标库。

## Risks / Trade-offs

- [Risk] 大量 GIF 或大尺寸图片会造成面板打开慢、聊天列表解码卡顿。  
  → Mitigation：清单只加载元数据；图片使用 lazy loading；UI 固定尺寸；后续可增加文件大小警告或缩略图约定。

- [Risk] 表情目录变化后，客户端缓存清单与服务端校验结果短暂不一致。  
  → Mitigation：发送时以服务端校验为准；失败时提示“表情不存在或已移除”，并允许前端刷新清单。

- [Risk] 表情消息扩展现有聊天 DTO，旧客户端可能只显示空文本。  
  → Mitigation：保留 `Content` 兼容字段为表情名；前端以 `MessageType` 渲染，新旧字段共存一段时间。

- [Risk] 文件夹名直接展示可能不够友好。  
  → Mitigation：第一版接受文件夹名即分组名；后续如有需要再引入可选 manifest，不影响当前目录扫描能力。

- [Risk] 表情面板占用聊天区域，降低对战中查看历史消息效率。  
  → Mitigation：面板默认关闭，固定高度，内部滚动；离开聊天 tab 自动关闭。

- [Risk] 如果把 UI 逻辑继续堆进 `logPanel.js`，文件职责会更重。  
  → Mitigation：清单加载独立为 `emoteCatalog.js`；`logPanel.js` 只保留面板状态、DOM 渲染和事件绑定。

## Migration Plan

1. 新增 `assets/emotes/` 目录约定和少量测试/示例资源，确保开发环境能访问静态 URL。
2. 新增服务端表情清单 DTO、清单服务和 `/api/emotes` 控制器。
3. 扩展聊天发送 DTO 和广播 DTO，支持 `text` / `emote` 消息类型。
4. 扩展 `GameHub` 的聊天发送流程，增加表情校验、消息构造、历史写入和广播。
5. 新增前端 `emoteCatalog.js`，负责请求和缓存清单。
6. 扩展 `gameManager.js`，提供发送表情消息的方法并解析表情消息字段。
7. 扩展 `src/ui/logPanel.js`，增加表情按钮、分组面板、表情网格和表情消息渲染。
8. 扩展 `src/styles/game.css`，定义表情面板、格子、消息图片和失败占位样式。
9. 补充后端目录扫描/校验测试和前端清单渲染/点击发送测试。
10. 手动验证玩家、观战者、历史回放、房间隔离、目录变更、图片加载失败和移动端触控布局。

Rollback strategy:
- 若表情清单 API 或 UI 出现问题，可隐藏表情按钮并保留文本聊天不变。
- DTO 扩展字段应保持可选，回滚前端 UI 时不影响文本消息发送与接收。

## Open Questions

- 表情固定目录最终是否采用 `assets/emotes/`，还是需要放在 `public/emotes/` 或配置项中。
- 分组排序是否只按文件夹名自然排序，还是需要约定数字前缀如 `01-default`。
- 是否需要对单张表情文件大小设置服务端扫描级上限提示；第一版建议先不阻断，只过滤扩展名。
- 表情弹幕是否默认开启；当前设计倾向于沿用聊天弹幕规则，但若动图干扰过强，可在实现时只在聊天列表展示动图，弹幕使用静态缩略表现。
