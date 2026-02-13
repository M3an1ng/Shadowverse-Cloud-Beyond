## Context
当前项目已具备用户注册/登录与角色字段，但权限控制仍以“客户端会话 + 参数透传”为主，核心问题包括：
- 服务端缺少统一认证入口：大量接口通过 `userId/actorUserId` 判定调用者，容易被伪造请求绕过。
- SignalR 与 HTTP 的身份边界不一致：连接身份、Hub 方法调用、REST 请求的授权逻辑未统一。
- 授权规则分散：管理员能力、资源归属（本人访问）与开发工具权限没有统一策略表达。
- 审计不足：无法稳定追踪“谁在何时以何种策略访问了哪些资源并被允许/拒绝”。

## Goals / Non-Goals
- Goals:
  - 建立统一身份认证机制，覆盖 HTTP API 与 SignalR。
  - 建立可组合的 RBAC + Policy 授权体系，默认拒绝未声明权限。
  - 明确资源级授权（Owner/Admin）与调试能力授权边界。
  - 提供完整审计与告警信号，支持合规与追溯。
  - 支持兼容迁移与灰度回滚，避免一次性切换风险。
- Non-Goals:
  - 本提案不引入第三方 SSO/OAuth Provider（仅限站内账户体系）。
  - 本提案不覆盖业务规则层（卡牌 DSL/战斗规则）语义改造。
  - 本提案不要求首期实现细粒度 ABAC 引擎（先以 RBAC + Policy 为主）。

## Decisions
- Decision: 使用短时 Access Token + 可轮换 Refresh Token 作为统一身份凭证
  - Reason: 兼顾安全性、跨端一致性与性能，便于前后端统一鉴权。
  - Alternatives considered:
    - 仅服务端 Session：多端扩展与 API 调用场景下可扩展性较差。
    - 长期有效单令牌：泄漏风险高，吊销与轮换能力不足。

- Decision: 授权采用“策略优先 + 默认拒绝”
  - Reason: 避免遗漏敏感接口导致隐式放行，确保新增接口需显式声明策略。
  - Alternatives considered:
    - 黑名单式限制：维护成本高且容易漏掉新接口。

- Decision: 资源级授权统一走 `IAuthorizationService` + 资源处理器
  - Reason: 将“本人可访问/管理员可覆盖”的规则从控制器散落逻辑提升为可复用策略。
  - Alternatives considered:
    - 控制器内手写判断：重复且难以审计一致性。

- Decision: 前端权限只做展示投影，服务端保持最终裁决
  - Reason: 防止前端本地状态被篡改后影响真实安全边界。
  - Alternatives considered:
    - 前端直接控制所有权限：无法抵抗客户端伪造请求。

- Decision: 审计事件与授权决策结构化记录
  - Reason: 支持问题复盘、安全告警、权限变更追溯与运营排查。
  - Alternatives considered:
    - 仅普通日志：难以按身份/策略/资源聚合检索。

## Architecture Overview
1. Identity Layer
- Auth API 在登录/注册成功后签发 Access Token 与 Refresh Token。
- Access Token 承载最小身份声明（UserId、Role、PermissionVersion、TokenId）。
- Refresh Token 支持轮换与撤销（单设备/全局注销可扩展）。

2. Authorization Layer
- 声明策略（如 `AdminOnly`、`OwnerOrAdmin`、`DevToolsAccess`、`EconomyWrite`）。
- API 控制器与 Hub 方法必须绑定策略；未绑定视为拒绝（受 feature flag 控制逐步开启）。
- 资源级策略处理器根据目标资源 owner 与操作者 claims 决策。

3. Permission Model
- Role 作为默认权限集合载体（Player/Admin）。
- PermissionCode 支持后续细粒度扩展（如 `devtools.log.read`、`users.role.write`）。
- PermissionVersion 用于权限变更后强制会话刷新。

4. Audit & Telemetry
- 记录授权决策事件：`actor`, `resource`, `action`, `policy`, `result`, `reason`, `traceId`。
- 关键拒绝指标：401/403 比例、高危接口拒绝次数、可疑越权尝试次数。

## Migration Plan
### Phase 0 - Inventory & Guardrails
- 盘点所有敏感接口/Hub 方法与当前判权方式。
- 增加告警：检测“匿名 + 传入 userId”访问路径。

### Phase 1 - Dual-Mode (兼容模式)
- 保留旧逻辑，同时接入 Token 验证链路并输出审计（不强制阻断）。
- 将管理员接口先切到强策略模式。

### Phase 2 - Enforced Auth
- 对高风险路径（用户管理、经济写操作、开发工具）开启强制鉴权与策略授权。
- 关闭旧 `actorUserId` 伪鉴权入口（仅保留兼容开关）。

### Phase 3 - Full Enforcement
- 全量 API/Hub 采用统一鉴权与授权。
- 启用默认拒绝策略与 CI 校验（新增接口必须声明策略）。

## Risks / Trade-offs
- 风险: Token 机制引入状态同步复杂度（刷新、撤销、并发登录）
  - Mitigation: 首期限定单活动 Refresh Token 策略并提供明确错误码。
- 风险: 灰度期间双逻辑并存导致行为差异
  - Mitigation: 每次决策都落审计并对比旧/新判定结果。
- 风险: 强鉴权开启后可能影响旧客户端
  - Mitigation: 增加兼容开关、版本门禁和逐步放量。

## Rollback Strategy
- 通过 feature flag 逐项回退：
  - `access_control.enforce_api_policy`
  - `access_control.enforce_hub_policy`
  - `access_control.require_token`
- 回滚时保留审计日志与告警，确保问题可追踪。

## Open Questions
- 是否在首期引入权限组（Role + Custom Grants）还是仅固定角色模型？
- Refresh Token 是否需要按设备维度管理（设备指纹/并发会话上限）？
- 管理员高危操作是否要求二次确认（如二次密码或操作验证码）？
