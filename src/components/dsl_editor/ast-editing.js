import {
    createValueNode,
    createOperatorNode,
    createFunctionCallNode,
    createListQueryNode,
    createListSortNode,
    createListTakeNode,
    validateAST,
    findIncompleteNode,
    findRightmostLeafNode
} from "./core.js";

export function performASTGoBack(editor) {
    if (!editor.astRoot) {
        return { success: false, newRoot: null };
    }

    // 如果是单个值节点
    if (editor.astRoot.type === 'value') {
        // 如果是封装的表达式，解封装回到内部AST
        if (editor.astRoot.metadata && editor.astRoot.metadata.type === "encapsulated" && editor.astRoot.metadata.innerAST) {
            console.log("DslEditor: Unencapsulating expression:", editor.astRoot.metadata.innerAST);
            editor.isExpressionCompleted = false; // 重置完成状态
            return { success: true, newRoot: editor.astRoot.metadata.innerAST };
        }
        // 普通值节点，直接清空
        return { success: true, newRoot: null };
    }

    // 如果是操作符节点
    if (editor.astRoot.type === 'operator') {
        // 如果右操作数存在，移除右操作数
        if (editor.astRoot.rightOperand) {
            editor.astRoot.setRightOperand(null);
            return { success: true, newRoot: editor.astRoot };
        }

        // 如果只有左操作数，返回左操作数
        if (editor.astRoot.leftOperand) {
            const leftOperand = editor.astRoot.leftOperand;
            leftOperand.parent = null; // 清除父节点引用
            return { success: true, newRoot: leftOperand };
        }

        // 如果没有操作数，清空
        return { success: true, newRoot: null };
    }

    return { success: false, newRoot: editor.astRoot };
}

export function saveCurrentLayer(editor) {
    const top = editor.peekEditingPath();
    if (!top) {
        console.warn('DslEditor: saveCurrentLayer called with empty editingPath');
        return;
    }
    if (!editor.workspaceAST || findIncompleteNode(editor.workspaceAST)) {
        editor.showExpressionError("工作区表达式不完整，无法保存。");
        console.warn('DslEditor: saveCurrentLayer failed: workspaceAST is null or incomplete');
        return;
    }

    // 关键修复：在调用 ascend() 之前捕获要保存的 AST，因为 ascend() 会将 this.workspaceAST 设置为 null
    const astToSave = editor.workspaceAST;

    const { parentNode, slot, index } = top;

    if (!parentNode) {
        console.error('DslEditor: saveCurrentLayer failed: parentNode from editingPath is null.');
        editor.showExpressionError("保存失败：编辑上下文丢失。");
        return;
    }

    console.log('DslEditor: saveCurrentLayer - Found target parent node from editing path:', parentNode);

    // 保存门槛：空表达式不可保存
    if (astToSave?.type === 'value' && astToSave?.metadata?.type === 'empty') {
        editor.showExpressionError("表达式不能为空。");
        return;
    }

    // 保存门槛：AST 语义校验
    const astValidation = validateAST(astToSave);
    if (!astValidation?.isValid) {
        const msg = (astValidation?.errors || []).map(e => String(e)).join('\n');
        editor.showExpressionError(msg || "表达式不合法，无法保存。");
        return;
    }

    // 预期类型由胶囊提示展示，最终由完整表达式校验把关

    // 关键修复：重构保存逻辑以正确处理虚拟根节点
    // 遵循规则：保存时总是对工作区内容进行封装
    const valueToWrite = editor.createEncapsulatedFrom(astToSave);

    let ok = false;
    if (parentNode.type === 'functionCall' && slot === 'arg') {
        const argIndex = typeof index === 'number' ? index : 0;
        if (typeof parentNode.setArg === 'function') {
            parentNode.setArg(argIndex, valueToWrite);
            ok = true;
        }
    } else {
        ok = editor.writeBackToParentSlot(parentNode, slot, valueToWrite);
    }

    if (!ok) {
        console.warn('DslEditor: writeBackToParentSlot failed unexpectedly.');
        return;
    }

    editor.popEditingPath();

    // 关键修复：保存完成后，应该上升到刚刚被修改的父节点进行预览
    editor.ascend(parentNode);
}

export function createRootNode(editor, option) {
    if (option.type === "context") {
        editor.astRoot = createValueNode(option.value, option.returnType, {
            type: "context",
            name: option.value,
            displayName: option.label
        });
    } else if (option.type === "staticValue") {
        // 创建静态值节点，等待用户输入
        editor.astRoot = createValueNode("[输入值]", "Object", {
            type: "staticValue",
            isAwaitingInput: true,
            staticValue: ""
        });
        editor.currentNode = editor.astRoot;
    } else if (option.type === "function") {
        const signature = option.meta || {};
        const parameters = signature.parameters || signature.Parameters || [];
        editor.astRoot = createFunctionCallNode(option.value, option.returnType || signature.returnType || signature.returnTypeName || 'Object', parameters);
    } else if (option.type === "eventField") {
        editor.astRoot = createValueNode(option.value, option.returnType, {
            type: "eventField",
            displayValue: option.meta?.displayValue || option.label,
            eventType: option.meta?.eventType,
            propertyName: option.meta?.propertyName,
            scope: option.meta?.scope
        });
    }

    // 关键修复：同步workspaceAST
    editor.workspaceAST = editor.astRoot;
    console.log("DslEditor: Created root node, synced workspaceAST:", {
        astRoot: editor.astRoot?.toExpression?.() || null,
        workspaceAST: editor.workspaceAST?.toExpression?.() || null
    });

    // 触发状态更新
    editor.triggerStateUpdate();
}

export function extendAST(editor, option) {
    // 新逻辑：优先处理基于选区的操作
    // 统一逻辑：所有操作符的添加都基于当前工作区内容

    // 关键修复：如果当前不在编辑模式（没有路径），但工作区有内容，则自动进入扩展主表达式模式
    if (editor.editingPath.length === 0 && editor.workspaceAST && validateAST(editor.workspaceAST).isValid) {
        console.log("DslEditor: Auto-expanding main expression before extending.");
        editor.expandMainExpression();
    }

    const incompleteNode = findIncompleteNode(editor.astRoot);

    // 关键修复：如果当前不在编辑模式（没有路径），但工作区有内容，则自动进入扩展主表达式模式
    if (editor.editingPath.length === 0 && editor.workspaceAST && validateAST(editor.workspaceAST).isValid) {
        console.log("DslEditor: Auto-expanding main expression before extending.");
        editor.expandMainExpression();
        return; // 关键：准备好扩展上下文后，等待用户下一步操作
    }

    if (incompleteNode && incompleteNode.type === 'operator') {
        // 为操作符添加右操作数
        addRightOperandToOperator(editor, incompleteNode, option);
    } else if (incompleteNode && incompleteNode.type === 'functionCall') {
        addArgToFunctionCall(editor, incompleteNode, option);
    } else if (option.type === "operator") {
        // 创建新的操作符节点 (基于工作区)
        createOperatorFromCurrent(editor, option);
    } else if (option.type === "property") {
        // 扩展属性访问
        editor.extendPropertyAccess(option);
    } else if (option.type === "eventField") {
        const newNode = createValueNode(option.value, option.returnType, {
            type: "eventField",
            displayValue: option.meta?.displayValue || option.label,
            eventType: option.meta?.eventType,
            propertyName: option.meta?.propertyName,
            scope: option.meta?.scope
        });
        const rightmostNode = findRightmostLeafNode(editor.astRoot);
        if (rightmostNode) {
            editor.replaceNodeInAST(rightmostNode, newNode);
        } else {
            editor.astRoot = newNode;
            editor.workspaceAST = newNode;
        }
    } else if (option.type === "listPipeline") {
        editor.openPipelineFromCurrent();
    } else if (option.type === "listQuery") {
        // 创建查询节点
        createQueryNodeFromCurrent(editor, option);
    } else if (option.type === "listSort") {
        createSortNodeFromCurrent(editor, option);
    } else if (option.type === "listTake") {
        createTakeNodeFromCurrent(editor, option);
    } else if (option.type === "function") {
        createFunctionCallFromCurrent(editor, option);
    }
}

export function createFunctionCallFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    const signature = option.meta || {};
    const parameters = signature.parameters || signature.Parameters || [];
    const returnType = option.returnType || signature.returnType || signature.returnTypeName || 'Object';

    const functionNode = createFunctionCallNode(option.value, returnType, parameters);

    // Use current workspace as first argument when the function expects at least one parameter.
    if (parameters.length > 0) {
        functionNode.setArg(0, editor.cloneAST(editor.workspaceAST));
    }

    editor.workspaceAST = functionNode;
    editor.astRoot = functionNode;

    // If there are more required arguments, guide the user into editing the next slot.
    const nextIndex = parameters.findIndex((p, i) => !p?.isOptional && !functionNode.args[i]);
    if (nextIndex >= 0) {
        editor.handleEnterEditModeForSlot(functionNode, 'arg', nextIndex);
    }
}

export function addArgToFunctionCall(editor, functionNode, option) {
    const params = functionNode.parameters || [];
    const args = functionNode.args || [];
    const argIndex = params.findIndex((p, i) => !p?.isOptional && !args[i]);
    if (argIndex < 0) {
        editor.showExpressionError('函数参数已齐全或不可编辑');
        return;
    }

    const makeNodeFromOption = (opt) => {
        if (opt.type === 'context') {
            return createValueNode(opt.value, opt.returnType, { type: 'context', name: opt.value, displayName: opt.label });
        }
        if (opt.type === 'property') {
            return createValueNode(opt.value, opt.returnType, {
                type: 'property',
                displayName: opt.label,
                fullPath: opt.value
            });
        }
        if (opt.type === 'staticValue') {
            return createValueNode("[输入值]", "Object", { type: "staticValue", isAwaitingInput: true, staticValue: "" });
        }
        if (opt.type === 'function') {
            const signature = opt.meta || {};
            const parameters = signature.parameters || signature.Parameters || [];
            return createFunctionCallNode(opt.value, opt.returnType || signature.returnType || signature.returnTypeName || 'Object', parameters);
        }
        if (opt.type === 'eventField') {
            return createValueNode(opt.value, opt.returnType, {
                type: "eventField",
                displayValue: opt.meta?.displayValue || opt.label,
                eventType: opt.meta?.eventType,
                propertyName: opt.meta?.propertyName,
                scope: opt.meta?.scope
            });
        }
        return null;
    };

    const argNode = makeNodeFromOption(option);
    if (!argNode) return;

    functionNode.setArg(argIndex, argNode);
    editor.astRoot = editor.astRoot; // no-op; keep structure
    editor.workspaceAST = editor.astRoot;
}

export function addRightOperandFromWorkspace(editor, option) {
    console.log("DslEditor: Adding right operand from workspace:", option);

    // 在工作区创建右操作数节点
    if (option.type === "context") {
        editor.astRoot = createValueNode(option.value, option.returnType, {
            type: "context",
            name: option.value,
            displayName: option.label
        });
    } else if (option.type === "staticValue") {
        editor.astRoot = createValueNode("[输入值]", "Object", {
            type: "staticValue",
            isAwaitingInput: true,
            staticValue: ""
        });
        editor.currentNode = editor.astRoot;
    } else if (option.type === "function") {
        const signature = option.meta || {};
        const parameters = signature.parameters || signature.Parameters || [];
        editor.astRoot = createFunctionCallNode(option.value, option.returnType || signature.returnType || signature.returnTypeName || 'Object', parameters);
    } else if (option.type === "eventField") {
        editor.astRoot = createValueNode(option.value, option.returnType, {
            type: "eventField",
            displayValue: option.meta?.displayValue || option.label,
            eventType: option.meta?.eventType,
            propertyName: option.meta?.propertyName,
            scope: option.meta?.scope
        });
    }

    editor.workspaceAST = editor.astRoot;
    console.log("DslEditor: Right operand created in workspace, awaiting completion");
}

export function addRightOperandToOperator(editor, operatorNode, option) {
    let rightOperand;

    if (option.type === "context") {
        rightOperand = createValueNode(option.value, option.returnType, {
            type: "context",
            name: option.value,
            displayName: option.label
        });
    } else if (option.type === "staticValue") {
        rightOperand = createValueNode("[输入值]", "Object", {
            type: "staticValue",
            isAwaitingInput: true,
            staticValue: ""
        });
        editor.currentNode = rightOperand;
    } else if (option.type === "function") {
        rightOperand = createValueNode(`${option.value}()`, option.returnType, {
            type: "function",
            functionName: option.value,
            arguments: []
        });
    }

    if (rightOperand) {
        operatorNode.setRightOperand(rightOperand);

        // 简化：操作符作为普通AST节点在工作区正常处理
        // 工作区继续指向整个操作符节点，而不是切换到右操作数
        editor.workspaceAST = operatorNode;
        editor.astRoot = operatorNode;

        console.log('DslEditor: addRightOperandToOperator -> right operand set, operator remains in workspace');
    }
}

export function createOperatorFromCurrent(editor, option) {
    const operator = option.operator || option.value;
    if (!editor.workspaceAST) {
        console.error("DslEditor: Cannot create operator, workspace is empty.");
        return;
    }

    // 步骤 A: 自动保存当前工作区内容
    // 获取当前编辑上下文，但不弹出
    const top = editor.peekEditingPath();
    if (!top || !top.parentNode) {
        console.error("DslEditor: Cannot create operator, no editing context found for auto-save.");
        editor.showExpressionError("无法添加操作符：编辑上下文丢失。");
        return;
    }

    // 直接将当前工作区内容（例如，手动封装后的节点）写回主AST
    const rawLeftOperand = editor.cloneAST(editor.workspaceAST);
    const leftOperandNode = rawLeftOperand?.metadata?.type === 'encapsulated'
        ? rawLeftOperand
        : editor.createEncapsulatedFrom(rawLeftOperand);
    editor.writeBackToParentSlot(top.parentNode, top.slot, leftOperandNode);
    console.log("DslEditor: Step A - Auto-saved workspace to main AST.", leftOperandNode);

    // 步骤 B: 生成操作符和相应的占位符
    const rightOperandPlaceholder = createValueNode("", "Void", { type: 'empty' });
    const newOperatorNode = createOperatorNode(operator);
    newOperatorNode.setLeftOperand(leftOperandNode); // 使用封装后的左操作数保持一致显示
    newOperatorNode.setRightOperand(rightOperandPlaceholder);
    console.log("DslEditor: Step B - Created new operator node.", newOperatorNode);

    // 步骤 C: 检测并创建外层封装
    const newEncapsulatedNode = editor.createEncapsulatedFrom(newOperatorNode);
    console.log("DslEditor: Step C - Encapsulated the new operator node.", newEncapsulatedNode);

    // 步骤 D: 更新主表达式，并将工作区切换到新加载的空白表达式
    // 再次写入同一个槽位，用包含操作符的新封装节点覆盖掉旧的
    editor.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);
    console.log("DslEditor: Step D - Wrote back the final encapsulated operator node to main AST.");

    // 从新的主AST节点中获取内部操作符节点的正确引用
    const operatorNodeInMainAST = newEncapsulatedNode.metadata.innerAST;

    // 进入新操作符右侧占位符的编辑模式
    editor.handleEnterEditModeForSlot(operatorNodeInMainAST, 'right');
    console.log("DslEditor: Step D - Switched to edit mode for the new placeholder.");
}

export function createQueryNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    const top = editor.peekEditingPath();
    if (!top || !top.parentNode) {
        editor.showExpressionError("无法添加筛选：编辑上下文丢失。");
        return;
    }

    // 步骤 A: 自动保存当前工作区内容
    const sourceNode = editor.cloneAST(editor.workspaceAST);
    editor.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

    // 步骤 B: 创建查询节点和占位符
    const queryNode = createListQueryNode(sourceNode);
    const placeholder = createValueNode("", "Void", { type: 'empty' });
    queryNode.setFilterExpression(placeholder);

    // 步骤 C: 检测并创建外层封装
    const newEncapsulatedNode = editor.createEncapsulatedFrom(queryNode);

    // 步骤 D: 更新主表达式，并将工作区切换到新加载的空白表达式
    editor.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);

    const queryNodeInMainAST = newEncapsulatedNode.metadata.innerAST;

    // 进入新操作符右侧占位符的编辑模式
    editor.handleEnterEditModeForSlot(queryNodeInMainAST, 'filterExpression');
}

export function createSortNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    const top = editor.peekEditingPath();
    if (!top || !top.parentNode) {
        editor.showExpressionError("无法添加排序：编辑上下文丢失。");
        return;
    }

    const sourceNode = editor.cloneAST(editor.workspaceAST);
    editor.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

    const sortNode = createListSortNode(sourceNode);
    const placeholder = createValueNode("", "Void", { type: 'empty' });
    sortNode.setSortExpression(placeholder);

    const newEncapsulatedNode = editor.createEncapsulatedFrom(sortNode);
    editor.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);

    const sortNodeInMainAST = newEncapsulatedNode.metadata.innerAST;
    editor.handleEnterEditModeForSlot(sortNodeInMainAST, 'sortExpression');
}

export function createTakeNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    const top = editor.peekEditingPath();
    if (!top || !top.parentNode) {
        editor.showExpressionError("无法添加获取：编辑上下文丢失。");
        return;
    }

    const sourceNode = editor.cloneAST(editor.workspaceAST);
    editor.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

    const takeNode = createListTakeNode(sourceNode);
    const placeholder = createValueNode("", "Void", { type: 'empty' });
    takeNode.setCountExpression(placeholder);

    const newEncapsulatedNode = editor.createEncapsulatedFrom(takeNode);
    editor.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);

    const takeNodeInMainAST = newEncapsulatedNode.metadata.innerAST;
    editor.handleEnterEditModeForSlot(takeNodeInMainAST, 'countExpression');
}

export function handleEnterEditModeForSlot(editor, parentNode, slot, index = null) {
    console.log(`DslEditor: Entering edit mode for slot "${slot}"${index !== null ? ` (index ${index})` : ''} of node:`, parentNode);

    // 清除上一次的上下文覆盖
    editor.contextOverride = null;
    editor.slotExpectedType = null;

    editor.clearEditingPath();
    editor.pushEditingPath({ parentNode, slot, descriptor: `编辑 ${slot}`, index });
    const highlightTarget = editor.getHighlightTargetNode ? editor.getHighlightTargetNode() : null;
    editor.selectedNode = highlightTarget?.metadata?.type === 'encapsulated' ? highlightTarget : null;

    // 统一解包逻辑
    let contentToEdit = null;
    if (parentNode.type === 'virtualRoot') {
        contentToEdit = parentNode.main;
        editor.slotExpectedType = editor.normalizeTypeName(editor.expectedType);
    } else if (slot === 'left') {
        contentToEdit = parentNode.leftOperand;
    } else if (slot === 'right') {
        contentToEdit = parentNode.rightOperand;
    } else if (parentNode.type === 'functionCall' && slot === 'arg') {
        const argIndex = typeof index === 'number' ? index : 0;
        contentToEdit = (parentNode.args || [])[argIndex] || null;
        const p = (parentNode.parameters || [])[argIndex];
        if (p?.typeName) {
            editor.slotExpectedType = editor.normalizeTypeName(p.typeName);
        }
    } else if (parentNode.type === 'listQuery' && slot === 'filterExpression') {
        contentToEdit = parentNode.filterExpression;
        editor.slotExpectedType = 'Boolean';
        const sourceReturnType = parentNode.source.returnType;
        const innerType = editor.getCollectionInnerType(sourceReturnType);
        if (innerType) {
            editor.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
            console.log(`DslEditor: Context override set for query. 'self' is now '${innerType}'`);
        }
    } else if (parentNode.type === 'listSort' && slot === 'sortExpression') {
        contentToEdit = parentNode.sortExpression;
        const sourceReturnType = parentNode.source.returnType;
        const innerType = editor.getCollectionInnerType(sourceReturnType);
        if (innerType) {
            editor.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
            console.log(`DslEditor: Context override set for sort. 'self' is now '${innerType}'`);
        }
    } else if (parentNode.type === 'listTake' && slot === 'countExpression') {
        contentToEdit = parentNode.countExpression;
        editor.slotExpectedType = 'Int32';
        // Take 操作的参数通常是数字，不需要上下文覆盖
    }
    
    // 如果槽位有内容，将其解包到工作区
    if (contentToEdit && contentToEdit.metadata?.type === 'encapsulated') {
        editor.workspaceAST = editor.cloneAST(contentToEdit.metadata.innerAST);
    } else {
        editor.workspaceAST = editor.cloneAST(contentToEdit);
    }

    editor.astRoot = editor.workspaceAST;
    editor.isExpressionCompleted = false;

    editor.renderEditor();
    console.log("DslEditor: Workspace is ready for editing the specified slot.");
}
