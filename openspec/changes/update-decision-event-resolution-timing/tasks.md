## 1. Implementation
- [x] 1.1 将 Choose/Random 决策生成延后到事件处理阶段（Commit 或等价位置）
- [x] 1.2 调整决策事件输入来源，确保可被 RewriteEventParamsEffect 改写
- [x] 1.3 调整事务服务：分支能力执行时机后移（依赖事件输出）
- [x] 1.4 更新决策缓存逻辑与重复执行行为
- [x] 1.5 更新/新增测试：拦截改写 selectionCount 生效
