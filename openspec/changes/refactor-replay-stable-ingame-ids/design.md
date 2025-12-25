# Design: Minimal Stable Identifier Set For Offline Replay

## 1. 判断准则
本变更只依据两个准则决定“哪些 ID 必须去 Guid”：
1) **稳定回放必要性**：若某个标识会出现在 Output RawEvent 并被后续事实事件引用，则它必须跨进程稳定且可重建。
2) **调试指令高频访问**：若测试者需要经常手工输入该标识（调试控制台/日志定位），则它必须可读、规律、可手输、可识别类别。

## 1.5 InGameId 格式与解析（规范化与容错）
### 1.5.1 Canonical 格式（Output RawEvent MUST 使用）
- `PlayerId`: `P-1`, `P-2`, ...（十进制正整数，不允许前导 0）
- `CardInstanceId`: `C-000001`（6 位十进制，左侧 0 填充）
- `BuffInstanceId`: `B-000001`（6 位十进制，左侧 0 填充）
- 可选：`EventId`: `EV-000001`（6 位十进制，左侧 0 填充）

### 1.5.2 解析容错（Input/Debug 入口 MAY 接受）
- 允许忽略首尾空白
- 允许前缀大小写不敏感（`p-1` 解析为 `P-1`）
- 对 `C-`/`B-`/`EV-`：允许非填充数字（例如 `C-12`）并规范化为 6 位（`C-000012`）
- 对不符合上述格式的字符串必须拒绝，并给出“期望格式”诊断

## 2. Replay Authority Id Set（最小集合）
### 2.1 必须稳定且高频手输
- `PlayerId`（局内座次）：推荐 `P-1` / `P-2`
  - 用于：RawEvent.sourceId（当 source 为玩家/主战者语义）、回放恢复玩家上下文
- `CardInstanceId`：推荐 `C-000001` 递增
  - 用于：RawEvent.targetIds、事件输出中“创建/抽到/移动的卡牌实例”引用
- `BuffInstanceId`：推荐 `B-000001` 递增
  - 用于：BuffApplied/BuffRemoved 之间的引用闭环（若存在后续引用）

### 2.2 必须稳定但不一定常手输（可选）
- `EventId`：推荐 `EV-000001`（顺序号）
  - 不是必须；仅在需要显式引用某个历史事件（诊断/外部索引）时引入
- `AbilityInstanceKey`（稳定键，不是 RuntimeId）
  - 仅当回放事实需要引用“来源能力”且影响可重建语义时才引入

### 2.3 明确不需要稳定（runtime-only）
- `Ability.RuntimeId`、`Effect.Id`、`PendingChoiceId`
  - 只用于运行期索引/在线交互恢复，必须不进入 Output RawEvent

## 3. UserId vs PlayerId（身份分层）
- `UserId`：跨房间/跨会话稳定（账号/设备/登录态），用于联机与权限；不参与回放事实重建。
- `PlayerId`：每局内稳定（座次/阵营），用于事件 Source/Target、回放事实、DSL 访问。
- 映射：同一局中 `UserId -> PlayerId` 一对一；房间解散/重新开局会重新分配 PlayerId。

## 4. RawEvent schema 迁移策略
- **新写入**：Output RawEvent 仅允许 InGameId（字符串）作为 `sourceId/targetIds` 与所有实例引用字段；禁止写入 Guid。
- **兼容读取**：对旧 Journal（Guid）提供 best-effort 读取窗口：
  - 旧数据读入后在 rehydrator 层映射为 InGameId（若无法映射则明确拒绝并输出诊断）
  - 兼容窗口结束后移除旧读路径
