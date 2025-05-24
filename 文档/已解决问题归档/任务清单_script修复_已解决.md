# script.js 修复任务清单

这是一个逐步修复 `script.js` 文件中已知结构性问题的任务列表。

## 已完成的任务

- [x] **步骤 1：** 整理 `endTurn` 函数结构
  - [x] 将 `playCard` 函数定义从 `endTurn` 内部移出。
  - [x] 将 `playCard` 函数定义插入到顶层作用域。
  - [x] 将按钮禁用逻辑 (`endTurnButton.disabled = true;`) 移入 `endTurn` 的 `if (!currentPlayer.isAI)` 块。
  - [x] 将玩家切换 (`gameState.currentPlayerId = ...`) 和 `setTimeout(startTurn, ...)` 逻辑移入 `endTurn` 函数末尾。
  - [x] 确保 `endTurn` 函数有正确的单个结束括号。
- [x] **步骤 2：** 修复 `renderGame` 函数内容
  - [x] 将 Player 1 战场为空的检查逻辑 (`if (player1.field.length === 0)`) 移入 `renderGame` 函数内部，放在 `player1.field.forEach` 循环之后。
- [x] **步骤 3：** 修复语法错误
  - [x] 移除第 445 行附近由 `apply_diff` 操作遗留的多余右花括号 `}`。
- [x] **步骤 4：** 整理 `renderGame` 函数结构 _(已通过读取文件确认完成)_
  - [x] 移除第 627 行附近的错误结束括号。
  - [x] 移除第 850 行附近的错误结束括号。
  - [x] 在第 674 行（Player 1 空场检查之后）添加正确的 `renderGame` 结束括号。
  - [x] **验证：** `renderGame` 函数体范围现在看起来正确。
  - [x] **补充修复 (关联步骤 3 & 4):**
    - [x] 修正了 `renderGame` 内部 `player1.field.forEach` 循环中嵌套错误的 `if/else` 结构 (解决卡牌点击逻辑和后续解析错误)。
    - [x] 清理了 `renderGame` 函数结束后的大量冗余/历史注释块 (旧 686-708 行)。
    - [x] 通过在文件末尾添加 `}` 解决了最终的括号不匹配错误。
  - [x] **结论:** 所有已知的 TS/语法解析错误已解决。

## 待办任务

- [x] **步骤 5：验证攻击逻辑唯一性**
  - [x] **确认：** 已检查文件，确认只存在一组攻击相关的变量和函数定义 (`selectedAttackerUid`, `initiateAttack`, `selectAttackTarget`, `executeAttack`)，位于 `renderGame` 之后。
  - [x] **清理（如有必要）：** 无需清理。
- [ ] **--> 下一步建议：步骤 6：清理其他代码和注释**
  - [ ] 查找并移除可能残留的 `render` 文本碎片 (之前在旧 822 行附近)。
  - [ ] 查找并移除可能残留的无效占位注释块 (之前在旧 840-849 行附近)。
  - [ ] 移除 `loadCardData` 函数中的临时注释 `// 使用临时名称以避免冲突` (仍在约 109 行)。
- [ ] **步骤 7：** 代码审查与细化
  - [ ] 检查 `renderGame` 中 Player 1 战场卡牌的 `onclick` 逻辑（约 651-667 行附近）是否完全正确。
  - [ ] 检查是否存在其他由修复过程引入的潜在问题。

_(注意：行号会随着每次修改而变化，需要根据实际情况调整。在执行每一步之前，务必重新读取文件确认行号！)_
