# Change: Add Local Battle Customization

## Why

当前对战桌布和对战背景音乐只能依赖内置资源。若直接要求玩家频繁上传这类个性化资源，会引入不必要的服务端存储、带宽、审核和跨设备同步复杂度，而且并不符合该功能“仅影响当前玩家视觉与听觉体验”的本质。

对于这类纯客户端 cosmetic，更合理的最小方案是允许玩家在浏览器本地选择自定义桌布图片和背景音乐，并持久化到当前浏览器中。这样既能满足长期个性化需求，也避免把本地素材管理错误地做成服务端资产系统。

## What Changes

- 为对战界面新增“本地自定义桌布”和“本地自定义背景音乐”能力，仅对当前浏览器和当前玩家生效。
- 使用浏览器本地持久化存储保存玩家选择的图片和音频资源及其关联设置，刷新页面后仍可恢复。
- 新增对战个性化设置入口，允许玩家选择、替换、预览、清除本地桌布和 BGM。
- 为本地资源增加基础校验与失败回退：
  - 限定支持的图片和音频类型。
  - 对过大的资源给出明确失败提示。
  - 当本地资源读取失败、被清除或不可恢复时，自动回退到默认内置桌布和默认静音/默认 BGM。
- 明确本次变更不包含：
  - 服务端上传或 CDN 托管玩家自定义素材。
  - 账号级同步、跨设备同步。
  - 将本地桌布或 BGM 广播给对手、观战者或房间内其他连接。

## Capabilities

### New Capabilities

- `local-battle-customization`: 为当前浏览器提供本地持久化的对战桌布和背景音乐自定义能力，包括资源选择、恢复、替换、清除、失败回退以及仅本地生效的行为边界。

### Modified Capabilities

- None.

## Impact

- Affected frontend:
  - `src/main.js`
  - `src/ui.js`
  - `src/styles/game.css`
  - `src/styles/main-menu.css`
  - Potentially `src/storageFallback.js` or a new browser-storage module dedicated to battle customization
- Potentially affected markup:
  - `index.html`
- Affected user experience:
  - 新增本地资源选择与管理入口
  - 对战加载时恢复本地桌布和 BGM
  - 资源失效时自动回退默认表现
- Storage impact:
  - 浏览器本地存储容量占用增加，主要来自图片与音频资源
  - 需要定义资源大小上限、错误提示与清除策略
- Explicit non-impact:
  - No backend API changes
  - No database schema changes
  - No multiplayer protocol or game-state changes
