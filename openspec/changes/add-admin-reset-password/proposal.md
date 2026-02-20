# Change: Add Admin Password Reset in User Management

## Why
当前控制台用户管理仅支持角色调整，不支持管理员对账号进行密码重置。运维与测试场景需要在不暴露旧密码的前提下，快速恢复用户登录能力。

## What Changes
- 在后端用户管理接口中新增管理员重置目标用户密码的能力。
- 在开发者控制台“当前用户管理”面板新增“重置密码”操作入口。
- 为新行为增加后端自动化测试（成功、权限拒绝、弱密码校验）。

## Impact
- Affected specs: user-management
- Affected code:
  - Server/Controllers/UsersController.cs
  - src/devToolsUserManagement.js
  - src/styles/devtools.css
  - Server.Tests/UserManagementControllerTests.cs
