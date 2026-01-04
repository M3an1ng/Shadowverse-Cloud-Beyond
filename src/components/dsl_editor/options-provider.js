import { findIncompleteNode } from "./core.js";
import { getOptionsForType as getOptionsForTypeFromModule } from "./operators.js";

export function getCurrentEditingOptions(editor) {
    console.log("DslEditor: getCurrentEditingOptions called");
    console.log("DslEditor: astRoot:", editor.astRoot);
    console.log("DslEditor: isExpressionCompleted:", editor.isExpressionCompleted);

    if (!editor.astRoot || (editor.astRoot.type === 'value' && editor.astRoot.metadata?.type === 'empty')) {
        // 空表达式，提供数据源选项
        console.log("DslEditor: No AST root or empty node, returning data source options");
        const opts = getDataSourceOptions(editor);
        // 关键 UX：即使存在 expectedType，也不能在“起点选择”阶段做严格过滤。
        // 例如 expectedType=Boolean 时，玩家需要从上下文对象/事件字段起步，再通过比较/逻辑运算构造布尔表达式。
        return opts;
    }

    // 找到当前需要编辑的位置
    const incompleteNode = findIncompleteNode(editor.astRoot);
    console.log("DslEditor: Found incomplete node:", incompleteNode);
    if (incompleteNode) {
        const options = getOptionsForIncompleteNode(editor, incompleteNode);
        console.log("DslEditor: Returning options for incomplete node:", options);
        return options;
    }

    // 关键修复：基于整个表达式的根节点来决定后续选项
    console.log("DslEditor: Using root node for options:", editor.astRoot);
    console.log("DslEditor: Root node structure:", {
        type: editor.astRoot.type,
        value: editor.astRoot.value,
        returnType: editor.astRoot.returnType,
        metadata: editor.astRoot.metadata,
        expression: editor.astRoot.toExpression?.() || null
    });

    // 使用根节点获取选项，这样可以基于整个表达式的返回类型来决定后续操作
    const options = getOptionsForCompleteNode(editor, editor.astRoot);
    console.log("DslEditor: Returning options for complete expression:", options);
    return options;
}

export function getOptionsForIncompleteNode(editor, node) {
    if (node.type === 'functionCall') {
        // If a function call is incomplete, guide the user to fill arguments.
        const params = node.parameters || [];
        const args = node.args || [];
        const argIndex = params.findIndex((p, i) => !p?.isOptional && !args[i]);
        if (argIndex >= 0) {
            editor.slotExpectedType = editor.normalizeTypeName(params[argIndex]?.typeName) || null;
        }
        return getDataSourceOptions(editor);
    }
    if (node.type === 'operator') {
        if (!node.rightOperand) {
            // 操作符缺少右操作数
            if (editor.isLogicalOperator(node.operator)) {
                // 逻辑操作符需要布尔表达式
                return getBooleanExpressionOptions(editor);
            } else if (['|', '&', '-'].includes(node.operator)) {
                const leftType = node.leftOperand ? editor.calculateASTReturnType(node.leftOperand) : null;
                if (leftType && editor.isCollectionType(leftType)) {
                    editor.slotExpectedType = leftType;
                }
                return getDataSourceOptions(editor);
            } else {
                // 比较操作符需要兼容类型的值
                return getDataSourceOptions(editor);
            }
        }
    }

    if (node.type === 'listQuery') {
        editor.slotExpectedType = 'Boolean';
        if (!editor.contextOverride?.name) {
            const innerType = editor.getCollectionInnerType(node.source?.returnType);
            if (innerType) {
                editor.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
            }
        }
        return getBooleanExpressionOptions(editor);
    }

    if (node.type === 'listSort') {
        if (!editor.contextOverride?.name) {
            const innerType = editor.getCollectionInnerType(node.source?.returnType);
            if (innerType) {
                editor.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
            }
        }
        return getDataSourceOptions(editor);
    }

    if (node.type === 'listTake') {
        editor.slotExpectedType = 'Int32';
        return getDataSourceOptions(editor);
    }

    // 默认提供数据源选项
    return getDataSourceOptions(editor);
}

export function getOptionsForCompleteNode(editor, node) {
    console.log("DslEditor: getOptionsForCompleteNode called with node:", node);

    // 关键修复：基于表达式的最后一个节点来提供选项
    const lastNode = editor.findLastNodeInExpression(node);
    console.log("DslEditor: Found last node:", lastNode);

    if (lastNode && lastNode !== node) {
        // 如果最后一个节点不是根节点，基于最后节点的类型提供选项
        const lastNodeType = editor.getNodeActualReturnType(lastNode);
        console.log("DslEditor: Last node type:", lastNodeType);
        console.log("DslEditor: Providing options based on last node type:", lastNodeType);

        const baseOptions = getOptionsForType(editor, lastNodeType);
        return appendTriggeringEventFieldOptions(editor, baseOptions, lastNode);
    }

    // 回退：检查是否是封装的表达式或操作符节点
    const isEncapsulated = node.metadata && node.metadata.type === "encapsulated";
    const isOperatorNode = node.type === 'operator';
    console.log("DslEditor: Node is encapsulated:", isEncapsulated, "isOperatorNode:", isOperatorNode);

    if (isEncapsulated || isOperatorNode) {
        // 封装的表达式或操作符节点：基于实际返回类型提供适当的操作符
        const actualReturnType = editor.getNodeActualReturnType(node);
        console.log("DslEditor: Encapsulated/Operator expression return type:", actualReturnType);
        console.log("DslEditor: Providing options based on actual return type:", actualReturnType);

        const baseOptions = getOptionsForType(editor, actualReturnType);
        return appendTriggeringEventFieldOptions(editor, baseOptions, node);
    } else {
        // 未封装的表达式：只能继续属性访问或完成表达式
        const actualReturnType = editor.getNodeActualReturnType(node);
        console.log("DslEditor: Non-encapsulated expression return type:", actualReturnType);

        const typeOptions = getOptionsForType(editor, actualReturnType);
        console.log("DslEditor: Returning options for non-encapsulated expression:", typeOptions);
        return appendTriggeringEventFieldOptions(editor, typeOptions, node);
    }
}

export function getOptionsForType(editor, type) {
    console.log("DslEditor: Getting options for type:", type);

    const options = [];

    if (type === 'Int32') {
        // 数值类型：提供数值操作符和比较操作符
        options.push(
            { type: 'operator', operator: '+', label: '加法 (+)', description: '数值相加' },
            { type: 'operator', operator: '-', label: '减法 (-)', description: '数值相减' },
            { type: 'operator', operator: '*', label: '乘法 (*)', description: '数值相乘' },
            { type: 'operator', operator: '/', label: '除法 (/)', description: '数值相除' },
            { type: 'operator', operator: '>', label: '大于 (>)', description: '比较两个数值' },
            { type: 'operator', operator: '<', label: '小于 (<)', description: '比较两个数值' },
            { type: 'operator', operator: '>=', label: '大于等于 (>=)', description: '比较两个数值' },
            { type: 'operator', operator: '<=', label: '小于等于 (<=)', description: '比较两个数值' },
            { type: 'operator', operator: '==', label: '等于 (==)', description: '比较两个值是否相等' },
            { type: 'operator', operator: '!=', label: '不等于 (!=)', description: '比较两个值是否不相等' }
        );
    } else if (type === 'Boolean') {
        // 布尔类型：提供逻辑操作符
        options.push(
            { type: 'operator', operator: '&&', label: '逻辑与 (&&)', description: '两个条件都为真' },
            { type: 'operator', operator: '||', label: '逻辑或 (||)', description: '至少一个条件为真' }
        );
    } else {
        // 其他类型：使用原有逻辑
        const typeOptions = getOptionsForTypeFromModule(editor, type);
        const allowedOptions = typeOptions.filter(option =>
            option.type === 'operator' ||
            option.type === 'function' ||
            option.type === 'query' ||
            option.type === 'listQuery' ||
            option.type === 'property'
        );
        options.push(...allowedOptions);
    }

    console.log("DslEditor: Generated options for type", type, ":", options);
    return options;
}

export function getBooleanExpressionOptions(editor) {
    let options = [];

    console.log("DslEditor: Getting boolean expression options");

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
            label: f.name,
            description: f.description,
            returnType: f.returnType,
            meta: f
        }));
    options.push(...booleanFunctions);

    return options;
}

export function getDataSourceOptions(editor) {
    let options = [];

    console.log("DslEditor: Getting data source options");

    // 1. 上下文对象 (Context Object)
    const contextObjects = getContextObjectsForOptions(editor);
    options.push(...contextObjects.map(obj => ({
        type: "context",
        value: obj.name,
        label: obj.displayName,
        description: obj.description,
        returnType: obj.typeName
    })));

    // 2. 静态值 (Static Value) - 数据源的一种
    options.push({
        type: "staticValue",
        value: "input",
        label: "输入值",
        description: "输入常量值 (数值、文本、布尔值等)"
    });

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

    const timing = (ctx?.timing || 'On').toString();
    const allowOutput = timing.toLowerCase() === 'after';

    const selected = eventTypes[0];
    const ev = (editor.metadata?.events || []).find(e =>
        String(e.eventType ?? e.EventType ?? '').toLowerCase() === String(selected).toLowerCase()
    );
    if (!ev) return [];

    const eventDisplayName = ev.displayName || ev.DisplayName || String(selected);
    const parameters = ev.parameters || ev.Parameters || [];

    const normalizeScope = (s) => String(s || 'both').toLowerCase();
    const scopeAllowed = (scope) => {
        const sc = normalizeScope(scope);
        if (sc === 'both') return true;
        if (sc === 'input') return true;
        if (sc === 'output') return allowOutput;
        return true;
    };

    const normalizeReturnType = (t) => editor.normalizeTypeName(t) || 'Object';
    const mapEventAccessor = (typeName) => {
        const t = normalizeReturnType(typeName);
        if (t === 'Boolean') return 'eventBool';
        if (t === 'String') return 'eventString';
        if (t === 'Guid') return 'eventGuid';
        return 'eventInt';
    };

    return parameters
        .filter(p => scopeAllowed(p.scope ?? p.Scope))
        .map(p => {
            const propertyName = p.propertyName || p.PropertyName || p.name;
            const fieldDisplayName = p.displayName || p.DisplayName || propertyName;
            const typeName = normalizeReturnType(p.typeName || p.TypeName || p.type);
            const scope = normalizeScope(p.scope || p.Scope);
            const accessor = mapEventAccessor(typeName);
            const expression = `${accessor}(\"${propertyName}\")`;
            return {
                type: 'eventField',
                value: expression,
                label: `${eventDisplayName}.${fieldDisplayName}`,
                description: `${p.description || p.Description || ''}（${scope}，${editor.getDisplayTypeName(typeName)}）`,
                returnType: typeName,
                meta: {
                    eventType: selected,
                    propertyName,
                    displayValue: `${eventDisplayName}.${fieldDisplayName}`,
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
