import {
    createValueNode,
    createOperatorNode,
    createFunctionCallNode,
    createListQueryNode,
    createListSortNode,
    createListTakeNode,
    createListDistinctNode,
    createListSelectNode,
    validateAST,
    findIncompleteNode,
    findRightmostLeafNode
} from "./core.js";
import { finalizeDraftInputs } from "./ast-normalize.js";

function createEnumValueNode(editor, option) {
    const enumType = editor.normalizeTypeName(
        option.enumType || option.returnType || editor.slotExpectedType || editor.expectedType
    );
    const rawValue = option.value ?? option.enumValue ?? "";
    const canonicalValue = enumType && typeof editor.resolveEnumValueFromInput === "function"
        ? editor.resolveEnumValueFromInput(enumType, rawValue)
        : rawValue;
    const localizedLabel = enumType && typeof editor.getEnumLabelForValue === "function"
        ? editor.getEnumLabelForValue(enumType, canonicalValue)
        : null;
    const displayValue = option.displayValue || option.label || localizedLabel || canonicalValue;
    const formatted = typeof editor.formatEnumLiteralValue === "function"
        ? editor.formatEnumLiteralValue(canonicalValue)
        : JSON.stringify(String(canonicalValue));

    return createValueNode(formatted, enumType || "String", {
        type: "enumValue",
        enumType: enumType || null,
        enumValue: canonicalValue,
        displayValue
    });
}

function createPropertyInputNode(basePath, option) {
    const normalizedBase = String(basePath || "").trim();
    const returnType = option.returnType || "Object";
    return createValueNode(normalizedBase, returnType, {
        type: "propertyInput",
        basePath: normalizedBase,
        key: "",
        isAwaitingInput: true,
        returnType
    });
}

export function performASTGoBack(editor) {
    if (!editor.astRoot) {
        return { success: false, newRoot: null };
    }

    // 如果是单个值节点
    if (editor.astRoot.type === 'value') {
        // 如果是封装的表达式，解封装回到内部AST
        if (editor.astRoot.metadata && editor.astRoot.metadata.type === "encapsulated" && editor.astRoot.metadata.innerAST) {
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

    // 如果是函数调用节点
    if (editor.astRoot.type === 'functionCall') {
        const args = editor.astRoot.args || [];
        let lastIndex = -1;
        for (let i = args.length - 1; i >= 0; i--) {
            if (args[i]) {
                lastIndex = i;
                break;
            }
        }

        if (lastIndex < 0) {
            return { success: true, newRoot: null };
        }

        const hasOtherArgs = args.some((arg, idx) => arg && idx !== lastIndex);
        if (!hasOtherArgs) {
            const onlyArg = args[lastIndex];
            if (onlyArg) {
                onlyArg.parent = null;
            }
            return { success: true, newRoot: onlyArg || null };
        }

        const removedArg = args[lastIndex];
        if (removedArg) {
            removedArg.parent = null;
        }
        editor.astRoot.setArg(lastIndex, null);
        return { success: true, newRoot: editor.astRoot };
    }

    return { success: false, newRoot: editor.astRoot };
}

export function saveCurrentLayer(editor) {
    const top = editor.peekEditingPath();
    if (!top) {
        console.warn('DslEditor: saveCurrentLayer called with empty editingPath');
        return;
    }
    if (!editor.workspaceAST) {
        console.warn("DslEditor: saveCurrentLayer called with empty workspaceAST.");
        return;
    }

    // 先提交静态值草稿，避免因未 blur 导致被判定为不完整
    finalizeDraftInputs(editor, editor.workspaceAST);

    if (findIncompleteNode(editor.workspaceAST)) {
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
    if (parentNode.type === 'virtualRoot' && slot === 'main') {
        parentNode.main = valueToWrite;
        ok = true;
    } else if (parentNode.type === 'functionCall' && slot === 'arg') {
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
    } else if (option.type === "enumValue") {
        editor.astRoot = createEnumValueNode(editor, option);
    } else if (option.type === "staticValue") {
        // 创建静态值节点，等待用户输入
        editor.astRoot = createValueNode("[输入值]", "Object", {
            type: "staticValue",
            isAwaitingInput: true,
            staticValue: ""
        });
        editor.currentNode = editor.astRoot;
    } else if (option.type === "propertyInput") {
        editor.astRoot = createPropertyInputNode(option.basePath || "", option);
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

    // 触发状态更新
    editor.triggerStateUpdate();
}

function getSlotNode(parentNode, slot, index) {
    if (!parentNode || !slot) return null;
    if (parentNode.type === 'virtualRoot' && slot === 'main') return parentNode.main;
    if (parentNode.type === 'operator') {
        return slot === 'left' ? parentNode.leftOperand : slot === 'right' ? parentNode.rightOperand : null;
    }
    if (parentNode.type === 'functionCall' && slot === 'arg') {
        const argIndex = typeof index === 'number' ? index : 0;
        return (parentNode.args || [])[argIndex] || null;
    }
    if (parentNode.type === 'listQuery' && slot === 'filterExpression') return parentNode.filterExpression;
    if (parentNode.type === 'listSort' && slot === 'sortExpression') return parentNode.sortExpression;
    if (parentNode.type === 'listTake' && slot === 'countExpression') return parentNode.countExpression;
    if (parentNode.type === 'listDistinct' && slot === 'keyExpression') return parentNode.keyExpression;
    if (parentNode.type === 'listSelect' && slot === 'selectExpression') return parentNode.selectExpression;
    return null;
}

function setSlotNode(parentNode, slot, index, node) {
    if (!parentNode || !slot) return false;
    if (parentNode.type === 'virtualRoot' && slot === 'main') {
        parentNode.main = node;
        return true;
    }
    if (parentNode.type === 'operator') {
        if (slot === 'left') {
            parentNode.setLeftOperand(node);
            return true;
        }
        if (slot === 'right') {
            parentNode.setRightOperand(node);
            return true;
        }
    }
    if (parentNode.type === 'functionCall' && slot === 'arg') {
        const argIndex = typeof index === 'number' ? index : 0;
        if (typeof parentNode.setArg === 'function') {
            parentNode.setArg(argIndex, node);
            return true;
        }
    }
    if (parentNode.type === 'listQuery' && slot === 'filterExpression') {
        parentNode.setFilterExpression(node);
        return true;
    }
    if (parentNode.type === 'listSort' && slot === 'sortExpression') {
        parentNode.setSortExpression(node);
        return true;
    }
    if (parentNode.type === 'listTake' && slot === 'countExpression') {
        parentNode.setCountExpression(node);
        return true;
    }
    if (parentNode.type === 'listDistinct' && slot === 'keyExpression') {
        parentNode.setKeyExpression(node);
        return true;
    }
    if (parentNode.type === 'listSelect' && slot === 'selectExpression') {
        parentNode.setSelectExpression(node);
        return true;
    }
    return false;
}

function isEmptySlotNode(node) {
    return !node || (node.type === 'value' && node.metadata?.type === 'empty');
}

function applyOptionToFocusedSlot(editor, option) {
    const focus = editor.lightWorkspaceFocus;
    if (!focus?.parentNode || !focus.slot) return false;
    const { parentNode, slot, index } = focus;
    const slotNode = getSlotNode(parentNode, slot, index);
    const isEmpty = isEmptySlotNode(slotNode);

    const setFocus = (targetNode, targetSlot, targetIndex = null) => {
        if (typeof editor.setLightWorkspaceFocus === 'function') {
            editor.setLightWorkspaceFocus(targetNode, targetSlot, targetIndex);
        }
    };

    const setSlotAndKeepFocus = (node) => {
        if (!setSlotNode(parentNode, slot, index, node)) return false;
        setFocus(parentNode, slot, index);
        return true;
    };

    if (option.type === "operator") {
        const operator = option.operator || option.value;
        const newOperatorNode = createOperatorNode(operator);
        let leftOperand = null;
        if (isEmpty) {
            leftOperand = createValueNode("", "Void", { type: "empty" });
        } else {
            const rawLeftOperand = editor.cloneAST(slotNode);
            leftOperand = rawLeftOperand?.metadata?.type === 'encapsulated'
                ? rawLeftOperand
                : editor.createEncapsulatedFrom(rawLeftOperand);
        }
        newOperatorNode.setLeftOperand(leftOperand);
        newOperatorNode.setRightOperand(null);
        if (!setSlotNode(parentNode, slot, index, newOperatorNode)) return false;
        setFocus(newOperatorNode, 'right');
        return true;
    }

    if (option.type === "listQuery") {
        if (isEmpty) {
            editor.showExpressionError("无法添加查询：缺少源集合。");
            return true;
        }
        const sourceNode = editor.cloneAST(slotNode);
        const queryNode = createListQueryNode(sourceNode);
        queryNode.setFilterExpression(createValueNode("", "Void", { type: "empty" }));
        if (!setSlotNode(parentNode, slot, index, queryNode)) return false;
        setFocus(queryNode, 'filterExpression');
        return true;
    }

    if (option.type === "listSort") {
        if (isEmpty) {
            editor.showExpressionError("无法添加排序：缺少源集合。");
            return true;
        }
        const sourceNode = editor.cloneAST(slotNode);
        const sortNode = createListSortNode(sourceNode);
        sortNode.setSortExpression(createValueNode("", "Void", { type: "empty" }));
        if (!setSlotNode(parentNode, slot, index, sortNode)) return false;
        setFocus(sortNode, 'sortExpression');
        return true;
    }

    if (option.type === "listTake") {
        if (isEmpty) {
            editor.showExpressionError("无法添加获取：缺少源集合。");
            return true;
        }
        const sourceNode = editor.cloneAST(slotNode);
        const takeNode = createListTakeNode(sourceNode);
        takeNode.setCountExpression(createValueNode("", "Void", { type: "empty" }));
        if (!setSlotNode(parentNode, slot, index, takeNode)) return false;
        setFocus(takeNode, 'countExpression');
        return true;
    }

    if (option.type === "listDistinct") {
        if (isEmpty) {
            editor.showExpressionError("无法添加去重：缺少源集合。");
            return true;
        }
        const sourceNode = editor.cloneAST(slotNode);
        const distinctNode = createListDistinctNode(sourceNode);
        distinctNode.setKeyExpression(createValueNode("", "Void", { type: "empty" }));
        if (!setSlotNode(parentNode, slot, index, distinctNode)) return false;
        setFocus(distinctNode, 'keyExpression');
        return true;
    }

    if (option.type === "listSelect") {
        if (isEmpty) {
            editor.showExpressionError("无法添加选择：缺少源集合。");
            return true;
        }
        const sourceNode = editor.cloneAST(slotNode);
        const selectNode = createListSelectNode(sourceNode);
        selectNode.setSelectExpression(createValueNode("", "Void", { type: "empty" }));
        if (!setSlotNode(parentNode, slot, index, selectNode)) return false;
        setFocus(selectNode, 'selectExpression');
        return true;
    }

    if (option.type === "function") {
        const signature = option.meta || {};
        const parameters = signature.parameters || signature.Parameters || [];
        const returnType = option.returnType || signature.returnType || signature.returnTypeName || 'Object';
        const functionNode = createFunctionCallNode(option.value, returnType, parameters);
        if (!isEmpty && parameters.length > 0) {
            functionNode.setArg(0, editor.cloneAST(slotNode));
        }
        if (!setSlotNode(parentNode, slot, index, functionNode)) return false;
        const nextIndex = parameters.findIndex((p, i) => !p?.isOptional && !functionNode.args[i]);
        if (nextIndex >= 0) {
            setFocus(functionNode, 'arg', nextIndex);
        } else {
            setFocus(parentNode, slot, index);
        }
        return true;
    }

    if (option.type === "property") {
        if (isEmpty || !slotNode || slotNode.type !== 'value') {
            editor.showExpressionError("无法添加属性：缺少有效的起点。");
            return true;
        }
        if (slotNode.metadata?.type === 'context' || slotNode.metadata?.type === 'property') {
            const newPath = `${slotNode.value}.${option.value}`;
            const newNode = createValueNode(newPath, option.returnType, {
                type: "property",
                fullPath: newPath,
                isNested: slotNode.metadata?.type === 'property',
                displayName: option.label
            });
            return setSlotAndKeepFocus(newNode);
        }
        editor.showExpressionError("无法添加属性：当前节点不支持继续扩展。");
        return true;
    }
    if (option.type === "propertyInput") {
        if (isEmpty || !slotNode || slotNode.type !== 'value') {
            editor.showExpressionError("无法添加键名输入：缺少有效的起点。");
            return true;
        }
        if (slotNode.metadata?.type === 'context' || slotNode.metadata?.type === 'property') {
            const newNode = createPropertyInputNode(slotNode.value, option);
            return setSlotAndKeepFocus(newNode);
        }
        editor.showExpressionError("无法添加键名输入：当前节点不支持继续扩展。");
        return true;
    }

    if (option.type === "context") {
        return setSlotAndKeepFocus(createValueNode(option.value, option.returnType, {
            type: "context",
            name: option.value,
            displayName: option.label
        }));
    }

    if (option.type === "enumValue") {
        return setSlotAndKeepFocus(createEnumValueNode(editor, option));
    }

    if (option.type === "staticValue") {
        return setSlotAndKeepFocus(createValueNode("[输入值]", "Object", {
            type: "staticValue",
            isAwaitingInput: true,
            staticValue: ""
        }));
    }
    if (option.type === "propertyInput") {
        return setSlotAndKeepFocus(createPropertyInputNode("", option));
    }

    if (option.type === "eventField") {
        return setSlotAndKeepFocus(createValueNode(option.value, option.returnType, {
            type: "eventField",
            displayValue: option.meta?.displayValue || option.label,
            eventType: option.meta?.eventType,
            propertyName: option.meta?.propertyName,
            scope: option.meta?.scope
        }));
    }

    return false;
}

export function extendAST(editor, option) {
    // 新逻辑：优先处理基于选区的操作
    // 统一逻辑：所有操作符的添加都基于当前工作区内容

    if (editor.isLightWorkspace && editor.lightWorkspaceFocus) {
        const applied = applyOptionToFocusedSlot(editor, option);
        if (applied) {
            return;
        }
    }

    // 关键修复：如果当前不在编辑模式（没有路径），但工作区有内容，则自动进入扩展主表达式模式
    if (!editor.isLightWorkspace && editor.editingPath.length === 0 && editor.workspaceAST && validateAST(editor.workspaceAST).isValid) {
        editor.expandMainExpression();
        return;
    }

    const incompleteNode = findIncompleteNode(editor.astRoot);

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
    } else if (option.type === "propertyInput") {
        const rightmostNode = findRightmostLeafNode(editor.astRoot);
        if (!rightmostNode || rightmostNode.type !== 'value') {
            editor.showExpressionError("无法添加键名输入：缺少有效的字典路径。");
            return;
        }
        if (rightmostNode.metadata?.type === 'context' || rightmostNode.metadata?.type === 'property') {
            const newNode = createPropertyInputNode(rightmostNode.value, option);
            editor.replaceNodeInAST(rightmostNode, newNode);
            if (editor.astRoot === rightmostNode) {
                editor.astRoot = newNode;
                editor.workspaceAST = newNode;
            }
            editor.currentNode = newNode;
            return;
        }
        editor.showExpressionError("无法添加键名输入：当前节点不支持继续扩展。");
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
    } else if (option.type === "enumValue") {
        const newNode = createEnumValueNode(editor, option);
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
    } else if (option.type === "listDistinct") {
        createDistinctNodeFromCurrent(editor, option);
    } else if (option.type === "listSelect") {
        createSelectNodeFromCurrent(editor, option);
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

    // If there are more required arguments, guide the user into editing the next slot.
    const nextIndex = parameters.findIndex((p, i) => !p?.isOptional && !functionNode.args[i]);

    if (editor.isLightWorkspace) {
        editor.workspaceAST = functionNode;
        editor.astRoot = functionNode;
        if (nextIndex >= 0 && typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(functionNode, 'arg', nextIndex);
        }
        return;
    }

    const top = editor.peekEditingPath ? editor.peekEditingPath() : null;
    if (top?.parentNode) {
        const encapsulated = editor.createEncapsulatedFrom(functionNode);
        editor.writeBackToParentSlot(top.parentNode, top.slot, encapsulated);
        const inner = encapsulated.metadata?.innerAST || functionNode;
        if (nextIndex >= 0) {
            editor.handleEnterEditModeForSlot(inner, 'arg', nextIndex, { preservePath: true });
        } else {
            editor.ascend(top.parentNode);
        }
        return;
    }

    const encapsulated = editor.createEncapsulatedFrom(functionNode);
    editor.virtualRoot.main = encapsulated;
    const inner = encapsulated.metadata?.innerAST || functionNode;
    if (nextIndex >= 0) {
        editor.handleEnterEditModeForSlot(inner, 'arg', nextIndex);
    } else {
        editor.ascend(editor.virtualRoot);
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
        if (opt.type === 'propertyInput') {
            return createPropertyInputNode(opt.basePath || "", opt);
        }
        if (opt.type === 'enumValue') {
            return createEnumValueNode(editor, opt);
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

    // 在工作区创建右操作数节点
    if (option.type === "context") {
        editor.astRoot = createValueNode(option.value, option.returnType, {
            type: "context",
            name: option.value,
            displayName: option.label
        });
    } else if (option.type === "enumValue") {
        editor.astRoot = createEnumValueNode(editor, option);
    } else if (option.type === "staticValue") {
        editor.astRoot = createValueNode("[输入值]", "Object", {
            type: "staticValue",
            isAwaitingInput: true,
            staticValue: ""
        });
        editor.currentNode = editor.astRoot;
    } else if (option.type === "propertyInput") {
        editor.astRoot = createPropertyInputNode(option.basePath || "", option);
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
}

export function addRightOperandToOperator(editor, operatorNode, option) {
    let rightOperand;

    if (option.type === "context") {
        rightOperand = createValueNode(option.value, option.returnType, {
            type: "context",
            name: option.value,
            displayName: option.label
        });
    } else if (option.type === "enumValue") {
        rightOperand = createEnumValueNode(editor, option);
    } else if (option.type === "staticValue") {
        rightOperand = createValueNode("[输入值]", "Object", {
            type: "staticValue",
            isAwaitingInput: true,
            staticValue: ""
        });
        editor.currentNode = rightOperand;
    } else if (option.type === "propertyInput") {
        rightOperand = createPropertyInputNode(option.basePath || "", option);
        editor.currentNode = rightOperand;
    } else if (option.type === "function") {
        const signature = option.meta || {};
        const parameters = signature.parameters || signature.Parameters || [];
        rightOperand = createFunctionCallNode(
            option.value,
            option.returnType || signature.returnType || signature.returnTypeName || 'Object',
            parameters
        );
    }

    if (rightOperand) {
        operatorNode.setRightOperand(rightOperand);

        // 简化：操作符作为普通AST节点在工作区正常处理
        // 工作区继续指向整个操作符节点，而不是切换到右操作数
        editor.workspaceAST = operatorNode;
        editor.astRoot = operatorNode;
        if (editor.isLightWorkspace && typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(operatorNode, 'right');
        }

    }
}

export function createOperatorFromCurrent(editor, option) {
    const operator = option.operator || option.value;
    if (!editor.workspaceAST) {
        console.error("DslEditor: Cannot create operator, workspace is empty.");
        return;
    }

    if (editor.isLightWorkspace) {
        const rawLeftOperand = editor.cloneAST(editor.workspaceAST);
        const leftOperandNode = rawLeftOperand?.metadata?.type === 'encapsulated'
            ? rawLeftOperand
            : editor.createEncapsulatedFrom(rawLeftOperand);
        const newOperatorNode = createOperatorNode(operator);
        newOperatorNode.setLeftOperand(leftOperandNode);
        newOperatorNode.setRightOperand(null);
        editor.workspaceAST = newOperatorNode;
        editor.astRoot = newOperatorNode;
        if (typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(newOperatorNode, 'right');
        }
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

    // 步骤 B: 生成操作符和相应的占位符
    const rightOperandPlaceholder = createValueNode("", "Void", { type: 'empty' });
    const newOperatorNode = createOperatorNode(operator);
    newOperatorNode.setLeftOperand(leftOperandNode); // 使用封装后的左操作数保持一致显示
    newOperatorNode.setRightOperand(rightOperandPlaceholder);

    // 步骤 C: 检测并创建外层封装
    const newEncapsulatedNode = editor.createEncapsulatedFrom(newOperatorNode);

    // 步骤 D: 更新主表达式，并将工作区切换到新加载的空白表达式
    // 再次写入同一个槽位，用包含操作符的新封装节点覆盖掉旧的
    editor.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);

    // 从新的主AST节点中获取内部操作符节点的正确引用
    const operatorNodeInMainAST = newEncapsulatedNode.metadata.innerAST;

    // 进入新操作符右侧占位符的编辑模式
    // 保留编辑路径栈，避免覆盖外层上下文
    editor.handleEnterEditModeForSlot(operatorNodeInMainAST, 'right', null, { preservePath: true });
}

export function createQueryNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    if (editor.isLightWorkspace) {
        const sourceNode = editor.cloneAST(editor.workspaceAST);
        const queryNode = createListQueryNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        queryNode.setFilterExpression(placeholder);
        editor.workspaceAST = queryNode;
        editor.astRoot = queryNode;
        if (typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(queryNode, 'filterExpression');
        }
        return;
    }

    const top = editor.peekEditingPath();
    if (!top || !top.parentNode) {
        editor.showExpressionError("无法添加查询：编辑上下文丢失。");
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
    // 保留编辑路径栈，避免覆盖外层上下文
    editor.handleEnterEditModeForSlot(queryNodeInMainAST, 'filterExpression', null, { preservePath: true });
}

export function createSortNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    if (editor.isLightWorkspace) {
        const sourceNode = editor.cloneAST(editor.workspaceAST);
        const sortNode = createListSortNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        sortNode.setSortExpression(placeholder);
        editor.workspaceAST = sortNode;
        editor.astRoot = sortNode;
        if (typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(sortNode, 'sortExpression');
        }
        return;
    }

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
    // 保留编辑路径栈，避免覆盖外层上下文
    editor.handleEnterEditModeForSlot(sortNodeInMainAST, 'sortExpression', null, { preservePath: true });
}

export function createTakeNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    if (editor.isLightWorkspace) {
        const sourceNode = editor.cloneAST(editor.workspaceAST);
        const takeNode = createListTakeNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        takeNode.setCountExpression(placeholder);
        editor.workspaceAST = takeNode;
        editor.astRoot = takeNode;
        if (typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(takeNode, 'countExpression');
        }
        return;
    }

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
    // 保留编辑路径栈，避免覆盖外层上下文
    editor.handleEnterEditModeForSlot(takeNodeInMainAST, 'countExpression', null, { preservePath: true });
}

export function createDistinctNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    if (editor.isLightWorkspace) {
        const sourceNode = editor.cloneAST(editor.workspaceAST);
        const distinctNode = createListDistinctNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        distinctNode.setKeyExpression(placeholder);
        editor.workspaceAST = distinctNode;
        editor.astRoot = distinctNode;
        if (typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(distinctNode, 'keyExpression');
        }
        return;
    }

    const top = editor.peekEditingPath();
    if (!top || !top.parentNode) {
        editor.showExpressionError("无法添加去重：编辑上下文丢失。");
        return;
    }

    const sourceNode = editor.cloneAST(editor.workspaceAST);
    editor.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

    const distinctNode = createListDistinctNode(sourceNode);
    const placeholder = createValueNode("", "Void", { type: 'empty' });
    distinctNode.setKeyExpression(placeholder);

    const newEncapsulatedNode = editor.createEncapsulatedFrom(distinctNode);
    editor.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);

    const distinctNodeInMainAST = newEncapsulatedNode.metadata.innerAST;
    // 保留编辑路径栈，避免覆盖外层上下文
    editor.handleEnterEditModeForSlot(distinctNodeInMainAST, 'keyExpression', null, { preservePath: true });
}

export function createSelectNodeFromCurrent(editor, option) {
    if (!editor.workspaceAST) return;

    if (editor.isLightWorkspace) {
        const sourceNode = editor.cloneAST(editor.workspaceAST);
        const selectNode = createListSelectNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        selectNode.setSelectExpression(placeholder);
        editor.workspaceAST = selectNode;
        editor.astRoot = selectNode;
        if (typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(selectNode, 'selectExpression');
        }
        return;
    }

    const top = editor.peekEditingPath();
    if (!top || !top.parentNode) {
        editor.showExpressionError("无法添加选择：编辑上下文丢失。");
        return;
    }

    const sourceNode = editor.cloneAST(editor.workspaceAST);
    editor.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

    const selectNode = createListSelectNode(sourceNode);
    const placeholder = createValueNode("", "Void", { type: 'empty' });
    selectNode.setSelectExpression(placeholder);

    const newEncapsulatedNode = editor.createEncapsulatedFrom(selectNode);
    editor.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);

    const selectNodeInMainAST = newEncapsulatedNode.metadata.innerAST;
    // 保留编辑路径栈，避免覆盖外层上下文
    editor.handleEnterEditModeForSlot(selectNodeInMainAST, 'selectExpression', null, { preservePath: true });
}


export function handleEnterEditModeForSlot(editor, parentNode, slot, index = null, options = {}) {

    // 清除上一次的上下文覆盖
    const preserveContextOverride = editor.isPipelineInline && editor.contextOverride?.name;
    if (!preserveContextOverride) {
        editor.contextOverride = null;
    }
    editor.slotExpectedType = null;

    const preservePath = options?.preservePath === true;
    if (!preservePath) {
        editor.clearEditingPath();
    }
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
        const itemType = editor.getCollectionInnerType(sourceReturnType)
            || (typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(sourceReturnType)
                ? sourceReturnType
                : null);
        if (itemType) {
            editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
        }
    } else if (parentNode.type === 'listSort' && slot === 'sortExpression') {
        contentToEdit = parentNode.sortExpression;
        const sourceReturnType = parentNode.source.returnType;
        const itemType = editor.getCollectionInnerType(sourceReturnType)
            || (typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(sourceReturnType)
                ? sourceReturnType
                : null);
        if (itemType) {
            editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
        }
    } else if (parentNode.type === 'listTake' && slot === 'countExpression') {
        contentToEdit = parentNode.countExpression;
        editor.slotExpectedType = 'Int32';
        // Take 操作的参数通常是数字，不需要上下文覆盖
    } else if (parentNode.type === 'listDistinct' && slot === 'keyExpression') {
        contentToEdit = parentNode.keyExpression;
        const sourceReturnType = parentNode.source.returnType;
        const itemType = editor.getCollectionInnerType(sourceReturnType)
            || (typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(sourceReturnType)
                ? sourceReturnType
                : null);
        if (itemType) {
            editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
        }
    } else if (parentNode.type === 'listSelect' && slot === 'selectExpression') {
        contentToEdit = parentNode.selectExpression;
        const sourceReturnType = parentNode.source.returnType;
        const itemType = editor.getCollectionInnerType(sourceReturnType)
            || (typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(sourceReturnType)
                ? sourceReturnType
                : null);
        if (itemType) {
            editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
        }
    }

    if (parentNode.type === 'operator' && (slot === 'left' || slot === 'right')) {
        const expected = editor.getExpectedTypeForSlot(parentNode, slot, index);
        if (expected) {
            editor.slotExpectedType = editor.normalizeTypeName(expected);
        }
    }

    const isLightSlot = editor.isLightWorkspace && !(parentNode.type === 'virtualRoot' && slot === 'main');
    if (isLightSlot && editor.workspaceAST) {
        editor.astRoot = editor.workspaceAST;
        editor.isExpressionCompleted = false;
        editor.renderEditor();
        return;
    }

    // 如果槽位有内容，将其解包到工作区
    if (contentToEdit && contentToEdit.metadata?.type === 'encapsulated') {
        editor.workspaceAST = editor.cloneAST(contentToEdit.metadata.innerAST);
    } else {
        editor.workspaceAST = editor.cloneAST(contentToEdit);
    }

    editor.astRoot = editor.workspaceAST;
    editor.isExpressionCompleted = false;

    if (typeof editor.syncPipelineContext === "function") {
        editor.syncPipelineContext();
    }
    editor.renderEditor();
}
