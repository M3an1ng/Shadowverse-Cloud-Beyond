# Change: Battle Log 中文公告与高亮展示

## Why
当前 Battle Log 以指令格式输出，玩家难以快速理解关键信息。需要将公告文本改为中文、用户友好的叙述，并在前端对关键变量进行强调展示。

## What Changes
- 事件公告文本将提供中文可读版本（用于 Battle Log 展示）。
- Battle Log 渲染支持对变量（如来源/目标/数值）进行样式与字体强调。
- 调整日志展示相关规范，明确 Battle Log 展示与调试指令回显的差异。

## Impact
- 受影响的 specs: event-log-interface, event-debug-console
- 受影响的代码: Server/Domain/Events/* GetAnnouncement, src/ui.js, src/styles/game.css, 可能的重放/解析逻辑
