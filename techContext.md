# 01-技术背景 (Technical Context) ✅ COMPLETED

> **状态：** ✅ COMPLETED | **版本：** V2.0 | **最后更新：** 2024-12-19
>
> **说明：** 基于当前代码库实现状态的完整技术背景文档

## 1. 开发环境 (Development Environment) ✅ COMPLETED

### 1.1 基础环境
*   **操作系统:** Windows 10/11
*   **IDE:** Visual Studio Code (推荐) / Visual Studio 2022
*   **版本控制:** Git (已配置仓库)
*   **AI 辅助工具:** Augment Agent (Claude Sonnet 4 基础)

### 1.2 活跃 MCP 服务器
*   **playwright**: Web UI 测试和自动化
*   **fetch**: 网络资源获取
*   **Context7**: 库文档查询
*   **memory-bank-mcp**: Memory Bank 文件管理
*   **interactive-feedback-mcp**: 用户交互式反馈
*   **toolbox**: Smithery 工具箱
*   **browser-tools-mcp**: 浏览器信息查看（自动化调试）
*   **exa**: 高级网络搜索 (网页, 论文, 公司等)
*   **str-replace-editor**: 代码编辑工具

### 1.3 开发工具链
*   **前端开发:** Vite 开发服务器 (热重载, 代理配置)
*   **后端调试:** Visual Studio 断点调试, Serilog 日志
*   **数据库管理:** SQLite Browser, EF Core 迁移工具
*   **API 测试:** Swagger UI, Postman

## 2. 项目技术栈 (Project Tech Stack) ✅ COMPLETED

### 2.1 后端架构 (.NET 8)
*   **语言:** C# 12
*   **框架:** .NET 8.0 (LTS)
*   **Web 框架:** ASP.NET Core 8.0
*   **架构模式:** 领域驱动设计 (DDD) + 事件驱动架构 (EDA)
*   **ORM:** Entity Framework Core 8.0
*   **数据库:** SQLite (开发环境) / 可扩展至 PostgreSQL
*   **实时通信:** SignalR (已完成实现)
*   **日志框架:** Serilog + 文件输出
*   **API 文档:** Swagger/OpenAPI 3.0

### 2.2 前端技术栈
*   **核心技术:** 原生 JavaScript (ES2022+), HTML5, CSS3
*   **模块系统:** ES Modules
*   **构建工具:** Vite 6.3.5 (开发服务器 + 热重载)
*   **实时通信:** @microsoft/signalr 8.0.7
*   **开发模式:** 前后端分离 + 代理配置

### 2.3 领域模型架构
*   **实体层:** CardDefinition, CardInstance, GameState, PlayerState
*   **能力系统:** 完整的卡片能力管理 (Ability, Trigger, Condition, Target, Effect)
*   **DSL 解析:** 自定义 DSL 语法 + AST 解析器
*   **事件系统:** 游戏事件处理 + 事件广播机制
*   **查询系统:** 卡片查询执行器 + 查询配置

### 2.4 测试框架
*   **单元测试:** xUnit.net (Server.Tests 项目)
*   **集成测试:** ASP.NET Core TestHost
*   **前端测试:** 手动测试 + 浏览器开发者工具

## 3. 领域架构实现 (Domain Architecture) ✅ COMPLETED

### 3.1 领域驱动设计 (DDD) 实现
*   **实体 (Entities):** CardDefinition, CardInstance, GameState, PlayerState
*   **值对象 (Value Objects):** 游戏事件, 动作 DTO, 状态 DTO
*   **聚合根 (Aggregate Roots):** GameState (游戏状态聚合)
*   **仓储模式 (Repository):** ICardRepository, EFCardRepository
*   **领域服务 (Domain Services):** GameService, AbilityManager

### 3.2 卡片能力管理系统 ✅ COMPLETED
*   **能力基础架构:** Ability, AbilityConfig, AbilityContext
*   **触发器系统:** Trigger 配置 + 事件监听机制
*   **条件系统:** Condition 配置 + 条件验证逻辑
*   **目标选择系统:** Target 配置 + 目标查询执行器
*   **效果系统:** Effect 配置 + 效果执行器
*   **DSL 解析器:** 自定义语法 + AST 节点系统

### 3.3 事件驱动架构 (EDA) ✅ COMPLETED
*   **游戏事件:** IGameEvent 接口 + 具体事件实现
*   **事件处理器:** EventProcessor + 事件队列管理
*   **事件广播:** SignalR Hub + 客户端事件分发
*   **事件上下文:** GameContext + GameActionContext

## 4. 构建与部署 (Build & Deployment) ✅ COMPLETED

### 4.1 开发环境构建
*   **后端构建:** `dotnet build` + `dotnet run`
*   **前端开发:** `npm run dev` (Vite 开发服务器)
*   **数据库迁移:** `dotnet ef migrations add` + `dotnet ef database update`
*   **测试执行:** `dotnet test` (单元测试)

### 4.2 生产环境部署
*   **后端部署:** ASP.NET Core 自托管 + Kestrel 服务器
*   **前端部署:** `npm run build` + 静态文件服务
*   **数据库:** SQLite 文件部署 (可扩展至 PostgreSQL)
*   **代理配置:** Vite 代理 (/api, /gamehub) 到后端服务

## 5. 关键依赖与库 (Key Dependencies) ✅ COMPLETED

### 5.1 后端依赖 (.NET 8)
*   **Microsoft.EntityFrameworkCore.Sqlite** (8.0.0): EF Core SQLite 提供程序
*   **Microsoft.AspNetCore.OpenApi** (8.0.0): OpenAPI/Swagger 支持
*   **Swashbuckle.AspNetCore** (6.5.0): Swagger UI 工具集
*   **Serilog.AspNetCore** (9.0.0): 结构化日志框架
*   **Serilog.Sinks.File** (7.0.0): 文件日志输出

### 5.2 前端依赖 (Node.js)
*   **@microsoft/signalr** (8.0.7): SignalR 客户端库
*   **vite** (6.3.5): 现代前端构建工具
*   **原生 JavaScript:** ES2022+ 模块系统

## 6. 当前实现状态 (Current Implementation Status) ✅ COMPLETED

### 6.1 已完成功能 ✅ COMPLETED
*   **基础游戏循环:** 回合管理、抽卡、出牌、战斗系统
*   **进化机制:** 完整的进化系统 (EP 管理、回合限制、属性提升)
*   **SignalR 实时通信:** 双向事件广播和状态同步
*   **卡片能力系统:** 触发器、条件、目标、效果的完整实现
*   **DSL 解析器:** 自定义语法和 AST 节点系统
*   **AI 对手:** 基础 AI 决策逻辑和自动化回合

### 6.2 开发中功能 🚧 IN-PROGRESS
*   **AI 智能化:** 更复杂的决策算法和策略评估
*   **卡片效果扩展:** 更多特殊效果和关键字能力
*   **UI/UX 优化:** 动画效果和视觉反馈改进
*   **测试覆盖:** 单元测试和集成测试完善

### 6.3 计划功能 📋 PLANNED
*   **用户系统:** 注册、登录、个人资料管理
*   **卡组管理:** 自定义卡组构建和管理
*   **多人对战:** 玩家间实时对战匹配
*   **数据库扩展:** 迁移至 PostgreSQL 生产环境

## 7. 技术风险与注意事项 (Technical Risks & Notes) ⚠️ ONGOING

### 7.1 当前技术债务
*   **测试覆盖率:** 需要完善单元测试和集成测试策略
*   **错误处理:** 需要统一的异常处理和错误恢复机制
*   **性能优化:** 大量卡片和复杂能力的性能优化
*   **代码文档:** 需要完善 API 文档和代码注释

### 7.2 架构考虑
*   **可扩展性:** 当前 SQLite 适合开发，生产环境需考虑 PostgreSQL
*   **并发处理:** SignalR 连接管理和并发游戏状态处理
*   **安全性:** 输入验证、权限控制、防作弊机制
*   **监控:** 生产环境监控和日志分析系统

## 8. 相关文档 (Related Documentation) 📚

*   **[02-项目架构总览](../02-项目架构总览.md)** - 系统架构和设计模式
*   **[04-进化机制完整指南](../04-进化机制完整指南.md)** - 进化系统详细实现
*   **[技术文档/M5_SignalR通信设计](../技术文档/M5_SignalR通信设计_V1.0.md)** - 实时通信架构
*   **[技术文档/M8_领域模型重构计划](../技术文档/M8_领域模型重构计划_V1.0.md)** - 领域模型设计
*   **[DSL-Grammar-Specification](../docs/DSL-Grammar-Specification.md)** - DSL 语法规范

---

**文档维护者:** Augment Agent
**创建日期:** 2024-05-22
**最后更新:** 2024-12-19
**版本:** V2.0