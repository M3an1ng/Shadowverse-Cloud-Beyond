import { findIncompleteNode } from "./core.js";
import { isListOperationNode } from "./pipeline.js";
import { getOptionsForType as getOptionsForTypeFromModule } from "./operators.js";
import { prepareEditorForRecommendations } from "./ast-normalize.js";

function resolveExpectedType(editor) {
    if (!editor) return null;
    if (editor.isLightWorkspace && editor.lightWorkspaceFocus) {
        return editor.normalizeTypeName(editor.slotExpectedType || editor.expectedType);
    }
    const top = typeof editor.peekEditingPath === "function" ? editor.peekEditingPath() : null;
    if (top?.parentNode && typeof editor.getExpectedTypeForSlot === "function") {
        return editor.normalizeTypeName(editor.getExpectedTypeForSlot(top.parentNode, top.slot, top.index));
    }
    return editor.normalizeTypeName(editor.slotExpectedType || editor.expectedType);
}

function resolveQueryableItemType(editor, sourceType) {
    if (!editor || !sourceType) return null;
    const innerType = editor.getCollectionInnerType(sourceType);
    if (innerType) return innerType;
    if (typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(sourceType)) {
        return sourceType;
    }
    return null;
}

export function getCurrentEditingOptions(editor) {

    prepareEditorForRecommendations(editor);

    if (editor.isLightWorkspace && editor.lightWorkspaceFocus?.parentNode) {
        const focusedOptions = getOptionsForLightFocus(editor, editor.lightWorkspaceFocus);
        if (focusedOptions && focusedOptions.length > 0) {
            return focusedOptions;
        }
    }

    if (!editor.workspaceAST || (editor.workspaceAST.type === 'value' && editor.workspaceAST.metadata?.type === 'empty')) {
        // 空表达式，提供数据源选项
        const opts = getDataSourceOptions(editor);
        // 关键 UX：即使存在 expectedType，也不能在“起点选择”阶段做严格过滤。
        // 例如 expectedType=Boolean 时，玩家需要从上下文对象/事件字段起步，再通过比较/逻辑运算构造布尔表达式。
        return opts;
    }

    // 找到当前需要编辑的位置
    const incompleteNode = findIncompleteNode(editor.workspaceAST);
    if (incompleteNode) {
        const options = getOptionsForIncompleteNode(editor, incompleteNode);
        return options;
    }

    // 关键修复：基于整个表达式的根节点来决定后续选项

    // 使用根节点获取选项，这样可以基于整个表达式的返回类型来决定后续操作
    const options = getOptionsForCompleteNode(editor, editor.workspaceAST);
    return options;
}

function getLightFocusSlotNode(focus) {
    if (!focus?.parentNode || !focus.slot) return null;
    const { parentNode, slot, index } = focus;
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

function getOptionsForLightFocus(editor, focus) {
    const slotNode = getLightFocusSlotNode(focus);
    if (!slotNode || (slotNode.type === 'value' && slotNode.metadata?.type === 'empty')) {
        return getOptionsForIncompleteNode(editor, focus.parentNode);
    }
    return getOptionsForCompleteNode(editor, slotNode);
}


export function getOptionsForIncompleteNode(editor, node) {
    if (node.type === 'functionCall') {
        // If a function call is incomplete, guide the user to fill arguments.
        const params = node.parameters || [];
        const args = node.args || [];
        const argIndex = params.findIndex((p, i) => !p?.isOptional && !args[i]);
        if (argIndex >= 0) {
            const expected = typeof editor.getExpectedTypeForSlot === "function"
                ? editor.getExpectedTypeForSlot(node, "arg", argIndex)
                : params[argIndex]?.typeName;
            editor.slotExpectedType = editor.normalizeTypeName(expected) || null;
        }
        return getDataSourceOptions(editor);
    }
    if (node.type === 'operator') {
        if (!node.rightOperand) {
            // 操作符缺少右操作数
            if (editor.isLogicalOperator(node.operator)) {
                // 逻辑操作符需要布尔表达式
                return getBooleanExpressionOptions(editor);
            } else if (['|', '&', '~'].includes(node.operator)) {
                const leftType = node.leftOperand ? editor.calculateASTReturnType(node.leftOperand) : null;
                if (leftType && editor.isCollectionType(leftType)) {
                    editor.slotExpectedType = leftType;
                }
                return getDataSourceOptions(editor);
            } else {
                // 比较操作符需要兼容类型的值
                const leftType = node.leftOperand ? editor.calculateASTReturnType(node.leftOperand) : null;
                if (leftType) {
                    const normalizedLeft = editor.normalizeTypeName(leftType);
                    if (['contains', 'startsWith', 'endsWith'].includes(node.operator)) {
                        if (editor.isCollectionType?.(normalizedLeft)) {
                            const inner = editor.getCollectionInnerType?.(normalizedLeft);
                            editor.slotExpectedType = editor.normalizeTypeName(inner) || normalizedLeft;
                        } else if (editor.isTextType?.(normalizedLeft)) {
                            editor.slotExpectedType = 'String';
                        } else {
                            editor.slotExpectedType = normalizedLeft;
                        }
                    } else {
                        editor.slotExpectedType = normalizedLeft;
                    }
                } else if (['contains', 'startsWith', 'endsWith'].includes(node.operator)) {
                    editor.slotExpectedType = 'String';
                }
                return getDataSourceOptions(editor);
            }
        }
    }

    if (node.type === 'listQuery') {
        editor.slotExpectedType = 'Boolean';
        if (!editor.contextOverride?.name) {
            const sourceType = node.source?.returnType || editor.calculateASTReturnType?.(node.source);
            const itemType = resolveQueryableItemType(editor, sourceType);
            if (itemType) {
                editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
            }
        }
        return getBooleanExpressionOptions(editor);
    }

    if (node.type === 'listSort') {
        if (!editor.contextOverride?.name) {
            const sourceType = node.source?.returnType || editor.calculateASTReturnType?.(node.source);
            const itemType = resolveQueryableItemType(editor, sourceType);
            if (itemType) {
                editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
            }
        }
        return getDataSourceOptions(editor);
    }

    if (node.type === 'listTake') {
        editor.slotExpectedType = 'Int32';
        return getDataSourceOptions(editor);
    }

    if (node.type === 'listDistinct') {
        if (!editor.contextOverride?.name) {
            const sourceType = node.source?.returnType || editor.calculateASTReturnType?.(node.source);
            const itemType = resolveQueryableItemType(editor, sourceType);
            if (itemType) {
                editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
            }
        }
        return getDataSourceOptions(editor);
    }

    if (node.type === 'listSelect') {
        if (!editor.contextOverride?.name) {
            const sourceType = node.source?.returnType || editor.calculateASTReturnType?.(node.source);
            const itemType = resolveQueryableItemType(editor, sourceType);
            if (itemType) {
                editor.contextOverride = { name: 'self', displayName: '当前项', typeName: itemType };
            }
        }
        return getDataSourceOptions(editor);
    }

    // 默认提供数据源选项
    return getDataSourceOptions(editor);
}

export function getOptionsForCompleteNode(editor, node) {

    // 关键修复：基于表达式的最后一个节点来提供选项
    const lastNode = editor.findLastNodeInExpression(node);

    if (lastNode && lastNode !== node) {
        // 如果最后一个节点不是根节点，基于最后节点的类型提供选项
        const lastNodeType = editor.getNodeActualReturnType(lastNode);

        const baseOptions = getOptionsForType(editor, lastNodeType);
        return appendTriggeringEventFieldOptions(editor, baseOptions, lastNode);
    }

    // 回退：检查是否是封装的表达式或操作符节点
    const isEncapsulated = node.metadata && node.metadata.type === "encapsulated";
    const isOperatorNode = node.type === 'operator';

    if (isEncapsulated || isOperatorNode) {
        // 封装的表达式或操作符节点：基于实际返回类型提供适当的操作符
        const actualReturnType = editor.getNodeActualReturnType(node);

        const baseOptions = getOptionsForType(editor, actualReturnType);
        return appendTriggeringEventFieldOptions(editor, baseOptions, node);
    } else {
        // 未封装的表达式：只能继续属性访问或完成表达式
        const actualReturnType = editor.getNodeActualReturnType(node);

        const typeOptions = getOptionsForType(editor, actualReturnType);
        return appendTriggeringEventFieldOptions(editor, typeOptions, node);
    }
}

export function getOptionsForType(editor, type) {

    const typeOptions = getOptionsForTypeFromModule(editor, type);
    const options = typeOptions.filter(option =>
        option.type === 'operator' ||
        option.type === 'function' ||
        option.type === 'query' ||
        option.type === 'listQuery' ||
        option.type === 'listSort' ||
        option.type === 'listTake' ||
        option.type === 'listDistinct' ||
        option.type === 'listSelect' ||
        option.type === 'listPipeline' ||
        option.type === 'property' ||
        option.type === 'propertyInput' ||
        option.type === 'enumValue'
    );

    if (editor.isPipelineInline && editor.isLightWorkspace) {
        return options.filter(option =>
            option.type !== 'listPipeline' && option.type !== 'listQuery'
        );
    }

    const hasPipeline = hasListPipelineInEditor(editor);
    if (hasPipeline) {
        return options.filter(option => option.type !== 'listPipeline');
    }

    return options;
}

function hasListPipelineInEditor(editor) {
    if (!editor) return false;
    const root = editor.virtualRoot?.main || editor.workspaceAST;
    return containsListOperation(root);
}

function containsListOperation(node) {
    if (!node) return false;
    if (node.metadata?.type === 'encapsulated' && node.metadata?.innerAST) {
        return containsListOperation(node.metadata.innerAST);
    }
    if (isListOperationNode(node)) return true;
    if (node.type === 'operator') {
        return containsListOperation(node.leftOperand) || containsListOperation(node.rightOperand);
    }
    if (node.type === 'functionCall') {
        return (node.args || []).some(arg => containsListOperation(arg));
    }
    return false;
}

export function getBooleanExpressionOptions(editor) {
    let options = [];


    // 1. 上下文对象 (Context Object) - 可以构建布尔表达式
    const contextObjects = getContextObjectsForOptions(editor);
    options.push(...contextObjects.map(obj => ({
        type: "context",
        value: obj.name,
        label: obj.displayName,
        description: obj.description,
        returnType: obj.typeName
    })));

    // 2. 静态值 (Static Value) - 特别是布尔值
    options.push({
        type: "staticValue",
        value: "input",
        label: "输入值",
        description: "输入常量值 (数值、文本、布尔值等)"
    });

    // 2.5 触发事件字段不再作为全局选项暴露

    // 3. 布尔函数 (返回布尔值的函数)
    const functions = editor.metadata?.dsl?.functions || [];
    const booleanFunctions = functions
        .filter(f => f.returnType === 'Boolean')
        .map(f => ({
            type: "function",
            value: f.name,
            label: typeof editor.getReadableFunctionName === "function" ? editor.getReadableFunctionName(f.name) : f.name,
            description: f.description,
            returnType: f.returnType,
            meta: f
        }));
    options.push(...booleanFunctions);

    return options;
}

export function getDataSourceOptions(editor) {
    let options = [];


    // 1. 上下文对象 (Context Object)
    const contextObjects = getContextObjectsForOptions(editor);
    options.push(...contextObjects.map(obj => ({
        type: "context",
        value: obj.name,
        label: obj.displayName,
        description: obj.description,
        returnType: obj.typeName
    })));

    const expectedType = resolveExpectedType(editor);
    const enumOptions = expectedType && typeof editor.getEnumValueOptions === "function"
        && typeof editor.isEnumType === "function" && editor.isEnumType(expectedType)
        ? editor.getEnumValueOptions(expectedType)
        : [];
    const hideStaticValue = expectedType
        && typeof editor.isEnumType === "function"
        && editor.isEnumType(expectedType)
        && enumOptions.length > 0;

    if (enumOptions.length > 0) {
        options.push(...enumOptions);
    }

    if (!hideStaticValue) {
        // 2. 静态值 (Static Value) - 数据源的一种
        options.push({
            type: "staticValue",
            value: "input",
            label: "输入值",
            description: "输入常量值 (数值、文本、布尔值等)"
        });
    }

    // 3. 触发事件字段不再作为全局选项暴露

    return options;
}

export function getContextObjectsForOptions(editor) {
    const contextObjects = editor.metadata?.dsl?.contextObjects || [];
    if (!editor.contextOverride?.name) {
        return contextObjects;
    }

    const override = {
        name: editor.contextOverride.name,
        displayName: editor.contextOverride.displayName || editor.contextOverride.name,
        description: editor.contextOverride.description || editor.contextOverride.displayName || editor.contextOverride.name,
        typeName: editor.contextOverride.typeName
    };

    const overrideName = String(override.name || '').toLowerCase();
    const filtered = contextObjects.filter(obj => String(obj.name || '').toLowerCase() !== overrideName);
    return [override, ...filtered];
}

export function getTriggeringEventFieldOptions(editor) {
    const ctx = editor.triggeringEventContext;
    const eventTypes = ctx?.eventTypes || [];
    if (!Array.isArray(eventTypes) || eventTypes.length === 0) return [];
    if (eventTypes.length !== 1) {
        return editor.getObjectProperties?.("GameEventBase") || [];
    }

    const timing = (ctx?.timing || 'On').toString();
    const allowOutput = timing.toLowerCase() === 'after';

    const selected = eventTypes[0];
    const ev = (editor.metadata?.events || []).find(e =>
        String(e.eventType ?? e.EventType ?? '').toLowerCase() === String(selected).toLowerCase()
    );
    if (!ev) return [];

    const parameters = ev.parameters || ev.Parameters || [];

    const normalizeScope = (s) => String(s || 'both').toLowerCase();
    const scopeAllowed = (scope) => {
        const sc = normalizeScope(scope);
        if (sc === 'both') return true;
        if (sc === 'input') return true;
        if (sc === 'output') return allowOutput;
        return true;
    };

    return parameters
        .filter(p => scopeAllowed(p.scope ?? p.Scope))
        .map(p => {
            const propertyName = p.propertyName || p.PropertyName || p.name;
            const fieldDisplayName = p.displayName || p.DisplayName || propertyName;
            const typeName = editor.normalizeTypeName(p.typeName || p.TypeName || p.type) || 'Object';
            const typeOverride = p.dslDisplayTypeName || p.DslDisplayTypeName;
            const displayType = typeOverride
                ? (editor.isCollectionType(typeName) ? `${typeOverride}列表` : typeOverride)
                : editor.getDisplayTypeName(typeName);
            const scope = normalizeScope(p.scope || p.Scope);
            return {
                type: 'property',
                value: propertyName,
                label: fieldDisplayName,
                description: `${p.description || p.Description || ''}（${scope}，${displayType}）`,
                returnType: typeName,
                meta: {
                    eventType: selected,
                    propertyName,
                    scope
                }
            };
        });
}

function appendTriggeringEventFieldOptions(editor, options, node) {
    if (!isTriggeringEventContextNode(node)) return options;
    const eventFieldOptions = getTriggeringEventFieldOptions(editor);
    if (!eventFieldOptions.length) return options;
    return [...eventFieldOptions, ...options];
}

function isTriggeringEventContextNode(node) {
    if (!node) return false;
    if (node.type === 'value' && node.metadata?.type === 'encapsulated') {
        return isTriggeringEventContextNode(node.metadata?.innerAST);
    }
    if (node.type !== 'value') return false;
    const path = String(node.metadata?.fullPath || node.value || '');
    return path.toLowerCase() === 'context.triggeringevent';
}
