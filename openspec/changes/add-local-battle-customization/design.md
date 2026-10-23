## Context

当前项目的对战视觉资源以静态打包文件为主，例如主菜单背景直接在 `index.html` 中引用图片资源；前端现有持久化工具 `src/storageFallback.js` 仅覆盖字符串级 `localStorage/sessionStorage` 读写，不适合持久保存图片和音频二进制数据。

这次变更的目标是为“仅当前浏览器生效”的对战桌布和对战背景音乐提供稳定的本地持久化方案，同时保持以下边界：
- 不引入服务端上传、数据库或账号同步。
- 不把桌布/BGM 纳入服务端游戏状态或多人协议。
- 不把简单的本地 cosmetic 设置做成复杂资产系统。

这意味着实现必须完全收敛在前端，并且要处理浏览器音频自动播放限制、对象 URL 生命周期、资源失效回退和存储容量约束。

## Goals / Non-Goals

**Goals:**
- 允许玩家为当前浏览器选择本地图片作为对战桌布，并在刷新后恢复。
- 允许玩家为当前浏览器选择本地音频作为对战 BGM，并在刷新后恢复。
- 为两类资源提供替换、清除、恢复失败回退和明确错误提示。
- 保证本地资源不会进入服务端请求、SignalR 消息或共享房间状态。
- 保持实现简单，优先复用原生浏览器能力。

**Non-Goals:**
- 不支持账号级同步、跨设备同步、云端备份。
- 不支持将本地桌布或 BGM 分享给对手、观战者或其他客户端。
- 不实现高级音频功能，如播放列表、淡入淡出、均衡器、混音和音量曲线编辑。
- 不实现图片裁剪、滤镜、旋转或素材市场。
- 不修改服务端 API、数据库结构或对战规则。

## Decisions

### 1) Use a dedicated IndexedDB module for battle customization assets

不复用 `src/storageFallback.js`。该模块只适合字符串键值，不适合可靠保存 `Blob`。

新增一个轻量前端模块，例如 `src/battleCustomizationStorage.js`，职责仅包括：
- 打开/升级 IndexedDB 数据库
- 保存和读取桌布/BGM 资源记录
- 删除单项资源
- 读取和写入轻量偏好设置

建议的 IndexedDB 结构：
- database: `svb-battle-customization`
- object store: `assets`
  - key: `background` | `bgm`
  - value:
    - `key`
    - `blob`
    - `fileName`
    - `mimeType`
    - `size`
    - `updatedAt`
- object store: `preferences`
  - key examples:
    - `bgmEnabled`
    - `autoplayRetryPending`

这样可以把大对象存储与轻量配置分开，保持扩展性，但仍然比引入第三方存储库更简单。

### 2) Validate assets with explicit initial limits and MIME whitelists

为了避免浏览器内存压力和不可预测的解码失败，本次变更定义初始资源约束：
- 桌布支持：`image/png`、`image/jpeg`、`image/webp`
- BGM 支持：`audio/mpeg`、`audio/ogg`、`audio/wav`、`audio/mp4`
- 桌布大小上限：`10 MB`
- BGM 大小上限：`20 MB`

选择文件时先做前端校验，再执行保存。校验失败时：
- 不覆盖现有已保存资源
- 向用户显示非阻塞错误信息

备选方案：
- 仅检查 `file.type.startsWith('image/')` / `audio/`。  
  放弃原因：表面简单，但行为边界不稳定，后续测试和用户反馈会更混乱。

### 3) Store original blobs, not base64 or object URLs

保存原始 `Blob` 到 IndexedDB，而不是：
- 存 base64/data URL 到 `localStorage`
- 存运行时 `blob:` URL

原因：
- `blob:` URL 只在当前文档生命周期有效，刷新后失效。
- base64 膨胀明显，不适合音频。
- 原始 `Blob` 恢复后可以重新生成对象 URL，成本最低。

运行时恢复流程：
1. 从 IndexedDB 读取记录。
2. 用 `URL.createObjectURL(blob)` 创建当前会话 URL。
3. 应用到背景图层或 `audio.src`。
4. 当资源被替换、清除或页面清理时调用 `URL.revokeObjectURL(...)`。

### 4) Render the custom battle background via a dedicated game-page layer

不直接改写大量现有战场 DOM 结构，新增一个专用背景层或 CSS 变量。

推荐方案：
- 在 `#game-page` 或 `#game-container` 内新增一个不可交互的背景层元素，例如 `#battle-custom-background`
- 使用 CSS 控制：
  - 绝对定位
  - 低层级
  - `background-image`
  - `background-size: cover`
  - `background-position: center`
  - 柔化遮罩，避免影响卡牌可读性

这样可以：
- 将 custom background 限定在游戏页
- 避免影响菜单页和其他路由
- 让回退到默认背景时只需要移除一层或清空 CSS 变量

### 5) Use a singleton HTMLAudioElement for local BGM playback

本次不引入 Web Audio API 或第三方音频库。新增一个前端单例控制器，例如 `src/battleCustomizationAudio.js`，使用 `HTMLAudioElement` 完成：
- 加载本地 BGM 对象 URL
- `loop = true`
- 在进入/离开游戏页时尝试播放或暂停
- 清除资源时停止播放并释放 URL

原因：
- 需求只是单轨循环 BGM
- `HTMLAudioElement` 已足够满足播放、暂停、重试和错误处理
- 比 Web Audio API 更简单、更易测试

### 6) Handle autoplay restrictions by degrading gracefully

浏览器可能阻止未经过用户交互的自动播放。处理策略：
- 进入游戏页时，如果存在已保存 BGM 且 BGM 功能启用，则尝试 `audio.play()`
- 如果被拒绝：
  - 保留已恢复的 BGM 选择
  - 标记为“待用户交互后重试”
  - 不抛出未捕获异常
  - 在设置面板或播放控件里提示“点击后开始播放”
- 在用户下一次点击游戏页相关控件时执行一次重试

这样可以满足浏览器限制，同时不把一个正常的浏览器策略误报为资源损坏。

### 7) Keep battle customization state strictly client-only

桌布和 BGM 仅属于本地 UI 层，不允许：
- 拼入 API 请求体
- 拼入 SignalR 握手或事件
- 写入 `gameState`
- 通过房间状态同步给其他客户端

该能力应当被视为与窗口布局、个人音量偏好类似的本地环境设置，而不是对战数据。

### 8) On restore failure, remove the broken record and fall back immediately

若出现以下情况：
- IndexedDB 读取失败
- `Blob` 不存在或无效
- `audio` / `image` 加载报错

系统应：
1. 记录 warning 级别日志
2. 删除对应损坏条目
3. 回退到默认背景或默认静音/默认 BGM 状态
4. 向用户显示一次非阻塞提示

这样可以避免每次进游戏页都重复尝试损坏资源。

## Risks / Trade-offs

- [风险] IndexedDB 容量和浏览器策略不可完全控。  
  → Mitigation：限制文件大小，保存后立即做读回校验，失败时不替换旧资源。

- [风险] 自动播放策略会让“已恢复 BGM”看起来像失效。  
  → Mitigation：明确区分“资源读取失败”和“播放被浏览器阻止”，给出不同提示。

- [风险] 自定义桌布可能降低战场元素可读性。  
  → Mitigation：背景层加遮罩和透明度控制，默认使用 cover + center，不开放复杂视觉参数。

- [风险] 如果把太多逻辑堆进 `main.js` 或 `ui.js`，会继续放大现有大文件问题。  
  → Mitigation：将存储、音频、设置面板逻辑拆成独立前端模块，再由主入口组装。

## Migration Plan

1. 新增 battle customization 的前端存储模块和测试。
2. 新增游戏页背景层与设置入口。
3. 接入桌布选择、保存、恢复、替换和清除流程。
4. 接入 BGM 选择、保存、恢复、播放、暂停和清除流程。
5. 增加失败回退、自动播放降级和用户提示。
6. 验证刷新恢复、资源替换、清除、多人对战不共享和存储异常场景。

## Open Questions

- BGM 的默认行为是“有已保存资源就自动尝试播放”，还是首次保存后默认关闭、让玩家手动开启。
- 游戏页设置入口放在页头按钮、浮动按钮，还是复用现有某个设置区；本次设计倾向于页头或浮动按钮的最小入口。
- 是否需要在后续迭代中加入“导出/导入本地自定义配置”，当前设计明确不包含。
