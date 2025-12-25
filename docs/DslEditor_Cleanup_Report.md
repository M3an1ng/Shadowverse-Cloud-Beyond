# DslEditor.js 代码清理分析报告

本文档记录了对 `src/components/DslEditor.js` 文件进行代码清理的分析过程和结果。目标是识别并移除所有废弃、冗余或未使用的代码，为后续的重构工作打下基础。

## 1. 已识别的可移除代码

### 1.1 已废弃的属性

| 行号 | 属性名 | 状态 | 原因 |
|---|---|---|---|
| 43 | `editingContext` | **可移除** | 代码中明确注释为 DEPRECATED，已被 `editingPath` 替代。 |

### 1.2 已迁移的类/方法

| 起始行 | 名称 | 状态 | 迁移目标 |
|---|---|---|---|
| 22 | `AST` 类定义 | **可移除** | `core.js` |
| 24 | `ValueNode` 类定义 | **可移除** | `core.js` |
| 29 | `OperatorNode` 类定义 | **可移除** | `core.js` |
| 674 | `renderMainExpression` 方法 | **可移除** | `ui-main.js` |
| 4152 | `inferOperatorReturnType` 方法 | **可移除** | `core.js` |

---

### 1.3 潜在的冗余/未使用方法

| 起始行 | 名称 | 状态 | 原因与分析 |
|---|---|---|---|
| 630 | `parseExpression` | **可移除** | 这是一个非常简单的旧解析器，仅能处理 `A.B` 形式的路径，与组件当前基于AST的复杂表达式构建逻辑完全不符。代码的核心是通过UI交互逐步构建AST，而不是解析字符串。因此该方法是冗余的。 |
| 1760 | `renderNodeTreeRecursive` | **可移除** | 经过搜索确认，此方法在 `DslEditor.js` 中没有任何调用。它是旧的渲染逻辑的残留，已被 `renderASTNodeRecursive` (line 908) 替代。 |
| 4221 | `updateExpression` | **可移除** | 旧的表达式更新逻辑，依赖于已废弃的 `this.nodeTree` 属性。所有新的更新都应通过 `updateExpressionFromAST` (line 4094) 处理。 |
| 4260 | `buildOperatorExpression` | **可移除** | `updateExpression` 的辅助函数，属于旧逻辑。 |
| 4281 | `buildPropertyWithListQueriesExpression` | **可移除** | `updateExpression` 的辅助函数，属于旧逻辑。 |

---

*此报告将随着分析的深入持续更新。*
### Phase 2: Unused Method Analysis (Post-Cleanup)

After the initial cleanup of deprecated and legacy systems, a second analysis was performed to identify methods that are no longer called anywhere in the codebase.

- **`renderPropertyWithListQueries`**:
  - **Reasoning**: A search for this method's usage reveals it is only ever defined. It was part of the legacy `nodeTree` rendering logic and is not called by the current AST-based rendering system.
  - **Action**: Remove the method.
- **`renderOperatorNode`**, **`renderFunctionNode`**, **`renderOrderByInput`**, **`renderTakeInput`**, **`renderOrderByInputForOperation`**, **`renderTakeInputForOperation`**:
  - **Reasoning**: A project-wide search confirms that these methods are only defined and never called. They are all remnants of the old `nodeTree` rendering system, which has been replaced by the AST-based renderer (`renderOperatorNodeAST`, etc.).
  - **Action**: Remove all of these methods.
- **`isFunctionCompatible`** and **`isListFunctionCompatible`**:
  - **Reasoning**: A project-wide search shows these methods are defined but never used. All call sites have been updated to use the more precise `isFunctionCompatibleWithType` method. These are redundant legacy code.
  - **Action**: Remove both methods.
- **`inferReturnType`**:
  - **Reasoning**: This method is labeled as a "backward compatibility method" and operates on the legacy `this.nodeTree` property. A project-wide search confirms it is never called. The new AST-based logic uses `calculateASTReturnType` instead.
  - **Action**: Remove the method.
| `setExpression(expression)` | L3943-L3947 | Unused public method. Its functionality is handled by the `render` method's initial `expression` parameter. | Removed |
| `parseExpression(expression)` | L618-L618 | Unused legacy method. Part of the old `nodeTree` parsing system. | **可移除** |