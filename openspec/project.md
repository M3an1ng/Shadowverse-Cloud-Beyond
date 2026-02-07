# 项目背景

## 目的
- 构建一款受《影之诗》启发的集换式卡牌游戏，具备实时对战、进化机制和可配置的能力 DSL。
- 作为学习与演示平台，展示规范驱动开发、整洁架构以及迭代式玩法扩展。

## 技术栈
- 后端：C# 12、.NET 8 ASP.NET Core、SignalR 实时 Hub、Entity Framework Core 8、Serilog、Swagger/OpenAPI。
- 数据：开发环境 SQLite；生产/扩展计划使用 PostgreSQL。
- 前端：ES2022+ 原生 JavaScript、HTML5/CSS3、ES Modules、Vite 6、@microsoft/signalr 8 客户端。
- 工具/测试：xUnit + ASP.NET Core TestHost、Jest + jsdom、Playwright/@playwright/test、Babel preset-env 转译。
- DSL：自定义卡牌能力 DSL（解析器/AST 支撑触发器、条件、目标、效果执行）。

## 项目约定

### 代码风格
- C#：类型/方法用 PascalCase，本地变量/参数用 camelCase；强调单一职责和描述性命名。
- 前端：ES Modules，函数清晰可读；仅对非直观逻辑添加注释；优先简单的原生 JS，而非额外框架抽象。
- 文档优先：修改须与现有规范/文档保持一致，避免猜测式变更（参考 `.clinerules`）。

### 架构模式
- 领域驱动设计，分层架构（Domain、Application、Infrastructure、Presentation）。
- 事件驱动的游戏流程；SignalR 用于双向事件广播。
- EF Core 实现仓储 + 工作单元；命令/策略模式用于游戏动作与 AI；能力与查询系统采用工厂/建造者模式。

### 测试策略
- 后端：`Server.Tests` 使用 xUnit + ASP.NET Core TestHost，命令 `dotnet test`。
- 前端：Jest + jsdom 做单元测试；提供 Playwright UI 自动化；通过 Vite 开发服务器手动验证。
- 实时行为通过 SignalR 联调与手动对局验证；测试覆盖率需持续提升。

### Git 工作流
- 已配置 Git 仓库；无特殊分支策略记录——采用常规功能分支 + PR 审查。
- 在合并前保持 OpenSpec 提案/设计与 Memory Bank 文档与代码同步。

## 领域背景
- 参考《影之诗》的集换式卡牌游戏，包含进化、卡牌能力、AI 对手与事件驱动的状态更新。
- 路线从网页 Demo 走向更丰富的机制（卡组构建、多玩家），同时保持教学/实验属性。

## 重要约束
- 目标运行时为 .NET 8，前端为 ES2022+；默认 Windows 开发环境。
- 开发用 SQLite，计划支持 PostgreSQL；需保持 SignalR 的实时响应。
- 遵循既有 DSL 结构和 DDD/EDA 模式；触及核心游戏流程时优先补充测试。

## 外部依赖
- 后端包：Microsoft.EntityFrameworkCore.Sqlite、Microsoft.AspNetCore.OpenApi、Swashbuckle.AspNetCore、Serilog.AspNetCore、Serilog.Sinks.File、SignalR。
- 前端/构建包：@microsoft/signalr、Vite、Babel core/preset-env。
- 测试/自动化：xUnit、Jest + jest-environment-jsdom、Playwright/@playwright/test。
- 数据存储：SQLite（开发），计划支持 PostgreSQL。
