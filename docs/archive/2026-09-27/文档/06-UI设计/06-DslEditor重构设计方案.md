# 06. DslEditor 重构实施手册 (V4.0)

**版本: 4.0**

**作者: AI 技术总监**

**更新日期: 2025-08-24**

## 1. 目标

本文档是 `src/components/DslEditor.js` 组件重构工作的**唯一**、**权威**的开发指南。它旨在为接手该任务的程序员提供从零开始完成重构所需的所有信息，包括任务背景、设计思路、详细的步骤、代码实现指导、以及潜在问题的解决方案。

**核心任务：** 将 `getAvailableOptions` 方法重构为一个健壮的、上下文感知的推荐引擎，使其能够根据DSL表达式的当前状态，精确地提供所有合法的下一步操作。

## 2. 背景与设计回顾

- **元数据驱动:** 编辑器的所有选项（对象、属性、函数等）都由后端 `/api/AbilityEditorMetadata` 接口提供。前端**严禁**硬编码任何游戏逻辑。
- **链式构建:** 用户的每一步选择都决定了下一步的可用选项。
- **通用列表查询:** `.query()`, `.orderBy()`, `.take()` 等操作是通用的，适用于任何 `List<T>` 类型的节点，其核心是改变内部DSL表达式的上下文（引入 `self` 关键字）。

## 3. 任务清单 (Task Checklist)

- [ ] **步骤一：** 实现类型检查辅助函数
- [ ] **步骤二：** 改造 `getPropertyFromPath` 以获取完整类型信息
- [ ] **步骤三：** 重构 `getAvailableOptions` 的主体逻辑
- [ ] **步骤四：** 增强 `addNode` 以支持新节点和上下文切换
- [ ] **步骤五：** 清理旧的兼容性代码

---

## 4. 详细实施步骤

### 步骤一：实现类型检查辅助函数

**目标:** 创建一组纯函数，用于识别元数据中的类型名称。

**位置:** `DslEditor` 类的末尾。

**任务:**
1.  在 `DslEditor.js` 文件末尾，`setExpression` 方法之后，添加以下三个函数。

**代码实现:**
```javascript
// DslEditor.js

    // ... (setExpression 方法) ...

    // ----------------------------------------------------------------
    // 类型检查辅助函数 (Type Checking Helpers)
    // ----------------------------------------------------------------

    /**
     * 判断一个类型名是否为基础类型 (e.g., "Int32", "Boolean")
     * @param {string} typeName - 类型名称
     * @returns {boolean}
     */
    isBaseType(typeName) {
        const baseTypes = ['Int32', 'Boolean', 'String', 'Guid', 'Single', 'Double', 'Int64'];
        return baseTypes.includes(typeName);
    }

    /**
     * 判断一个类型名是否为集合类型 (e.g., "List<CardInstance>")
     * @param {string} typeName - 类型名称
     * @returns {boolean}
     */
    isCollectionType(typeName) {
        return typeName && (typeName.startsWith('List<') || typeName.startsWith('IEnumerable<'));
    }

    /**
     * 从集合类型中提取内部泛型类型
     * @param {string} typeName - 集合类型名称 (e.g., "List<CardInstance>")
     * @returns {string|null} - 内部类型名称 (e.g., "CardInstance") or null
     */
    getCollectionInnerType(typeName) {
        if (!this.isCollectionType(typeName)) {
            return null;
        }
        const match = typeName.match(/<(.*)>/);
        return match ? match[1] : null;
    }
}
```

**潜在问题与解决方案:**
- **问题:** 后端未来可能增加新的基础类型。
- **解决方案:** 当前设计是可接受的。如果基础类型频繁变动，未来可考虑将 `baseTypes` 列表也加入到元数据中。

### 步骤二：改造 `getPropertyFromPath`

**目标:** 增强此函数，使其不仅能找到属性，还能返回属性的完整上下文信息，为 `getAvailableOptions` 提供决策依据。

**位置:** 替换 `DslEditor.js` 中现有的 `getPropertyFromPath` 方法。

**任务:**
1.  用下面的新实现完全替换旧的 `getPropertyFromPath` 方法。

**代码实现:**
```javascript
// DslEditor.js

    /**
     * 根据完整路径获取属性及其所有者类型的信息
     * @param {string} fullPath - 属性的完整路径 (e.g., "player.hand")
     * @returns {{property: object, ownerType: object}|null}
     */
    getPropertyFromPath(fullPath) {
        if (!fullPath) return null;
    
        const parts = fullPath.split('.');
        if (parts.length === 0) return null;
    
        let currentTypeName;
        let currentTypeMeta;
        let propertyMeta = null;
    
        // 1. 确定起点上下文
        const contextObject = this.getDslContextObject(parts[0]);
        if (!contextObject) {
            console.warn(`DslEditor: Could not find root context object: ${parts[0]}`);
            return null;
        }
        currentTypeName = contextObject.typeName;
    
        // 2. 从根开始，迭代解析路径的每一部分
        for (let i = 1; i < parts.length; i++) {
            const propertyName = parts[i];
            currentTypeMeta = this.getTypeMetadata(currentTypeName);
    
            if (!currentTypeMeta || !currentTypeMeta.properties) {
                console.warn(`DslEditor: No metadata or properties found for type: ${currentTypeName}`);
                return null;
            }
    
            propertyMeta = currentTypeMeta.properties.find(prop => prop.name === propertyName);
            if (!propertyMeta) {
                console.warn(`DslEditor: Property "${propertyName}" not found in type: ${currentTypeName}`);
                return null;
            }
    
            // 为下一次迭代准备
            currentTypeName = propertyMeta.typeName;
        }
    
        // 返回最终找到的属性，以及它所属的直接父类型的元数据
        return { property: propertyMeta, ownerType: currentTypeMeta };
    }
```

**潜在问题与解决方案:**
- **问题:** `fullPath` 的第一部分如果不是一个合法的根上下文对象，会返回 `null`。
- **解决方案:** 这是预期行为。`getPropertyFromPath` 的调用者（主要是 `getAvailableOptions`）需要处理 `null` 返回值。

### 步骤三：重构 `getAvailableOptions` 主体逻辑

**目标:** 将此方法的核心逻辑替换为我们设计的、清晰的、基于类型判断的分类处理流程。

**位置:** 替换 `DslEditor.js` 中现有的 `getAvailableOptions` 方法。

**任务:**
1.  用下面的新实现完全替换旧的 `getAvailableOptions` 方法。

**代码实现:**
```javascript
// DslEditor.js

    /**
     * 获取可用选项 (V3.0 重构版)
     */
    getAvailableOptions() {
        // 场景 A: 根节点
        if (!this.nodeTree) {
            const contextObjects = this.metadata?.dsl?.contextObjects || [];
            return contextObjects.map(obj => ({
                type: 'context',
                value: obj.name,
                label: obj.displayName,
                description: obj.description,
                returnType: obj.typeName
            }));
        }

        const nodeType = this.nodeTree.type;
        const returnType = this.nodeTree.returnType; // 假设 nodeTree 中已存储了返回类型

        // 场景 B: 上下文对象
        if (nodeType === 'context') {
            const contextObj = this.getDslContextObject(this.nodeTree.name);
            if (contextObj && contextObj.properties) {
                return contextObj.properties.map(prop => ({
                    type: 'property',
                    value: prop.name,
                    label: prop.displayName,
                    description: prop.description,
                    returnType: prop.typeName
                }));
            }
        }

        // 场景 C: 属性
        if (nodeType === 'property') {
            // C1: 基础类型
            if (this.isBaseType(returnType)) {
                let options = [];
                const operators = ['==', '!=', '>', '>=', '<', '<='];
                options.push(...operators.map(op => ({ type: 'operator', value: op, label: op })));
                // TODO: 添加函数筛选逻辑
                return options;
            }
            // C3: 集合类型
            if (this.isCollectionType(returnType)) {
                let options = [];
                // 添加通用集合函数
                const collectionFunctions = this.metadata.dsl.functions.filter(f => f.parameters.some(p => p.typeName === "IEnumerable"));
                options.push(...collectionFunctions.map(f => ({ type: 'function', value: f.name, label: f.name, meta: f })));
                // 添加列表查询操作
                options.push({ type: 'listQuery', value: '.query', label: '筛选 (.query)' });
                options.push({ type: 'listQuery', value: '.orderBy', label: '排序 (.orderBy)' });
                options.push({ type: 'listQuery', value: '.take', label: '提取 (.take)' });
                return options;
            }
            // C2: 对象类型
            const typeMeta = this.getTypeMetadata(returnType);
            if (typeMeta && typeMeta.properties) {
                return typeMeta.properties.map(prop => ({
                    type: 'property',
                    value: prop.name,
                    label: prop.displayName,
                    description: prop.description,
                    returnType: prop.typeName
                }));
            }
        }
        
        // 场景 D & E (函数和操作符) - 待实现
        if (nodeType === 'function' || nodeType === 'operator') {
             // 暂时返回操作符
             const operators = ['==', '!=', '&&', '||'];
             return operators.map(op => ({ type: 'operator', value: op, label: op }));
        }

        return []; // 默认返回空
    }
```

**潜在问题与解决方案:**
- **问题:** `this.nodeTree.returnType` 必须被正确维护。
- **解决方案:** 这是下一步 `addNode` 方法需要解决的核心问题。`addNode` 必须在每次添加新节点时，精确地计算并存储新 `nodeTree` 的 `returnType`。

### 步骤四：增强 `addNode`

**目标:** 改造 `addNode`，使其在构建 `nodeTree` 时，能够正确地维护 `returnType` 属性，并为 `.query()` 等操作创建正确的嵌套上下文。

**位置:** 替换 `DslEditor.js` 中现有的 `addNode` 方法。

**任务:**
1.  用下面的新实现完全替换旧的 `addNode` 方法。

**代码实现:**
```javascript
// DslEditor.js

    /**
     * 添加节点 (V3.0 重构版)
     */
    addNode(option) {
        const newFullPath = this.nodeTree ? `${this.nodeTree.fullPath}.${option.value}` : option.value;

        if (option.type === 'context' || option.type === 'property') {
            this.nodeTree = {
                type: option.type,
                name: option.value,
                fullPath: newFullPath,
                returnType: option.returnType // **核心：保存返回类型**
            };
        } 
        else if (option.type === 'listQuery') {
            // **核心：处理列表查询**
            // 暂时只更新路径，完整的上下文切换需要更复杂的UI状态管理
            this.nodeTree = {
                ...this.nodeTree,
                fullPath: `${this.nodeTree.fullPath}${option.value}(...)`, // UI上显示占位符
                // 这里可以增加一个状态，表示等待输入查询表达式
                isAwaitingQuery: true, 
                queryContextType: this.getCollectionInnerType(this.nodeTree.returnType)
            };
        }
        // ... 其他类型 (function, operator) 的处理 ...

        this.updateExpression();
        this.renderEditor();
    }
```

**潜在问题与解决方案:**
- **问题:** `listQuery` 的实现非常复杂，它需要动态创建一个新的、具有不同上下文（`self` 指向 `queryContextType`）的 `DslEditor` 实例。
- **解决方案:** 这是整个重构中最难的部分。初期可以只实现路径更新，UI上显示一个占位符 `(...)`。完整的嵌套编辑器功能可以作为下一个迭代任务。

### 步骤五：清理旧代码

**目标:** 移除在重构后不再需要的旧的辅助函数。

**任务:**
1.  删除 `isFunctionCompatible` 和 `isListFunctionCompatible` 方法，因为新的类型判断逻辑已经完全取代了它们。

---
## 5. 总结

这份实施手册为 `DslEditor.js` 的重构提供了明确、可执行的路径。开发者应严格按照任务清单和代码实现指导进行操作。在实现过程中，应特别关注 `nodeTree` 中 `returnType` 状态的维护，以及为实现 `listQuery` 嵌套上下文所做的准备工作。