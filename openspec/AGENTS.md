# OpenSpec 指南

面向使用 OpenSpec 进行规范驱动开发的 AI 编码助手说明。

## TL;DR 速查清单

- 先查已有工作：`openspec spec list --long`、`openspec list`（全文搜索仅用 `rg`）
- 明确范围：新增能力 vs 修改已有能力
- 选择唯一 `change-id`：kebab-case，动词开头（`add-`、`update-`、`remove-`、`refactor-`）
- 脚手架：`proposal.md`、`tasks.md`、`design.md`（需要时），以及每个受影响能力的增量 spec
- 写增量：使用 `## ADDED|MODIFIED|REMOVED|RENAMED Requirements`；每条 Requirement 至少一个 `#### Scenario:`
- 校验：`openspec validate [change-id] --strict` 并修复问题
- 审批：提案获批前不要开始实现

## 三阶段流程

### 阶段 1：创建变更
需要提案的情况：
- 新增功能或特性
- 破坏性变更（API、schema）
- 架构或模式调整  
- 性能优化（影响行为）
- 安全模式更新

触发示例：
- “帮我创建一个变更提案”
- “帮我规划一个变更”
- “帮我写一个提案”
- “我想创建一个规范提案”
- “我想创建一个 spec”

模糊匹配指引：
- 包含：`proposal`、`change`、`spec`
- 以及：`create`、`plan`、`make`、`start`、`help`

无需提案的情况：
- Bug 修复（恢复既定行为）
- 拼写/格式/注释
- 非破坏性依赖更新
- 配置调整
- 针对既有行为的测试

**流程**
1. 阅读 `openspec/project.md`、`openspec list`、`openspec list --specs` 了解当前上下文。
2. 选取唯一、动词开头的 `change-id`，在 `openspec/changes/<id>/` 下搭建 `proposal.md`、`tasks.md`、可选 `design.md`，以及 spec 增量。
3. 用 `## ADDED|MODIFIED|REMOVED Requirements` 编写增量，每条 Requirement 至少一个 `#### Scenario:`.
4. 运行 `openspec validate <id> --strict`，修复问题后再分享提案。

### 阶段 2：实现变更
将步骤作为 TODO 逐一完成。
1. **阅读 proposal.md** - 理解要做什么
2. **阅读 design.md**（如有） - 查看技术决策
3. **阅读 tasks.md** - 获取实现清单
4. **按顺序实现任务** - 依次完成
5. **确认完成度** - `tasks.md` 全部完成后再更新状态
6. **更新清单** - 全部完成后把任务改为 `- [x]`
7. **审批门槛** - 提案审阅通过前不要开始实现

### 阶段 3：归档变更
部署后，单独提交 PR：
- 将 `changes/[name]/` → `changes/archive/YYYY-MM-DD-[name]/`
- 若能力有变更，更新 `specs/`
- 仅工具变更可用 `openspec archive <change-id> --skip-specs --yes`（必须显式传递 change ID）
- 运行 `openspec validate --strict` 确认归档变更通过校验

## 开始任何任务前

**上下文检查清单：**
- [ ] 阅读 `specs/[capability]/spec.md` 中的相关规范
- [ ] 检查 `changes/` 中的未决变更是否冲突
- [ ] 阅读 `openspec/project.md` 获取约定
- [ ] 运行 `openspec list` 查看活跃变更
- [ ] 运行 `openspec list --specs` 查看已有能力

**创建规范前：**
- 总是先检查能力是否已存在
- 更倾向修改现有规范而非创建重复项
- 使用 `openspec show [spec]` 查看当前状态
- 如果需求模糊，脚手架前先问 1–2 个澄清问题

### 搜索指引
- 枚举规范：`openspec spec list --long`（脚本可用 `--json`）
- 枚举变更：`openspec list`（或 `openspec change list --json`，已弃用但可用）
- 查看详情：
  - Spec: `openspec show <spec-id> --type spec`（过滤可用 `--json`）
  - Change: `openspec show <change-id> --json --deltas-only`
- 全文搜索（用 ripgrep）：`rg -n "Requirement:|Scenario:" openspec/specs`

## 快速开始

### CLI 命令

```bash
# 基本命令
openspec list                  # 列出活跃变更
openspec list --specs          # 列出规范
openspec show [item]           # 显示变更或规范
openspec validate [item]       # 校验变更或规范
openspec archive <change-id> [--yes|-y]   # 部署后归档（非交互模式加 --yes）

# 项目管理
openspec init [path]           # 初始化 OpenSpec
openspec update [path]         # 更新指令文件

# 交互模式
openspec show                  # 交互选择
openspec validate              # 批量校验模式

# 调试
openspec show [change] --json --deltas-only
openspec validate [change] --strict
```

### 命令标志

- `--json` - 机器可读输出
- `--type change|spec` - 指定类型
- `--strict` - 全面校验
- `--no-interactive` - 关闭提示
- `--skip-specs` - 归档时跳过规范更新
- `--yes`/`-y` - 跳过确认（非交互归档）

## 目录结构

```
openspec/
├── project.md              # 项目约定
├── specs/                  # 当前真实状态（已构建）
│   └── [capability]/       # 单一聚焦能力
│       ├── spec.md         # 需求与场景
│       └── design.md       # 技术模式
├── changes/                # 提案（计划变更）
│   ├── [change-name]/
│   │   ├── proposal.md     # 为什么，做什么，影响
│   │   ├── tasks.md        # 实现清单
│   │   ├── design.md       # 技术决策（可选，见标准）
│   │   └── specs/          # 增量变更
│   │       └── [capability]/
│   │           └── spec.md # ADDED/MODIFIED/REMOVED
│   └── archive/            # 已完成的变更
```

## 创建变更提案

### 决策树

```
新的需求？
├─ 恢复既定行为的 Bug 修复？ → 直接修
├─ 拼写/格式/注释？ → 直接修  
├─ 新功能/能力？ → 创建提案
├─ 破坏性变更？ → 创建提案
├─ 架构变更？ → 创建提案
└─ 不清楚？ → 创建提案（更安全）
```

### 提案结构

1. **创建目录：** `changes/[change-id]/`（kebab-case，动词开头，唯一）

2. **编写 proposal.md：**
```markdown
# Change: [简述变更]

## Why
[1-2 句描述问题/机会]

## What Changes
- [变更列表]
- [破坏性变更标记 **BREAKING**]

## Impact
- 受影响的 specs: [能力列表]
- 受影响的代码: [关键文件/系统]
```

3. **创建 spec 增量：** `specs/[capability]/spec.md`
```markdown
## ADDED Requirements
### Requirement: New Feature
The system SHALL provide...

#### Scenario: Success case
- **WHEN** user performs action
- **THEN** expected result

## MODIFIED Requirements
### Requirement: Existing Feature
[完整的修改后需求]

## REMOVED Requirements
### Requirement: Old Feature
**Reason**: [为何移除]
**Migration**: [如何处理]
```
若影响多个能力，在 `changes/[change-id]/specs/<capability>/spec.md` 下分别创建增量文件，每个能力一个。

4. **创建 tasks.md：**
```markdown
## 1. Implementation
- [ ] 1.1 Create database schema
- [ ] 1.2 Implement API endpoint
- [ ] 1.3 Add frontend component
- [ ] 1.4 Write tests
```

5. **需要时创建 design.md：**
满足以下任一则创建 `design.md`，否则省略：
- 跨服务/模块或新架构模式
- 新的外部依赖或重大数据模型变更
- 安全、性能或迁移复杂度
- 需要先定技术决策以消除歧义

`design.md` 最小骨架：
```markdown
## Context
[背景、约束、干系人]

## Goals / Non-Goals
- Goals: [...]
- Non-Goals: [...]

## Decisions
- Decision: [决策与原因]
- Alternatives considered: [备选方案 + 理由]

## Risks / Trade-offs
- [风险] → 缓解方案

## Migration Plan
[步骤、回滚]

## Open Questions
- [...]
```

## 规范文件格式

### 关键：Scenario 格式

**正确**（使用 #### 标题）：
```markdown
#### Scenario: User login success
- **WHEN** valid credentials provided
- **THEN** return JWT token
```

**错误**（不要用项目符号或粗体代替）：
```markdown
- **Scenario: User login**  ❌
**Scenario**: User login     ❌
### Scenario: User login      ❌
```

每个 Requirement 必须至少包含一个 Scenario。

### Requirement 表述
- 规范性需求使用 SHALL/MUST（除非刻意非规范性，避免 should/may）

### 增量操作

- `## ADDED Requirements` - 新增能力
- `## MODIFIED Requirements` - 行为变更
- `## REMOVED Requirements` - 废弃特性
- `## RENAMED Requirements` - 仅名称变更

头部匹配使用 `trim(header)`，忽略空白。

#### 何时用 ADDED vs MODIFIED
- ADDED：引入可独立成需求的新能力/子能力。更偏好用于正交变更（如新增“Slash Command Configuration”）而不是改写既有需求语义。
- MODIFIED：改变现有需求的行为、范围或验收标准。总是粘贴完整更新后的内容（标题 + 所有场景）。归档时会用你提供的内容替换整个需求，部分增量会丢失细节。
- RENAMED：仅名称变更；若行为也变，先 RENAMED（名称），再 MODIFIED（内容，使用新名称）。

常见陷阱：用 MODIFIED 添加新关注点但未包含原文本，归档时会丢失细节。如果并未显式修改现有需求，应在 ADDED 下添加新需求。

正确编写 MODIFIED 需求：
1) 在 `openspec/specs/<capability>/spec.md` 找到现有需求。
2) 复制整个需求块（从 `### Requirement: ...` 到全部 Scenario）。
3) 粘贴到 `## MODIFIED Requirements` 下，编辑成新行为。
4) 确保标题文本完全匹配（忽略空白），并至少保留一个 `#### Scenario:`.

RENAMED 示例：
```markdown
## RENAMED Requirements
- FROM: `### Requirement: Login`
- TO: `### Requirement: User Authentication`
```

## 故障排查

### 常见错误

**“Change must have at least one delta”**
- 检查 `changes/[name]/specs/` 是否存在 .md 文件
- 确认文件包含操作前缀（如 ## ADDED Requirements）

**“Requirement must have at least one scenario”**
- 确认场景使用 `#### Scenario:`（4 个 #）
- 不要用项目符号或粗体做场景标题

**场景解析静默失败**
- 必须严格使用 `#### Scenario: Name` 格式
- 调试：`openspec show [change] --json --deltas-only`

### 校验提示

```bash
# 始终用严格模式做全面校验
openspec validate [change] --strict

# 调试增量解析
openspec show [change] --json | jq '.deltas'

# 检查特定需求
openspec show [spec] --json -r 1
```

## 快乐路径脚本

```bash
# 1) 查看当前状态
openspec spec list --long
openspec list
# 可选全文搜索：
# rg -n "Requirement:|Scenario:" openspec/specs
# rg -n "^#|Requirement:" openspec/changes

# 2) 选择 change id 并搭建脚手架
CHANGE=add-two-factor-auth
mkdir -p openspec/changes/$CHANGE/{specs/auth}
printf "## Why\n...\n\n## What Changes\n- ...\n\n## Impact\n- ...\n" > openspec/changes/$CHANGE/proposal.md
printf "## 1. Implementation\n- [ ] 1.1 ...\n" > openspec/changes/$CHANGE/tasks.md

# 3) 添加增量（示例）
cat > openspec/changes/$CHANGE/specs/auth/spec.md << 'EOF'
## ADDED Requirements
### Requirement: Two-Factor Authentication
Users MUST provide a second factor during login.

#### Scenario: OTP required
- **WHEN** valid credentials are provided
- **THEN** an OTP challenge is required
EOF

# 4) 校验
openspec validate $CHANGE --strict
```

## 多能力示例

```
openspec/changes/add-2fa-notify/
├── proposal.md
├── tasks.md
└── specs/
    ├── auth/
    │   └── spec.md   # ADDED: Two-Factor Authentication
    └── notifications/
        └── spec.md   # ADDED: OTP email notification
```

auth/spec.md
```markdown
## ADDED Requirements
### Requirement: Two-Factor Authentication
...
```

notifications/spec.md
```markdown
## ADDED Requirements
### Requirement: OTP Email Notification
...
```

## 最佳实践

### 简单优先
- 默认新增代码 <100 行
- 单文件实现直到证明确实不够
- 无充分理由避免引入新框架
- 选择“无聊但可靠”的模式

### 触发复杂度的条件
仅在以下情况增加复杂度：
- 性能数据表明当前方案太慢
- 明确的规模需求（>1000 用户，>100MB 数据）
- 多个已验证用例需要抽象

### 清晰引用
- 代码位置用 `file.ts:42` 格式
- 规范引用如 `specs/auth/spec.md`
- 关联变更与 PR 要互相关联

### 能力命名
- 用动词-名词：`user-auth`、`payment-capture`
- 每个能力单一目的
- 10 分钟可理解原则
- 如果描述需要“AND”，考虑拆分

### Change ID 命名
- 使用简短描述性的 kebab-case：`add-two-factor-auth`
- 优先动词前缀：`add-`、`update-`、`remove-`、`refactor-`
- 确保唯一；若已占用，加 `-2`、`-3` 等

## 工具选择指南

| 任务 | 工具 | 原因 |
|------|------|-----|
| 按模式找文件 | Glob | 快速模式匹配 |
| 搜索代码内容 | Grep | 优化的正则搜索 |
| 阅读特定文件 | Read | 直接文件访问 |
| 探索未知范围 | Task | 多步调查 |

## 错误恢复

### 变更冲突
1. 运行 `openspec list` 查看活跃变更
2. 检查是否有规范重叠
3. 与变更所有者协调
4. 考虑合并提案

### 校验失败
1. 使用 `--strict` 标志运行
2. 查看 JSON 输出细节
3. 验证规范文件格式
4. 确保 Scenario 格式正确

### 缺少上下文
1. 先读 project.md
2. 检查相关规范
3. 回顾近期归档
4. 提出澄清

## 快速参考

### 阶段标识
- `changes/` - 提案，尚未构建
- `specs/` - 已构建并部署
- `archive/` - 已完成变更

### 文件用途
- `proposal.md` - 为什么、做什么
- `tasks.md` - 实现步骤
- `design.md` - 技术决策
- `spec.md` - 需求与行为

### CLI 要点
```bash
openspec list              # 进行中的内容？
openspec show [item]       # 查看详情
openspec validate --strict # 是否正确？
openspec archive <change-id> [--yes|-y]  # 标记完成（自动化加 --yes）
```

牢记：Specs 是事实；Changes 是提案；保持同步。
