# Change: Add Dev-Tools Log Viewer

## Why
发布后排查问题困难，需要一个前端可视化窗口实时查看日志并辅助过滤、搜索与导出。

## What Changes
- 新增 /dev-tools 路径下的可视化日志窗口（前端页面与交互）。
- 增加实时流式日志供给（后端到前端的流式传输）。
- 提供日志等级过滤、关键字搜索、自动滚动与导出能力（JSON）。
- 限制仅登录用户可访问日志流与视图。

## Impact
- 受影响的 specs: devtools-log-viewer
- 受影响的代码: 前端路由与页面、后端日志流式端点（SignalR 或其他流式机制）、日志导出逻辑
