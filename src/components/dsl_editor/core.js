import { stringifyAstNode } from "./ast-stringify.js";

// Phase C - 核心 AST 类与工具函数迁移

function formatTypeName(typeName) {
    if (!typeName) return '未知类型';
    const trimmed = String(typeName).trim();
    if (!trimmed) return '未知类型';

    const match = trimmed.match(/^(List|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection)<(.+)>$/);
    if (match) {
        return `${formatTypeName(match[2])}列表`;
    }

    const map = {
        Int32: '整数',
        Int64: '整数',
        Number: '数字',
        Single: '数字',
        Double: '数字',
        Decimal: '数字',
        Boolean: '布尔',
        String: '文本',
        Guid: 'ID',
        DateTime: '时间',
        CardInstance: '卡牌',
        PlayerState: '玩家',
        TargetInfo: '目标',
        GameState: '对局状态',
        IAbility: '能力',
        Ability: '能力',
        Effect: '效果',
        IGameEvent: '事件'
    };

    return map[trimmed] || trimmed;
}

function splitGenericTypeArguments(raw) {
    const text = String(raw || "").trim();
    if (!text) return [];
    const result = [];
    let depth = 0;
    let current = "";
    for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (ch === "<") {
            depth += 1;
            current += ch;
            continue;
        }
        if (ch === ">") {
            depth = Math.max(0, depth - 1);
            current += ch;
            continue;
        }
        if (ch === "," && depth === 0) {
            const token = current.trim();
            if (token) result.push(token);
            current = "";
            continue;
        }
        current += ch;
    }
    const tail = current.trim();
    if (tail) result.push(tail);
    return result;
}

export function normalizeSemanticTypeName(typeName) {
    if (!typeName) return null;
    const trimmed = String(typeName).trim().replace(/^global::/i, "");
    if (!trimmed) return null;

    const aliasMap = {
        bool: "Boolean",
        boolean: "Boolean",
        "system.boolean": "Boolean",
        string: "String",
        "system.string": "String",
        object: "Object",
        "system.object": "Object",
        guid: "Guid",
        "system.guid": "Guid",
        int: "Int32",
        int32: "Int32",
        "system.int32": "Int32",
        long: "Int64",
        int64: "Int64",
        "system.int64": "Int64",
        float: "Single",
        single: "Single",
        "system.single": "Single",
        double: "Double",
        "system.double": "Double",
        decimal: "Decimal",
        "system.decimal": "Decimal",
        number: "Number",
        cardinstancesnapshot: "CardInstance",
        cardinstanceresetsnapshot: "CardInstance",
        carddefinitionsnapshot: "CardDefinition"
    };

    const lower = trimmed.toLowerCase();
    if (Object.prototype.hasOwnProperty.call(aliasMap, lower)) {
        return aliasMap[lower];
    }

    const nullable = trimmed.match(/^Nullable<(.+)>$/i);
    if (nullable) {
        return normalizeSemanticTypeName(nullable[1]) || nullable[1].trim();
    }

    const genericMatch = trimmed.match(/^([^<]+)<(.+)>$/);
    if (genericMatch) {
        const containerRaw = genericMatch[1].trim();
        const containerLower = containerRaw.toLowerCase();
        const rawArgs = splitGenericTypeArguments(genericMatch[2]);
        const normalizedArgs = rawArgs.map((arg) => normalizeSemanticTypeName(arg) || arg.trim());

        if (["list", "ienumerable", "ireadonlylist", "ireadonlycollection", "icollection"].includes(containerLower)) {
            const inner = normalizedArgs[0] || "Object";
            return `List<${inner}>`;
        }

        if (["dictionary", "idictionary", "ireadonlydictionary"].includes(containerLower)) {
            const keyType = normalizedArgs[0] || "Object";
            const valueType = normalizedArgs[1] || "Object";
            const dictionaryNameMap = {
                dictionary: "Dictionary",
                idictionary: "IDictionary",
                ireadonlydictionary: "IReadOnlyDictionary"
            };
            const name = dictionaryNameMap[containerLower] || containerRaw;
            return `${name}<${keyType}, ${valueType}>`;
        }

        return `${containerRaw}<${normalizedArgs.join(", ")}>`;
    }

    if (["list", "ienumerable", "ireadonlylist", "ireadonlycollection", "icollection"].includes(lower)) {
        return "List<Object>";
    }

    return trimmed;
}

export function isSemanticCollectionType(typeName) {
    const normalized = normalizeSemanticTypeName(typeName);
    if (!normalized) return false;
    return /^(List|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection)(<.+>)?$/.test(String(normalized));
}

export function getSemanticCollectionInnerType(typeName) {
    const normalized = normalizeSemanticTypeName(typeName);
    if (!normalized || !isSemanticCollectionType(normalized)) {
        return null;
    }
    const match = normalized.match(/<(.*)>/);
    if (!match) return "Object";
    const inner = String(match[1] || "").trim();
    return normalizeSemanticTypeName(inner) || inner || "Object";
}

export function isSemanticDictionaryType(typeName) {
    const normalized = normalizeSemanticTypeName(typeName);
    if (!normalized) return false;
    return /^(Dictionary|IDictionary|IReadOnlyDictionary)<.+>$/.test(String(normalized));
}

export function isSemanticBusinessObjectType(typeName) {
    const normalized = normalizeSemanticTypeName(typeName);
    if (!normalized) return false;
    return ["PlayerState", "CardInstance", "CardDefinition", "EffectInstance", "EventInstance"].includes(normalized);
}

export function isSemanticNumericType(typeName) {
    const normalized = normalizeSemanticTypeName(typeName);
    return ["Int32", "Int64", "Single", "Double", "Decimal", "Number"].includes(normalized);
}

function isSemanticSetSingletonType(typeName) {
    const normalized = normalizeSemanticTypeName(typeName);
    if (!normalized) return false;
    if (normalized === "Void") return false;
    if (normalized === "String") return false;
    if (normalized === "Object") return false;
    return !isSemanticCollectionType(normalized);
}

function getSemanticSetOperandInfo(typeName) {
    const normalized = normalizeSemanticTypeName(typeName);
    if (!normalized || normalized === "Void" || normalized === "String") {
        return null;
    }

    if (isSemanticCollectionType(normalized)) {
        const inner = getSemanticCollectionInnerType(normalized) || "Object";
        return {
            normalized,
            collectionType: normalized,
            innerType: inner,
            promoted: false
        };
    }

    if (!isSemanticSetSingletonType(normalized)) {
        return null;
    }

    return {
        normalized,
        collectionType: `List<${normalized}>`,
        innerType: normalized,
        promoted: true
    };
}

export function canSemanticOperateAsSetOperand(typeName) {
    return Boolean(getSemanticSetOperandInfo(typeName));
}

function resolveSemanticSetResultCollectionType(leftType, rightType) {
    const leftInfo = getSemanticSetOperandInfo(leftType);
    const rightInfo = getSemanticSetOperandInfo(rightType);
    if (!leftInfo && !rightInfo) {
        return null;
    }
    if (!leftInfo) {
        return rightInfo.collectionType;
    }
    if (!rightInfo) {
        return leftInfo.collectionType;
    }

    const leftInner = leftInfo.innerType;
    const rightInner = rightInfo.innerType;
    if (leftInner === rightInner) {
        return `List<${leftInner}>`;
    }
    if (leftInner === "Object") {
        return `List<${rightInner}>`;
    }
    if (rightInner === "Object") {
        return `List<${leftInner}>`;
    }
    return null;
}

function inferFunctionCallReturnType(node, traversing) {
    const functionName = String(node?.functionName || "").toLowerCase();
    if (!functionName) {
        return normalizeSemanticTypeName(node?.returnType) || "Object";
    }

    if (functionName === "find") {
        const sourceNode = (node.args || [])[0] || null;
        const sourceType = getSemanticNodeTypeInternal(sourceNode, traversing);
        const normalizedSource = normalizeSemanticTypeName(sourceType);
        if (!normalizedSource) {
            return normalizeSemanticTypeName(node.returnType) || "Object";
        }
        if (isSemanticCollectionType(normalizedSource)) {
            return getSemanticCollectionInnerType(normalizedSource) || "Object";
        }
        if (normalizedSource === "Object") {
            return "Object";
        }
        return normalizedSource;
    }

    return normalizeSemanticTypeName(node.returnType) || "Object";
}

function getSemanticNodeTypeInternal(node, traversing) {
    if (!node) return null;

    const trackable = typeof node === "object" || typeof node === "function";
    if (trackable) {
        if (traversing.has(node)) {
            return normalizeSemanticTypeName(node.returnType) || "Object";
        }
        traversing.add(node);
    }

    try {
        if (node.type === "virtualRoot") {
            return getSemanticNodeTypeInternal(node.main, traversing) || null;
        }

        if (node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
            return getSemanticNodeTypeInternal(node.metadata.innerAST, traversing);
        }

        if (node.type === "operator") {
            const leftType = getSemanticNodeTypeInternal(node.leftOperand, traversing);
            const rightType = getSemanticNodeTypeInternal(node.rightOperand, traversing);
            if (["|", "&", "~"].includes(node.operator)) {
                const setResultType = resolveSemanticSetResultCollectionType(leftType, rightType);
                if (setResultType) return setResultType;
            }
            return normalizeSemanticTypeName(inferOperatorReturnType(node.operator)) || "Object";
        }

        if (node.type === "listQuery" || node.type === "listSort" || node.type === "listTake" || node.type === "listDistinct") {
            const sourceType = getSemanticNodeTypeInternal(node.source, traversing);
            return sourceType || normalizeSemanticTypeName(node.returnType) || "Object";
        }

        if (node.type === "listSelect") {
            const sourceType = getSemanticNodeTypeInternal(node.source, traversing);
            const selectedType = getSemanticNodeTypeInternal(node.selectExpression, traversing);
            if (sourceType && isSemanticCollectionType(sourceType)) {
                const inner = normalizeSemanticTypeName(selectedType) || "Object";
                return `List<${inner}>`;
            }
            return normalizeSemanticTypeName(selectedType) || sourceType || normalizeSemanticTypeName(node.returnType) || "Object";
        }

        if (node.type === "functionCall") {
            return inferFunctionCallReturnType(node, traversing);
        }

        return normalizeSemanticTypeName(node.returnType) || "Object";
    } finally {
        if (trackable) {
            traversing.delete(node);
        }
    }
}

export function getSemanticNodeType(node) {
    return getSemanticNodeTypeInternal(node, new WeakSet());
}

/**
 * AST节点基类
 */
export class ASTNode {
    constructor(type, returnType = 'Object') {
        this.type = type;
        this.returnType = returnType;
        this.parent = null;
        this.isComplete = false;
    }

    updateCompleteness() {
        throw new Error("updateCompleteness must be implemented by subclass");
    }

    toExpression() {
        throw new Error("toExpression must be implemented by subclass");
    }

    validate() {
        throw new Error("validate must be implemented by subclass");
    }

    clone() {
        throw new Error("clone must be implemented by subclass");
    }
}

/**
 * 函数调用节点 - 表示 functionName(arg1, arg2, ...)
 */
export class FunctionCallNode extends ASTNode {
    constructor(functionName, returnType = 'Object', parameters = []) {
        super('functionCall', returnType);
        this.functionName = functionName;
        this.parameters = parameters; // [{ name, typeName, isOptional, description }]
        this.args = new Array(parameters.length).fill(null);
        this.updateCompleteness();
    }

    setArg(index, node) {
        if (index < 0 || index >= this.args.length) return;
        this.args[index] = node;
        if (node) node.parent = this;
        this.updateCompleteness();
    }

    updateCompleteness() {
        const missingRequired = this.parameters.some((p, i) => !p.isOptional && (!this.args[i] || !this.args[i].isComplete));
        const hasIncompleteProvided = this.args.some((a, i) => a && !a.isComplete && (this.parameters[i]?.isOptional ?? false));
        this.isComplete = !missingRequired && !hasIncompleteProvided;
    }

    toExpression() {
        return stringifyAstNode(this);
    }

    validate() {
        const errors = [];

        this.parameters.forEach((p, i) => {
            const arg = this.args[i];
            if (!p.isOptional && !arg) {
                errors.push(`函数 ${this.functionName} 缺少参数: ${p.name}`);
                return;
            }
            if (arg) {
                const v = arg.validate();
                if (!v.isValid) errors.push(...v.errors);
            }
        });

        return { isValid: errors.length === 0, errors };
    }

    clone() {
        const cloned = new FunctionCallNode(this.functionName, this.returnType, Array.isArray(this.parameters) ? [...this.parameters] : []);
        cloned.args = this.args.map((a) => (a ? a.clone() : null));
        cloned.args.forEach((a) => {
            if (a) a.parent = cloned;
        });
        cloned.updateCompleteness();
        return cloned;
    }
}

/**
 * 值节点 - 表示标量值、对象引用等
 */
export class ValueNode extends ASTNode {
    constructor(value, returnType, metadata = {}) {
        super('value', returnType);
        this.value = value;
        this.metadata = metadata;
        this.updateCompleteness();
    }

    updateCompleteness() {
        // 对于静态值节点，如果正在等待输入，则不完整
        if ((this.metadata.type === 'staticValue' || this.metadata.type === 'propertyInput') && this.metadata.isAwaitingInput) {
            this.isComplete = false;
        } else {
            this.isComplete = true;
        }
    }

    toExpression() {
        return stringifyAstNode(this);
    }

    validate() {
        const errors = [];

        // 空占位符仅用于编辑过程，不应被视为合法表达式
        if (this.metadata?.type === 'empty') {
            errors.push('表达式为空');
            return { isValid: false, errors };
        }

        // 封装节点需要校验其内部 AST
        if (this.metadata?.type === 'encapsulated' && this.metadata?.innerAST) {
            const innerValidation = this.metadata.innerAST.validate();
            if (!innerValidation.isValid) {
                errors.push(...(innerValidation.errors || []));
            }
        }

        return { isValid: errors.length === 0, errors };
    }

    clone() {
        const clonedMetadata = { ...this.metadata };
        if (this.metadata?.innerAST && typeof this.metadata.innerAST.clone === 'function') {
            clonedMetadata.innerAST = this.metadata.innerAST.clone();
        }
        return new ValueNode(this.value, this.returnType, clonedMetadata);
    }
}

/**
 * 操作符节点 - 表示二元操作符
 */
export class OperatorNode extends ASTNode {
    constructor(operator, returnType = null) {
        super('operator', returnType);
        this.operator = operator;
        this.leftOperand = null;
        this.rightOperand = null;
    }

    setLeftOperand(node) {
        this.leftOperand = node;
        if (node) {
            node.parent = this;
        }
        this.updateCompleteness();
    }

    setRightOperand(node) {
        this.rightOperand = node;
        if (node) {
            node.parent = this;
        }
        this.updateCompleteness();
    }

    updateCompleteness() {
        this.isComplete = this.leftOperand && this.rightOperand &&
                         this.leftOperand.isComplete && this.rightOperand.isComplete;
    }

    toExpression() {
        return stringifyAstNode(this);
    }

    validate() {
        const errors = [];

        if (!this.leftOperand) {
            errors.push('操作符缺少左操作数');
        } else {
            const leftValidation = this.leftOperand.validate();
            if (!leftValidation.isValid) {
                errors.push(...leftValidation.errors);
            }
        }

        if (!this.rightOperand) {
            errors.push('操作符缺少右操作数');
        } else {
            const rightValidation = this.rightOperand.validate();
            if (!rightValidation.isValid) {
                errors.push(...rightValidation.errors);
            }

            // 类型兼容性检查
            if (this.leftOperand && this.rightOperand) {
                const typeError = this.validateOperandTypes();
                if (typeError) {
                    errors.push(typeError);
                }
            }
        }

        return { isValid: errors.length === 0, errors };
    }

    validateOperandTypes() {
        const normalizeTypeName = (typeName) => normalizeSemanticTypeName(typeName);

        const areElementTypesCompatible = (leftInner, rightInner) => {
            if (!leftInner || !rightInner) return true;
            if (leftInner === 'Object' || rightInner === 'Object') return true;
            return leftInner === rightInner;
        };

        const leftType = normalizeTypeName(getSemanticNodeType(this.leftOperand));
        const rightType = normalizeTypeName(getSemanticNodeType(this.rightOperand));


        // 逻辑操作符要求两个操作数都是布尔类型
        if (['&&', '||'].includes(this.operator)) {
            if (leftType !== 'Boolean') {
                return `逻辑操作符 ${this.operator} 的左操作数必须是布尔表达式，当前类型：${formatTypeName(leftType)}`;
            }
            if (rightType !== 'Boolean') {
                return `逻辑操作符 ${this.operator} 的右操作数必须是布尔表达式，当前类型：${formatTypeName(rightType)}`;
            }
        }

        // 集合操作符：允许 集合↔集合，以及 集合↔单体元素（自动包装为集合）
        if (['|', '&', '~'].includes(this.operator)) {
            const leftInfo = getSemanticSetOperandInfo(leftType);
            const rightInfo = getSemanticSetOperandInfo(rightType);

            if (!leftInfo && !rightInfo) {
                return `集合操作符 ${this.operator} 的操作数必须是集合或可提升的单体类型，当前类型：${formatTypeName(leftType)} 与 ${formatTypeName(rightType)}`;
            }
            if (!leftInfo) {
                return `集合操作符 ${this.operator} 的左操作数必须是集合或可提升的单体类型，当前类型：${formatTypeName(leftType)}`;
            }
            if (!rightInfo) {
                return `集合操作符 ${this.operator} 的右操作数必须是集合或可提升的单体类型，当前类型：${formatTypeName(rightType)}`;
            }

            if (!areElementTypesCompatible(leftInfo.innerType, rightInfo.innerType)) {
                return `集合操作符 ${this.operator} 的集合元素类型不兼容：${formatTypeName(leftInfo.innerType)} 与 ${formatTypeName(rightInfo.innerType)}`;
            }
        }

        // 比较操作符要求类型兼容
        if (['==', '!=', '>', '<', '>=', '<='].includes(this.operator)) {
            const numericTypes = ['Int32', 'Int64', 'Single', 'Double', 'Number'];
            const bothNumeric = numericTypes.includes(leftType) && numericTypes.includes(rightType);
            if (!bothNumeric && leftType !== rightType && leftType !== 'Object' && rightType !== 'Object') {
                return `比较操作符 ${this.operator} 的操作数类型不兼容：${leftType} 与 ${rightType}`;
            }
        }

        return null;
    }

    clone() {
        const cloned = new OperatorNode(this.operator, this.returnType);
        if (this.leftOperand) {
            cloned.setLeftOperand(this.leftOperand.clone());
        }
        if (this.rightOperand) {
            cloned.setRightOperand(this.rightOperand.clone());
        }
        return cloned;
    }
}

/**
 * ListQuery节点 - 表示.query()操作
 */
export class ListQueryNode extends ASTNode {
    constructor(sourceNode) {
        // .query() 操作本身返回一个新的集合，其类型与源集合相同
        super('listQuery', getSemanticNodeType(sourceNode) || sourceNode?.returnType || 'Object');
        this.source = sourceNode;
        this.filterExpression = null; // 将在后续步骤中填充
        if (sourceNode) {
            sourceNode.parent = this;
        }
        this.updateCompleteness();
    }

    setFilterExpression(node) {
        this.filterExpression = node;
        if (node) {
            node.parent = this;
        }
        this.updateCompleteness();
    }

    updateCompleteness() {
        // source 必须存在，filterExpression 可以为空（表示不过滤）
        // 但在我们的交互流程中，它必须被填充才算完成
        const isEmpty = !this.filterExpression
            || this.filterExpression.metadata?.type === 'empty'
            || (this.filterExpression.metadata?.type === 'encapsulated'
                && this.filterExpression.metadata?.innerAST?.metadata?.type === 'empty');
        this.isComplete = this.source && (isEmpty || this.filterExpression.isComplete);
    }

    toExpression() {
        return stringifyAstNode(this);
    }

        validate() {
            const errors = [];
            if (!this.source) {
                errors.push('Query操作缺少源集合');
            }
            const isEmpty = !this.filterExpression
                || this.filterExpression.metadata?.type === 'empty'
                || (this.filterExpression.metadata?.type === 'encapsulated'
                    && this.filterExpression.metadata?.innerAST?.metadata?.type === 'empty');
            if (!isEmpty) {
                const filterValidation = this.filterExpression.validate();
                if (!filterValidation.isValid) {
                    errors.push(...filterValidation.errors);
                }
                const filterType = getSemanticNodeType(this.filterExpression);
                if (filterType !== 'Boolean') {
                    errors.push(`Query查询表达式必须返回布尔值，当前返回: ${formatTypeName(filterType)}`);
                }
            }
            return { isValid: errors.length === 0, errors };
        }

    clone() {
        const cloned = new ListQueryNode(this.source.clone());
        if (this.filterExpression) {
            cloned.setFilterExpression(this.filterExpression.clone());
        }
        return cloned;
    }
}
    
    /**
     * ListSortNode - 表示.orderBy()操作
     */
export class ListSortNode extends ASTNode {
    constructor(sourceNode) {
        super('listSort', getSemanticNodeType(sourceNode) || sourceNode?.returnType || 'Object');
        this.source = sourceNode;
        this.sortExpression = null; // 排序表达式
        this.direction = 'asc';
        if (sourceNode) {
            sourceNode.parent = this;
        }
        this.updateCompleteness();
    }
    
    setSortExpression(node) {
        this.sortExpression = node;
        if (node) {
            node.parent = this;
        }
        this.updateCompleteness();
    }

    setDirection(direction) {
        const normalized = String(direction || '').toLowerCase();
        this.direction = normalized === 'desc' ? 'desc' : 'asc';
    }
    
        updateCompleteness() {
            const isEmpty = !this.sortExpression
                || this.sortExpression.metadata?.type === 'empty'
                || (this.sortExpression.metadata?.type === 'encapsulated'
                    && this.sortExpression.metadata?.innerAST?.metadata?.type === 'empty');
            this.isComplete = this.source && (isEmpty || this.sortExpression.isComplete);
        }

    toExpression() {
        return stringifyAstNode(this);
    }
    
        validate() {
            const errors = [];
            if (!this.source) errors.push('OrderBy操作缺少源集合');
            const isEmpty = !this.sortExpression
                || this.sortExpression.metadata?.type === 'empty'
                || (this.sortExpression.metadata?.type === 'encapsulated'
                    && this.sortExpression.metadata?.innerAST?.metadata?.type === 'empty');
            if (!isEmpty) {
                const v = this.sortExpression.validate();
                if (!v.isValid) errors.push(...v.errors);
                const sortType = normalizeSemanticTypeName(getSemanticNodeType(this.sortExpression));
                if (sortType && (isSemanticCollectionType(sortType) || isSemanticDictionaryType(sortType) || isSemanticBusinessObjectType(sortType))) {
                    errors.push(`OrderBy排序键必须为非实体类型，当前返回: ${formatTypeName(sortType)}`);
                }
            }
            return { isValid: errors.length === 0, errors };
        }
    
    clone() {
        const cloned = new ListSortNode(this.source.clone());
        cloned.direction = this.direction;
        if (this.sortExpression) {
            cloned.setSortExpression(this.sortExpression.clone());
        }
        return cloned;
    }
    }
    
    /**
     * ListDistinctNode - 表示.distinct()操作
     */
export class ListDistinctNode extends ASTNode {
    constructor(sourceNode) {
        super('listDistinct', getSemanticNodeType(sourceNode) || sourceNode?.returnType || 'Object');
        this.source = sourceNode;
        this.keyExpression = null;
        if (sourceNode) {
            sourceNode.parent = this;
        }
        this.updateCompleteness();
    }

    setKeyExpression(node) {
        this.keyExpression = node;
        if (node) {
            node.parent = this;
        }
        this.updateCompleteness();
    }

    updateCompleteness() {
        const isEmpty = !this.keyExpression
            || this.keyExpression.metadata?.type === 'empty'
            || (this.keyExpression.metadata?.type === 'encapsulated'
                && this.keyExpression.metadata?.innerAST?.metadata?.type === 'empty');
        this.isComplete = this.source && !isEmpty && this.keyExpression.isComplete;
    }

    toExpression() {
        return stringifyAstNode(this);
    }

    validate() {
        const errors = [];
        if (!this.source) errors.push('Distinct操作缺少源集合');
        if (!this.keyExpression) {
            errors.push('Distinct操作缺少去重键表达式');
            return { isValid: false, errors };
        }
        const v = this.keyExpression.validate();
        if (!v.isValid) errors.push(...v.errors);

        const keyType = normalizeSemanticTypeName(getSemanticNodeType(this.keyExpression));
        if (keyType && (isSemanticDictionaryType(keyType) || isSemanticBusinessObjectType(keyType))) {
            errors.push(`Distinct去重键必须返回标量或标量列表，当前返回: ${formatTypeName(keyType)}`);
        }

        return { isValid: errors.length === 0, errors };
    }

    clone() {
        const cloned = new ListDistinctNode(this.source.clone());
        if (this.keyExpression) {
            cloned.setKeyExpression(this.keyExpression.clone());
        }
        return cloned;
    }
}

/**
 * ListSelectNode - 表示.select()操作
 */
export class ListSelectNode extends ASTNode {
    constructor(sourceNode) {
        super('listSelect', getSemanticNodeType(sourceNode) || sourceNode?.returnType || 'Object');
        this.source = sourceNode;
        this.selectExpression = null;
        if (sourceNode) {
            sourceNode.parent = this;
        }
        this.updateCompleteness();
    }

    setSelectExpression(node) {
        this.selectExpression = node;
        if (node) {
            node.parent = this;
        }
        const sourceType = getSemanticNodeType(this.source);
        const inner = getSemanticCollectionInnerType(sourceType);
        if (inner !== null) {
            const selectedType = normalizeSemanticTypeName(getSemanticNodeType(node)) || "Object";
            this.returnType = `List<${selectedType}>`;
        } else {
            const selectedType = normalizeSemanticTypeName(getSemanticNodeType(node));
            if (selectedType) {
                this.returnType = selectedType;
            } else if (sourceType) {
                this.returnType = sourceType;
            }
        }
        this.updateCompleteness();
    }

    updateCompleteness() {
        const isEmpty = !this.selectExpression
            || this.selectExpression.metadata?.type === 'empty'
            || (this.selectExpression.metadata?.type === 'encapsulated'
                && this.selectExpression.metadata?.innerAST?.metadata?.type === 'empty');
        this.isComplete = this.source && !isEmpty && this.selectExpression.isComplete;
    }

    toExpression() {
        return stringifyAstNode(this);
    }

    validate() {
        const errors = [];
        if (!this.source) errors.push('Select操作缺少源集合');
        if (!this.selectExpression) {
            errors.push('Select操作缺少投影表达式');
            return { isValid: false, errors };
        }
        const v = this.selectExpression.validate();
        if (!v.isValid) errors.push(...v.errors);

        return { isValid: errors.length === 0, errors };
    }

    clone() {
        const cloned = new ListSelectNode(this.source.clone());
        if (this.selectExpression) {
            cloned.setSelectExpression(this.selectExpression.clone());
        }
        return cloned;
    }
}

    /**
     * ListTakeNode - 表示.take()操作
     */
export class ListTakeNode extends ASTNode {
    constructor(sourceNode) {
        super('listTake', getSemanticNodeType(sourceNode) || sourceNode?.returnType || 'Object');
        this.source = sourceNode;
        this.countExpression = null; // 数量表达式
        this.strategy = 'first';
        if (sourceNode) {
            sourceNode.parent = this;
        }
        this.updateCompleteness();
    }
    
        setCountExpression(node) {
            this.countExpression = node;
            if (node) {
                node.parent = this;
            }
            this.updateCompleteness();
        }

    setStrategy(strategy) {
        const normalized = String(strategy || '').toLowerCase();
        if (['first', 'last', 'random', 'all'].includes(normalized)) {
            this.strategy = normalized;
        }
    }
    
    updateCompleteness() {
        if (String(this.strategy || '').toLowerCase() === 'all') {
            this.isComplete = Boolean(this.source);
            return;
        }
        const isEmpty = !this.countExpression
            || this.countExpression.metadata?.type === 'empty'
            || (this.countExpression.metadata?.type === 'encapsulated'
                && this.countExpression.metadata?.innerAST?.metadata?.type === 'empty');
        this.isComplete = this.source && (isEmpty || this.countExpression.isComplete);
        }

    toExpression() {
        return stringifyAstNode(this);
    }
    
    validate() {
        const errors = [];
        if (!this.source) errors.push('Take操作缺少源集合');
        if (String(this.strategy || '').toLowerCase() === 'all') {
            return { isValid: errors.length === 0, errors };
        }
        const isEmpty = !this.countExpression
            || this.countExpression.metadata?.type === 'empty'
            || (this.countExpression.metadata?.type === 'encapsulated'
                && this.countExpression.metadata?.innerAST?.metadata?.type === 'empty');
        if (!isEmpty) {
                const v = this.countExpression.validate();
                if (!v.isValid) errors.push(...v.errors);

                const t = normalizeSemanticTypeName(getSemanticNodeType(this.countExpression));
                if (t && !isSemanticNumericType(t)) {
                    errors.push(`Take数量表达式必须返回数值，当前返回: ${formatTypeName(t)}`);
                }
            }
            return { isValid: errors.length === 0, errors };
        }
    
        clone() {
            const cloned = new ListTakeNode(this.source.clone());
            cloned.strategy = this.strategy;
            if (this.countExpression) {
                cloned.setCountExpression(this.countExpression.clone());
            }
            return cloned;
        }
    }
    
    export function createListSortNode(sourceNode) {
        return new ListSortNode(sourceNode);
    }
    
    export function createListTakeNode(sourceNode) {
        return new ListTakeNode(sourceNode);
    }

export function createListDistinctNode(sourceNode) {
    return new ListDistinctNode(sourceNode);
}

export function createListSelectNode(sourceNode) {
    return new ListSelectNode(sourceNode);
}


export function createValueNode(value, returnType, metadata = {}) {
    return new ValueNode(value, returnType, metadata);
}

export function createOperatorNode(operator, returnType = null) {
    return new OperatorNode(operator, returnType ?? inferOperatorReturnType(operator));
}

export function createFunctionCallNode(functionName, returnType = 'Object', parameters = []) {
    return new FunctionCallNode(functionName, returnType, parameters);
}

export function createListQueryNode(sourceNode) {
    return new ListQueryNode(sourceNode);
}

export function inferOperatorReturnType(operator) {
    if (['&&', '||'].includes(operator)) {
        return 'Boolean';
    }
    if (['==', '!=', '>', '<', '>=', '<=', 'contains', 'startsWith', 'endsWith'].includes(operator)) {
        return 'Boolean';
    }
    if (['+', '-', '*', '/', '\\', '%'].includes(operator)) {
        return 'Number';
    }
    return 'Object';
}

export function validateAST(root) {
    if (!root) return { isValid: true, errors: [] };
    return root.validate();
}

export function findIncompleteNode(root) {
    if (!root) return null;
    if (!root.isComplete) return root;

    if (root.type === 'operator') {
        const leftIncomplete = findIncompleteNode(root.leftOperand);
        if (leftIncomplete) return leftIncomplete;

        const rightIncomplete = findIncompleteNode(root.rightOperand);
        if (rightIncomplete) return rightIncomplete;
    }
    if (root.type === 'functionCall') {
        for (const arg of root.args || []) {
            const incomplete = findIncompleteNode(arg);
            if (incomplete) return incomplete;
        }
    }
    if (root.type === 'listQuery') {
        const src = findIncompleteNode(root.source);
        if (src) return src;
        const f = findIncompleteNode(root.filterExpression);
        if (f) return f;
    }
    if (root.type === 'listSort') {
        const src = findIncompleteNode(root.source);
        if (src) return src;
        const f = findIncompleteNode(root.sortExpression);
        if (f) return f;
    }
    if (root.type === 'listTake') {
        const src = findIncompleteNode(root.source);
        if (src) return src;
        const strategy = String(root.strategy || '').toLowerCase();
        if (strategy !== 'all') {
            const f = findIncompleteNode(root.countExpression);
            if (f) return f;
        }
    }
    if (root.type === 'listDistinct') {
        const src = findIncompleteNode(root.source);
        if (src) return src;
        const f = findIncompleteNode(root.keyExpression);
        if (f) return f;
    }
    if (root.type === 'listSelect') {
        const src = findIncompleteNode(root.source);
        if (src) return src;
        const f = findIncompleteNode(root.selectExpression);
        if (f) return f;
    }
    return null;
}

export function findRightmostLeafNode(root) {
    if (!root) return null;

    if (root.type === 'value') {
        return root;
    }
    if (root.type === 'functionCall') {
        const args = root.args || [];
        for (let i = args.length - 1; i >= 0; i--) {
            if (args[i]) return findRightmostLeafNode(args[i]);
        }
        return root;
    }

    if (root.type === 'operator') {
        if (root.rightOperand) {
            return findRightmostLeafNode(root.rightOperand);
        }
        if (root.leftOperand) {
            return findRightmostLeafNode(root.leftOperand);
        }
    }

    return root;
}
