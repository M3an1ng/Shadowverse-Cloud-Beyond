# Change: Add unified access control system

## Why
当前系统的权限校验主要依赖前端会话字段与 `userId/actorUserId` 透传，后端缺少统一的身份鉴权与策略授权边界，存在越权风险、审计困难和跨端行为不一致问题。  
随着“系统设置/管理员用户管理”等能力增长，需要一套可扩展、可审计、可灰度迁移的权限控制体系。

## What Changes
- 引入统一身份层：HTTP API 与 SignalR 统一使用服务端可验证身份凭证（访问令牌 + 刷新机制）。
- 建立 RBAC + Policy 授权模型：角色定义权限等级与权限码，接口/Hub/资源访问按策略进行默认拒绝。
- 引入资源级授权：对用户资产（卡组、经济、日志、调试能力）执行“本人或管理员”规则，禁止仅凭传入 `userId` 访问。
- 引入管理员权限治理：角色变更最小化原则、最后管理员保护、批量操作审计。
- 建立审计与可观测能力：记录授权决策、拒绝原因、操作者身份，支持安全告警与回溯。
- 提供前端权限投影契约：前端仅做可见性提示，服务端作为最终授权裁决。
- 设计兼容迁移方案：支持从当前“裸 userId”模式平滑迁移到强鉴权模式并可回滚。

## Impact
- Affected specs: platform-access-control (new)
- Affected code:
  - `Server/Controllers/*`（鉴权接入与策略化授权）
  - `Server/Hubs/*`（连接鉴权、方法授权、组广播边界）
  - `Server/Program.cs`（认证授权中间件、策略注册）
  - `Server/Domain/Entities/User.cs`（权限模型扩展）
  - `src/userSession.js`、`src/main.js`、`src/devTools*`（令牌与权限投影接入）
  - 数据库迁移与审计日志存储模块
