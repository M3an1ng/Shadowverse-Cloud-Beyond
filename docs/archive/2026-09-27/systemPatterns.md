# 02-系统模式与架构 (System Patterns & Architecture) ✅ COMPLETED

> **状态：** ✅ COMPLETED | **版本：** V2.0 | **最后更新：** 2024-12-19
>
> **说明：** 基于当前实现的完整系统架构和设计模式文档

## 1. 总体架构 (Overall Architecture) ✅ COMPLETED

### 1.1 当前实现架构
*   **前后端分离架构:** 已完全实现并投入使用
*   **前端:** 原生 JavaScript (ES2022+) + Vite 构建工具
*   **后端:** ASP.NET Core 8.0 + SignalR 实时通信
*   **数据层:** Entity Framework Core + SQLite (开发) / PostgreSQL (生产)
*   **实时通信:** SignalR Hub 双向事件广播 ✅ 已实现

### 1.2 领域驱动设计 (DDD) 实现
*   **领域层 (Domain):** 实体、值对象、领域服务
*   **应用层 (Application):** 应用服务、DTO、用例协调
*   **基础设施层 (Infrastructure):** 数据访问、外部服务集成
*   **表示层 (Presentation):** API 控制器、SignalR Hub

### 1.3 事件驱动架构 (EDA) 实现
*   **事件源:** 游戏动作触发事件生成
*   **事件处理:** EventProcessor 统一处理事件队列
*   **事件广播:** SignalR 向所有客户端广播状态变化
*   **事件持久化:** 游戏状态通过事件重建和同步

## 2. 已实现设计模式 (Implemented Design Patterns) ✅ COMPLETED

### 2.1 核心架构模式
*   **领域驱动设计 (DDD):** ✅ 完整实现
    *   实体 (Entities): CardDefinition, CardInstance, GameState, PlayerState
    *   值对象 (Value Objects): 游戏事件 DTO, 动作 DTO
    *   聚合根 (Aggregate Roots): GameState 聚合
    *   领域服务 (Domain Services): GameService, AbilityManager

*   **事件驱动架构 (EDA):** ✅ 完整实现
    *   事件接口: IGameEvent 统一事件契约
    *   事件处理器: EventProcessor 事件队列管理
    *   事件广播: SignalR Hub 实时事件分发
    *   事件上下文: GameContext, GameActionContext

### 2.2 数据访问模式
*   **仓储模式 (Repository Pattern):** ✅ 完整实现
    *   接口定义: ICardRepository 抽象数据访问
    *   具体实现: EFCardRepository Entity Framework 实现
    *   依赖注入: 通过 DI 容器管理生命周期

*   **工作单元模式 (Unit of Work):** ✅ 通过 EF Core DbContext 实现
    *   事务管理: 自动事务边界管理
    *   变更追踪: EF Core 自动变更检测
    *   批量提交: SaveChanges 批量数据库操作

### 2.3 行为模式
*   **命令模式 (Command Pattern):** ✅ 完整实现
    *   动作 DTO: PlayCardActionDto, AttackActionDto, EvolveActionDto
    *   命令处理: GameService 统一处理游戏命令
    *   命令验证: 前置条件验证和业务规则检查

*   **策略模式 (Strategy Pattern):** ✅ 在能力系统中实现
    *   能力策略: 不同类型能力的执行策略
    *   AI 策略: 不同难度的 AI 决策策略
    *   目标选择策略: 不同的目标选择算法

### 2.4 创建模式
*   **工厂模式 (Factory Pattern):** ✅ 在能力系统中实现
    *   AbilityManager: 能力实例创建工厂
    *   CardInstance 创建: 从 CardDefinition 创建实例
    *   事件创建: 根据游戏动作创建对应事件

*   **建造者模式 (Builder Pattern):** ✅ 在查询系统中实现
    *   CardQueryConfig: 复杂查询条件构建
    *   AbilityConfig: 复杂能力配置构建

## 3. 已实现核心模块 (Implemented Core Modules) ✅ COMPLETED

### 3.1 后端核心模块
*   **GameService:** ✅ 游戏逻辑核心服务
    *   游戏状态管理和验证
    *   动作处理 (出牌、攻击、进化)
    *   事件生成和广播
    *   AI 回合自动化

*   **AbilityManager:** ✅ 卡片能力管理系统
    *   能力实例创建和注册
    *   触发器事件监听
    *   能力执行协调
    *   DSL 解析集成

*   **EventProcessor:** ✅ 事件处理引擎
    *   事件队列管理
    *   事件链处理
    *   状态同步协调

*   **GameHub (SignalR):** ✅ 实时通信中心
    *   客户端连接管理
    *   动作请求路由
    *   事件广播分发
    *   错误处理和日志

### 3.2 数据访问模块
*   **ApplicationDbContext:** ✅ EF Core 数据上下文
    *   CardDefinition 实体映射
    *   数据库迁移管理
    *   连接字符串配置

*   **EFCardRepository:** ✅ 卡片数据仓储
    *   卡片定义查询
    *   异步数据访问
    *   性能优化 (AsNoTracking)

*   **CardQueryExecutor:** ✅ 卡片查询执行器
    *   复杂查询条件处理
    *   动态查询构建
    *   结果过滤和排序

### 3.3 前端核心模块
*   **gameManager.js:** ✅ 游戏状态管理器
    *   本地游戏状态维护
    *   SignalR 事件处理
    *   动作发送协调
    *   AI 逻辑集成

*   **ui.js:** ✅ 用户界面管理器
    *   游戏界面渲染
    *   用户交互处理
    *   动画效果管理
    *   状态显示更新

*   **cardManager.js:** ✅ 卡片数据管理器
    *   卡片定义加载
    *   卡片实例创建
    *   卡片状态管理

*   **ai.js:** ✅ AI 决策引擎
    *   AI 策略实现
    *   决策算法执行
    *   自动化回合处理

### 3.4 能力系统模块
*   **DSL Parser:** ✅ DSL 解析器
    *   语法分析和 AST 构建
    *   能力配置解析
    *   错误处理和验证

*   **Trigger System:** ✅ 触发器系统
    *   事件监听注册
    *   触发条件检查
    *   触发时机管理

*   **Target System:** ✅ 目标选择系统
    *   目标查询执行
    *   选择策略应用
    *   目标验证检查

*   **Effect System:** ✅ 效果执行系统
    *   效果类型处理
    *   参数解析应用
    *   结果事件生成

## 4. 数据流模式 (Data Flow Patterns) ✅ COMPLETED

### 4.1 前端到后端数据流
```
用户操作 → UI 事件 → gameManager 处理 → SignalR 发送 → GameHub 接收 → GameService 处理
```

### 4.2 后端到前端数据流
```
GameService 处理 → 事件生成 → EventProcessor 队列 → SignalR 广播 → 前端事件处理 → UI 更新
```

### 4.3 能力系统数据流
```
游戏动作 → 触发器检查 → 条件验证 → 目标选择 → 效果执行 → 事件生成 → 状态更新
```

### 4.4 AI 决策数据流
```
AI 回合开始 → 状态分析 → 策略评估 → 动作选择 → 动作执行 → 结果评估
```

## 5. 当前实现状态总结 (Implementation Status Summary) ✅ COMPLETED

### 5.1 完全实现的系统 ✅
*   **基础游戏循环:** 回合管理、抽卡、出牌、战斗
*   **进化机制:** EP 管理、回合限制、属性提升
*   **SignalR 通信:** 双向实时事件广播
*   **卡片能力系统:** 完整的 DSL + AST 实现
*   **AI 对手:** 基础决策逻辑和自动化

### 5.2 部分实现的系统 🚧
*   **测试覆盖:** 基础测试框架，需要扩展
*   **错误处理:** 基础异常处理，需要统一化
*   **性能优化:** 基础优化，需要进一步调优

### 5.3 计划中的系统 📋
*   **用户管理:** 注册、登录、个人资料
*   **卡组管理:** 自定义卡组构建
*   **多人对战:** 玩家匹配和对战

## 6. 相关文档 (Related Documentation) 📚

*   **[01-技术背景](../01-技术背景.md)** - 技术栈和开发环境
*   **[04-进化机制完整指南](../04-进化机制完整指南.md)** - 进化系统实现
*   **[技术文档/M7_核心动作处理与事件广播设计](../技术文档/M7_核心动作处理与事件广播设计_V1.0.md)** - 事件架构
*   **[技术文档/M8_领域模型重构计划](../技术文档/M8_领域模型重构计划_V1.0.md)** - 领域模型
*   **[DSL-Grammar-Specification](../docs/DSL-Grammar-Specification.md)** - DSL 语法规范

---

**文档维护者:** Augment Agent
**创建日期:** 2024-05-22
**最后更新:** 2024-12-19
**版本:** V2.0