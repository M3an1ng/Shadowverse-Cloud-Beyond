# DSL 编辑器架构与工作流程指南

## 1. 核心理念：统一的状态模型

经过多次迭代，DSL编辑器最终确立了一套以 **“虚拟根节点 (Virtual Root)”** 和 **“编辑路径 (Editing Path)”** 为核心的统一状态管理模型。这个模型旨在消除所有特殊情况，将编辑器的每一种操作（无论是初始化、构建新表达式，还是修改现有表达式的某个部分）都归结为同一种基础操作：**对AST（抽象语法树）中某个节点的特定槽位 (Slot) 进行编辑**。

- **虚拟根节点 (`virtualRoot`)**: 这是一个存在于内存中的、不可见的顶层节点。它只有一个名为 `main` 的槽位，专门用于存放**主表达式 (Main Expression)**。通过引入它，对主表达式的编辑就变成了“对 `virtualRoot` 的 `main` 槽位进行编辑”，从而与编辑其他任何节点的子槽位（如操作符的左/右操作数）在逻辑上完全统一。

- **编辑路径 (`editingPath`)**: 这是一个栈结构，精确地记录了当前工作区 (Workspace) 正在编辑的内容来源于AST中的哪个位置。路径中的每一项都包含了 `parentNode` (父节点) 和 `slot` (槽位名)。`editingPath` 是驱动编辑器所有状态转换的唯一依据。

## 2. 关键概念定义

### 2.1. 主表达式 (Main Expression)

- **定义**: 用户最终构建完成的、完整的DSL表达式。它在UI上始终可见，是最终的“产品”。
- **存储**: 作为 `virtualRoot.main` 的内容存在。
- **状态**: 主表达式自身总是处于“已完成”或“部分完成”状态，它本身不可直接修改。对它的任何修改都必须通过进入其编辑模式来完成。

### 2.2. 工作区 (Workspace)

- **定义**: 用户进行实际构建和编辑操作的区域。它是动态变化的，反映了当前正在处理的表达式片段。
- **存储**: 由 `workspaceAST` 和 `astRoot` 两个属性表示。
- **状态**: 工作区的内容可以是任意的、不完整的表达式片段。用户的每一次选择（添加数据源、操作符等）都会直接反映在工作区中。

### 2.3. 封装节点 (Encapsulated Node)

- **定义**: 一个特殊的 `ValueNode`，其 `metadata.type` 为 `encapsulated`。它将一个完整的子表达式（存储在 `metadata.innerAST`）包裹起来，使其成为一个独立的、可以被当作单个值参与更高级运算的单元。
- **UI表现**: 在UI上通常显示为带有圆角边框的“胶囊”样式，例如 `(6)` 或 `(Target.HP > 10)`。
- **作用**:
    - **明确运算优先级**: 确保括号内的表达式先被计算。
    - **提供编辑入口**: 用户通过点击封装节点，可以进入其内部，对子表达式进行修改。

### 2.4. 占位符/空白表达式 (Placeholder / Empty Node)

- **定义**: 一个特殊的封装节点，其内部包裹的是一个 `metadata.type` 为 `empty` 的空节点。
- **UI表现**: 显示为一个黄色的、有脉冲动画的 `( )` 胶囊，明确提示用户此处需要输入。
- **作用**:
    - **初始化**: 编辑器初始化时，主表达式就是一个空白占位符。
    - **操作符**: 当添加一个需要两个操作数的操作符（如 `>`）时，右操作数会先以占位符的形式出现，引导用户填充。

## 3. 核心工作流程

### 3.1. 初始化流程

1.  **`render(container, "")`**: 外部调用 `render` 方法，传入空表达式字符串。
2.  **`initializeWorkspace()`**:
    - 创建一个空的封装节点（占位符）。
    - 将其赋值给 `virtualRoot.main`。
    - **立即调用 `handleEnterEditModeForSlot(this.virtualRoot, 'main')`**。
3.  **`handleEnterEditModeForSlot()`**:
    - 设置 `editingPath` 为 `[{ parentNode: virtualRoot, slot: 'main' }]`。
    - 解包 `virtualRoot.main` 的内容（即那个空节点）到工作区。
    - 此时 `workspaceAST` 为一个 `empty` 节点。
4.  **`renderEditor()`**:
    - `ui-main.js` 渲染主表达式区域，根据 `editingPath` 的内容，发现主表达式正在被编辑，可能会显示一个“正在编辑”的覆盖提示。
    - `ui-workspace.js` 渲染工作区，由于 `workspaceAST` 是 `empty`，UI显示“点击 [+] 开始构建表达式”。

### 3.2. 构建新表达式 (以 `6 > 5` 为例)

1.  **输入 `6`**:
    - 用户点击 `[+]`，选择“静态值”。
    - `addNode()` 调用 `createRootNode()`，创建一个 `staticValue` 节点并赋值给 `workspaceAST`。
    - 用户输入 `6`。
2.  **保存 `6`**:
    - 用户点击“保存”按钮，调用 `saveWorkspace()` -> `saveCurrentLayer()`。
    - `editingPath` 指向 `virtualRoot.main`。
    - `saveCurrentLayer` 将工作区的 `6` 封装成 `(6)`。
    - `writeBackToParentSlot` 将 `(6)` 写回 `virtualRoot.main`。
    - `popEditingPath`，编辑路径变为空。
    - `ascend(virtualRoot)` 被调用。
    - `ascend` 发现父节点是 `virtualRoot`，将**已更新的** `mainExpressionAST` (即 `(6)`) 加载到工作区。
    - UI刷新，主表达式显示 `(6)`，工作区也显示 `(6)`，用户可以继续操作。
3.  **添加 `>`**:
    - 用户在工作区 `(6)` 的基础上，点击 `[+]` 选择 `>` 操作符。
    - `addNode()` 调用 `extendAST()` -> `createOperatorFromCurrent()`。
    - `createOperatorFromCurrent` 将当前工作区的 `(6)` 作为左操作数，创建一个新的操作符节点 `(6) > ( )`。
    - **关键**: `createOperatorFromCurrent` 会立即将这个新节点写回它自己的来源地，即 `virtualRoot.main`。
    - 然后，它立即调用 `handleEnterEditModeForSlot`，进入新创建的操作符节点的 `right` 槽位进行编辑。
4.  **输入 `5` 并保存**:
    - 流程与步骤1、2类似，但此时 `editingPath` 指向的是 `>` 操作符的 `right` 槽位。
    - 保存后，`ascend` 会将 `>` 操作符节点加载到工作区。
    - 用户可以再次保存，将 `(6 > 5)` 写回 `virtualRoot.main`。

### 3.3. 编辑现有表达式

1.  **点击进入**: 用户点击主表达式中的任意封装节点（例如 `(Target.HP > 10)` 中的 `(Target.HP)`)。
2.  **`handleEnterEditModeForSlot()`**:
    - 点击事件会找到被点击节点的父节点 (`>`) 和它所在的槽位 (`left`)。
    - `handleEnterEditModeForSlot` 被调用，`editingPath` 被设置为 `[{ parentNode: '>', slot: 'left' }]`。
    - 被点击节点 `(Target.HP)` 的**内部内容** `Target.HP` 被解包到工作区。
3.  **修改并保存**:
    - 用户在工作区修改表达式。
    - 点击保存，`saveCurrentLayer` 会将工作区的新内容封装，并写回 `>` 操作符的 `left` 槽位。
    - `ascend` 会将 `>` 操作符加载到工作区，完成一次编辑循环。

## 4. 状态与UI渲染

- **UI渲染完全由 `editingPath` 和 `workspaceAST` 驱动**。
- **主表达式区域 (`ui-main.js`)**:
    - 正常情况下，渲染 `mainExpressionAST`。
    - 当 `editingPath` 不为空时，可以根据路径信息高亮当前正在被编辑的部分。
- **工作区 (`ui-workspace.js`)**:
    - 始终渲染 `workspaceAST` 的内容。
    - 如果 `workspaceAST` 为空或 `empty`，显示初始的 `[+]` 添加按钮。
    - 否则，显示当前工作区表达式的UI和可用的后续操作选项。

这份文档确立了编辑器后续所有功能开发和BUG修复的基准。