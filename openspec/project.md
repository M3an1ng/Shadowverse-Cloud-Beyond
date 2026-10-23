# 项目上下文

> 核对日期：2026-09-27。本文件只为规范工作流提供入口，当前实现细节统一维护在 docs。

## 项目与技术

Shadowverse-Cloud-Beyond 是网页卡牌项目，包含实时对战、牌组、卡牌工坊和能力 DSL。前端为原生 JavaScript/HTML/CSS、Vite、SignalR 客户端；后端为 .NET 8 ASP.NET Core、SignalR、EF Core SQLite。测试使用 Jest/jsdom 与 xUnit。

- [文档导航](../docs/README.md)
- [功能现状与规划边界](../docs/01-项目现状.md)
- [开发与验证](../docs/02-开发与验证.md)
- [架构与代码入口](../docs/03-架构与代码导航.md)
- [文档维护](../docs/08-文档维护.md)
- [OpenSpec 待核实清单](../docs/OpenSpec待核实清单.md)

## 规范边界

- 主规范位于 [specs](specs/)，变更位于 [changes](changes/)，历史归档位于 [changes/archive](changes/archive/)。
- 提案是需求和设计记录，任务勾选不是代码或部署证据；先核对相关实现与测试再声明完成。
- 旧变更可能相互覆盖，不按最后修改日期批量覆盖主规范。
- 当前代码使用 SQLite；本文件不将 PostgreSQL 或旧分层设计列为已实现能力。
- 账号登录不是旧说明里的 JWT / Refresh Token 流程；房间 ID 与单局 ID 分开；回放日志写入默认关闭。
- 简体中文反馈，改动保持最小闭环；操作确认与 Git 限制以 [AGENTS.md](../AGENTS.md) 为准，不创建额外分支或提交。

本文件原版本保存在 [历史归档](../docs/archive/2026-09-27/openspec/project.md)。
