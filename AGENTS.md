
# Core Principles

- **KISS 原则**: 坚持简洁与可维护性。代码只需满足当前需求，严禁过度工程化（Over-engineering）和不必要的防御性设计。
- **第一性原理**: 遇到问题深入剖析根源，而非通过“打补丁”解决表象。善用现有工具库，不重复造轮子。
- **事实导向**: 如果我的思路或现有代码有误，请直接指出并纠正，不需要客套。

# Engineering and Development Guidelines

## 1) Scope and Requirement Discipline
- **YAGNI（You Aren't Gonna Need It）**: 不实现“可能以后会用到”的功能，严格围绕已确认需求交付。
- **明确验收标准**: 编码前先明确输入、输出、边界条件和失败场景；无法定义验收标准时先澄清再开发。
- **优先最小可行改动**: 先用最小改动解决问题，只有当证据表明不足时再增加复杂度。

## 2) Design and Architecture
- **单一职责**: 模块、类、函数应有清晰单一职责，避免“上帝对象”和职责漂移。
- **组合优于继承**: 在可维护性和可测试性上优先选择组合；仅在明确收益时使用继承。
- **DRY 但避免过早抽象**: 重复达到“规则三次”再抽象，避免为消除一次重复引入复杂框架。
- **稳定接口，控制副作用**: 明确边界与契约，减少隐式状态修改和跨层耦合。

## 3) Code Quality and Readability
- **可读性优先于技巧**: 代码应让后续维护者快速理解；避免炫技式写法。
- **命名要表达业务语义**: 变量、函数、类型命名应准确表达意图，避免模糊缩写。
- **注释解释“为什么”**: 注释用于说明设计动机、约束和权衡，而非重复代码表面含义。
- **拒绝死代码**: 不保留无用分支、过期注释和长期未跟踪的 TODO。

## 4) Testing and Verification
- **行为变更必须有测试**: 新功能、修复和重构都应覆盖关键路径测试。
- **先复现再修复**: Bug 修复优先补充可复现测试，防止回归。
- **测试关注行为而非实现细节**: 降低脆弱 Mock 和过度耦合，提升重构安全性。
- **提交前验证**: 至少运行受影响范围内的测试与静态检查。

## 5) Error Handling and Observability
- **Fail Fast**: 非法输入和不变量破坏应尽早失败并给出明确信号。
- **禁止吞异常**: 必须保留错误上下文（请求标识、关键参数、调用链信息）用于排障。
- **日志结构化且可检索**: 记录关键上下文，不输出敏感信息（密钥、令牌、隐私数据）。
- **用户可理解的错误反馈**: 对外错误信息应可行动、可追踪，不暴露内部实现细节。

## 6) Performance and Resource Discipline
- **先度量后优化**: 以数据为依据定位瓶颈，避免“感觉驱动”的优化。
- **控制 I/O 与查询成本**: 避免 N+1 查询、重复远程调用和无界批量操作。
- **复杂度与规模匹配**: 小规模场景优先简单方案，规模增长时再演进架构。

## 7) Security and Data Safety
- **最小权限原则**: 仅授予完成任务所需权限。
- **输入校验与输出转义**: 对外部输入默认不可信，防止注入与越权。
- **敏感信息隔离**: 密钥、凭证、连接串必须放在安全配置，不写入源码和日志。
- **高风险操作显式确认**: 涉及数据删除、结构变更、生产操作时先确认范围与影响。

## 8) Collaboration and Decision-Making
- **先证据，后结论**: 讨论基于可验证事实（日志、测试、指标、复现步骤）。
- **直接指出问题并给出替代方案**: 发现设计或实现错误时，明确指出并附可执行修正路径。
- **变更可审阅、可回滚**: 优先小步提交，保持改动边界清晰，降低回滚成本。

# High-Risk Operation Restrictions (MANDATORY)

These rules are mandatory for any AI agent operating in this repository.

## Strictly Prohibited Actions
- Never run destructive database commands, including but not limited to:
  - `DROP DATABASE`, `DROP SCHEMA`, `DROP TABLE`, `TRUNCATE`, mass `DELETE` without strict scoped conditions.
- Never perform irreversible or broad destructive filesystem actions, including but not limited to:
  - `rm -rf`, `del /f /q`, recursive deletes, wildcard deletes, force-overwrite of critical directories.
- Never execute destructive Git/history operations unless the user explicitly requests and confirms:
  - `git reset --hard`, `git clean -fd`, force push, history rewrite, branch deletion.
- Never modify production/staging infrastructure, secrets, CI/CD, or live data paths without explicit user authorization.

## Mandatory Safety Requirements
- Default to non-destructive operations; prefer read-only inspection first.
- For any operation with data-loss risk, require explicit user confirmation in the same conversation before execution.
- Always present the exact target and impact scope before high-risk commands.
- Prefer reversible alternatives first (backups, dry-run, narrow scope, preview diffs).
- If scope is ambiguous, stop and ask; do not infer.

## Incident Prevention Baseline
- Assume all persistent data is critical unless the user clearly states otherwise.
- If an unexpected risky state is detected, stop immediately and report before proceeding.
- Log safety rationale in responses when declining risky instructions.
