# 02. DslEditor 重构实施指南

**版本: 1.0**

**作者: AI 技术总监**

**更新日期: 2025-08-24**

## 1. 任务背景与目标

### 1.1. 背景

当前 `src/components/DslEditor.js` 中的核心推荐逻辑方法 `getAvailableOptions` 功能较为初级，无法根据 DSL 节点的上下文返回类型，精确地提供下一步的可选操作。这导致编辑器无法完全支持设计文档中定义的、强大的链式构建能力。

### 1.2. 目标

本次重构的核心目标是将 `getAvailableOptions` 方法升级为一个完全由元数据驱动、具备精确上下文感知能力的智能推荐引擎。重构完成后，DslEditor 必须能够：

1.  **精确解析节点类型:** 准确识别当前表达式链末端节点的返回数据类型（如 `PlayerState`, `List<CardInstance>`, `Int32` 等）。
2.  **提供精确选项:** 根据节点的返回类型，严格按照 `06-DslEditor重构设计方案.md` 中定义的规则，提供合法的、上下文相关的下一步操作选项。
3.  **支持通用列表查询:** 对任何 `List<T>` 类型的节点，都能提供通用的 `.query()`, `.orderBy()`, `.take()` 操作。
4.  **支持嵌套上下文:** 在执行 `.query( ... )` 操作时，能够为括号内的子编辑器正确设置以上下文 (`self`)。

## 2. 潜在问题与风险分析

1.  **递归类型解析的复杂性:** `getPropertyFromPath` 方法需要递归遍历元数据来解析一个长路径（如 `player.leader.abilities`）的最终类型。如果元数据结构不规范或出现循环引用，可能导致解析失败或无限循环。**对策:** 编码时必须增加对 `null` 和 `undefined` 的健壮性检查，并设置合理的递归深度限制（虽然在此场景下不太可能出现）。

2.  **`this.nodeTree` 状态管理的精确性:** 整个推荐逻辑都依赖于 `this.nodeTree` 对象的准确性。`addNode` 方法在每次添加新节点后，必须正确地更新 `nodeTree` 的 `type`, `fullPath`, 和 `returnType` 字段。任何一个字段的错误都将导致后续推荐链条中断。**对策:** 在 `addNode` 方法中增加日志，并在测试时密切关注 `nodeTree` 状态的变化。

3.  **嵌套上下文的管理:** 在实现 `.query( ... )` 的内部 DSL 编辑器时，如何正确地创建、传递和使用新的上下文（特别是 `self` 关键字）是一个挑战。需要确保子编辑器的元数据查询能够正确地将 `self` 解析为列表的内部元素类型。**对策:** 可能需要在 `DslEditor` 的 `render` 或 `initialize` 方法中增加一个可选的 `customContext` 参数，用于接收父编辑器传递的特殊上下文。

4.  **异步元数据加载:** `MetadataDrivenComponent` 的 `initialize` 方法是异步的。必须确保所有依赖元数据的操作（特别是 `getAvailableOptions`）都在 `this.metadata` 加载完成后才执行。**对策:** 现有代码结构已通过 `await this.initialize()` 保证了这一点，但在后续修改中需保持警惕。

## 3. 详细实施步骤 (任务清单)

这是一份可以交由开发者直接执行的任务清单。

### 阶段一：添加辅助函数 (Helpers)

-   [ ] **任务 1.1: 实现 `isBaseType(typeName)`**
    -   **文件:** `src/components/DslEditor.js`
    -   **位置:** 在类的方法列表末尾。
    -   **实现:**
        ```javascript
        isBaseType(typeName) {
            const baseTypes = ['Int32', 'Boolean', 'String', 'Guid', 'Single', 'Double', 'Int64'];
            return baseTypes.includes(typeName);
        }
        ```
    -   **验证:** 调用 `this.isBaseType('Int32')` 应返回 `true`，`this.isBaseType('CardInstance')` 应返回 `false`。

-   [ ] **任务 1.2: 实现 `isCollectionType(typeName)`**
    -   **文件:** `src/components/DslEditor.js`
    -   **实现:**
        ```javascript
        isCollectionType(typeName) {
            return typeName && (typeName.startsWith('List<') || typeName.startsWith('IEnumerable<'));
        }
        ```
    -   **验证:** `this.isCollectionType('List<CardInstance>')` 应返回 `true`。

-   [ ] **任务 1.3: 实现 `getCollectionInnerType(typeName)`**
    -   **文件:** `src/components/DslEditor.js`
    -   **实现:**
        ```javascript
        getCollectionInnerType(typeName) {
            if (!this.isCollectionType(typeName)) return null;
            const match = typeName.match(/<(.*)>/);
            return match ? match[1] : null;
        }
        ```
    -   **验证:** `this.getCollectionInnerType('List<CardInstance>')` 应返回 `'CardInstance'`。

### 阶段二：改造核心解析与推荐逻辑

-   [ ] **任务 2.1: 重构 `getPropertyFromPath(fullPath)`**
    -   **文件:** `src/components/DslEditor.js`
    -   **目标:** 使其能够递归解析长路径，并返回包含 `{ property, ownerType }` 的详细信息对象。
    -   **实现:** 替换原有方法，参考设计文档中的逻辑，使用循环和 `getTypeMetadata` 逐级解析 `parts`。
    -   **验证:** 调用 `this.getPropertyFromPath('player.leader')` 应返回一个对象，其中 `property` 是 `leader` 的元数据，`ownerType` 是 `PlayerState` 的元数据。

-   [ ] **任务 2.2: 重构 `getAvailableOptions()`**
    -   **文件:** `src/components/DslEditor.js`
    -   **目标:** 将原有的 `if/else if` 结构，替换为设计文档 V3.0 中定义的、基于节点返回类型的全新逻辑。
    -   **实现:**
        1.  清空原方法内容。
        2.  实现**场景A (根节点)** 的逻辑。
        3.  实现**场景B (上下文对象)** 的逻辑。
        4.  实现**场景C (属性)** 的核心分支逻辑：
            -   调用 `getPropertyFromPath` 获取当前属性的 `typeName`。
            -   使用 `isBaseType` 和 `isCollectionType` 进行判断。
            -   **实现子场景 C1 (基础类型):** 返回操作符和兼容函数。
            -   **实现子场景 C2 (对象类型):** 返回下一级属性。
            -   **实现子场景 C3 (集合类型):** 返回通用集合函数和列表查询操作。
    -   **验证:** 在不同 `nodeTree` 状态下调用此方法，检查返回的 `options` 数组是否符合预期。

### 阶段三：更新节点操作与状态管理

-   [ ] **任务 3.1: 增强 `addNode(option)`**
    -   **文件:** `src/components/DslEditor.js`
    -   **目标:** 确保 `addNode` 能够处理新的 `listQuery` 选项类型，并正确更新 `nodeTree` 的状态。
    -   **实现:**
        -   在 `if (option.type === 'property')` 的逻辑中，确保 `this.nodeTree` 对象被赋予一个新的 `returnType` 属性，其值来自 `option.returnType`。
        -   添加一个新的 `else if (option.type === 'listQuery')` 分支。
            -   在此分支中，需要构建一个新的 `nodeTree` 对象，其 `type` 为 `listQuery`。
            -   **关键:** 如果 `option.value` 是 `.query`，则需要为 `nodeTree` 添加一个 `subContextType` 属性，其值为当前列表的内部类型（通过 `getCollectionInnerType` 获得）。
    -   **验证:** 添加一个属性节点后，检查 `this.nodeTree.returnType` 是否正确。添加一个 `.query` 节点后，检查 `this.nodeTree.subContextType` 是否正确。

-   [ ] **任务 3.2: (进阶) 实现 `.query()` 的嵌套渲染**
    -   **文件:** `src/components/DslEditor.js`
    -   **目标:** 修改 `renderNodeTreeRecursive` 方法，当遇到 `type === 'listQuery'` 且 `value === '.query'` 的节点时，渲染出一个新的、独立的 `DslEditor` 实例。
    -   **实现:**
        -   在 `renderNodeTreeRecursive` 中添加对新节点类型的处理。
        -   当渲染子编辑器时，需要将 `node.subContextType` 作为 `customContext` 传递给新的 `DslEditor` 实例。
        -   `DslEditor` 的 `render` 方法需要修改以接受这个 `customContext`，并在 `getAvailableOptions` 的根节点场景中优先使用它。
    -   **验证:** 在UI上，对一个列表使用 `.query` 操作后，应该出现一个新的、括号内的DSL编辑器，其 `[+]` 菜单的初始选项应该是列表元素的属性（通过 `self` 访问）。

## 4. 总结

这份实施指南为 `DslEditor` 的重构任务提供了从零开始的、详尽的操作步骤和风险提示。开发者应严格按照此文档的清单顺序执行，并在每一步完成后进行验证，以确保最终产出的代码质量和功能的正确性。