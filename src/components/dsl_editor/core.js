// Phase C - 核心 AST 类与工具函数迁移

function formatTypeName(typeName) {
    if (!typeName) return '未知类型';
    const trimmed = String(typeName).trim();
    if (!trimmed) return '未知类型';

    const match = trimmed.match(/^(List|IEnumerable)<(.+)>$/);
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
        const parts = this.args.map((a) => (a ? a.toExpression() : ''));
        return `${this.functionName}(${parts.join(', ')})`;
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
        // 如果是封装的表达式，返回其显示值
        if (this.metadata && this.metadata.type === "encapsulated") {
            return this.metadata.displayValue || this.value;
        }
        return this.value;
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
        // 检查操作数是否为 'empty' 节点，如果是，也视为空
        const leftNode = this.leftOperand;
        const rightNode = this.rightOperand;

        const leftIsEmpty = !leftNode || (leftNode.metadata && leftNode.metadata.type === 'empty');
        const rightIsEmpty = !rightNode || (rightNode.metadata && rightNode.metadata.type === 'empty');

        const left = leftIsEmpty ? '()' : leftNode.toExpression();
        const right = rightIsEmpty ? '()' : rightNode.toExpression();

        // 即使右侧为空，也返回完整的表达式结构
        return `${left} ${this.operator} ${right}`;
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
        const resolveNodeType = (node) => {
            if (!node) return null;
            if (node.metadata?.type === 'encapsulated' && node.metadata?.innerAST) {
                return resolveNodeType(node.metadata.innerAST);
            }
            return node.returnType;
        };

        const normalizeTypeName = (typeName) => {
            if (!typeName) return null;
            const trimmed = String(typeName).trim();
            if (!trimmed) return null;
            const aliasMap = {
                CardInstanceSnapshot: 'CardInstance',
                CardInstanceResetSnapshot: 'CardInstance',
                CardDefinitionSnapshot: 'CardDefinition'
            };
            const nullable = trimmed.match(/^Nullable<(.+)>$/);
            if (nullable) return aliasMap[nullable[1]] || nullable[1];
            if (trimmed === 'List' || trimmed === 'IEnumerable' || trimmed === 'IReadOnlyList' || trimmed === 'IReadOnlyCollection' || trimmed === 'ICollection') {
                return 'List<Object>';
            }
            const roListMatch = trimmed.match(/^IReadOnlyList<(.+)>$/);
            if (roListMatch) return `List<${aliasMap[roListMatch[1]] || roListMatch[1]}>`;
            const roCollectionMatch = trimmed.match(/^IReadOnlyCollection<(.+)>$/);
            if (roCollectionMatch) return `List<${aliasMap[roCollectionMatch[1]] || roCollectionMatch[1]}>`;
            const collectionMatch = trimmed.match(/^ICollection<(.+)>$/);
            if (collectionMatch) return `List<${aliasMap[collectionMatch[1]] || collectionMatch[1]}>`;
            const enumerableMatch = trimmed.match(/^IEnumerable<(.+)>$/);
            if (enumerableMatch) return `List<${aliasMap[enumerableMatch[1]] || enumerableMatch[1]}>`;
            return aliasMap[trimmed] || trimmed;
        };

        const isCollectionType = (typeName) => {
            const normalized = normalizeTypeName(typeName);
            if (!normalized) return false;
            return /^(List|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection)(<.+>)?$/.test(String(normalized));
        };

        const getCollectionInnerType = (typeName) => {
            const normalized = normalizeTypeName(typeName);
            if (!normalized) return null;
            const match = String(normalized).match(/<(.*)>/);
            return match ? match[1] : 'Object';
        };

        const areElementTypesCompatible = (leftInner, rightInner) => {
            if (!leftInner || !rightInner) return true;
            if (leftInner === 'Object' || rightInner === 'Object') return true;
            return leftInner === rightInner;
        };

        const isScalarCompatibleWithCollection = (scalarType, collectionType) => {
            const scalar = normalizeTypeName(scalarType);
            const collectionInner = getCollectionInnerType(collectionType);
            if (!scalar || !collectionInner) return false;
            if (scalar.toLowerCase() === 'string') return false;
            return collectionInner === 'Object' || scalar === collectionInner;
        };

        const leftType = resolveNodeType(this.leftOperand);
        const rightType = resolveNodeType(this.rightOperand);


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
            const leftIsCollection = isCollectionType(leftType);
            const rightIsCollection = isCollectionType(rightType);

            if (!leftIsCollection && !rightIsCollection) {
                return `集合操作符 ${this.operator} 至少需要一个集合操作数，当前类型：${formatTypeName(leftType)} 与 ${formatTypeName(rightType)}`;
            }

            const referenceCollectionType = leftIsCollection ? leftType : rightType;
            const referenceInnerType = getCollectionInnerType(referenceCollectionType);

            if (leftIsCollection) {
                const leftInner = getCollectionInnerType(leftType);
                if (!areElementTypesCompatible(leftInner, referenceInnerType)) {
                    return `集合操作符 ${this.operator} 的集合元素类型不兼容：${formatTypeName(leftInner)} 与 ${formatTypeName(referenceInnerType)}`;
                }
            } else if (!isScalarCompatibleWithCollection(leftType, referenceCollectionType)) {
                return `集合操作符 ${this.operator} 的左操作数必须是集合或兼容的元素类型，当前类型：${formatTypeName(leftType)}`;
            }

            if (rightIsCollection) {
                const rightInner = getCollectionInnerType(rightType);
                if (!areElementTypesCompatible(rightInner, referenceInnerType)) {
                    return `集合操作符 ${this.operator} 的集合元素类型不兼容：${formatTypeName(referenceInnerType)} 与 ${formatTypeName(rightInner)}`;
                }
            } else if (!isScalarCompatibleWithCollection(rightType, referenceCollectionType)) {
                return `集合操作符 ${this.operator} 的右操作数必须是集合或兼容的元素类型，当前类型：${formatTypeName(rightType)}`;
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
        super('listQuery', sourceNode.returnType);
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
        const sourceExpr = this.source ? this.source.toExpression() : '';
        const filterExpr = this.filterExpression ? this.filterExpression.toExpression() : '';
        const isEmpty = !this.filterExpression
            || this.filterExpression.metadata?.type === 'empty'
            || (this.filterExpression.metadata?.type === 'encapsulated'
                && this.filterExpression.metadata?.innerAST?.metadata?.type === 'empty')
            || !filterExpr;
        if (isEmpty) return sourceExpr;
        return `${sourceExpr}.query(${filterExpr})`;
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
                if (this.filterExpression.returnType !== 'Boolean') {
                    errors.push(`Query查询表达式必须返回布尔值，当前返回: ${formatTypeName(this.filterExpression.returnType)}`);
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
        super('listSort', sourceNode.returnType);
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
        const sourceExpr = this.source ? this.source.toExpression() : '';
        const sortExpr = this.sortExpression ? this.sortExpression.toExpression() : '';
        const isEmpty = !this.sortExpression
            || this.sortExpression.metadata?.type === 'empty'
            || (this.sortExpression.metadata?.type === 'encapsulated'
                && this.sortExpression.metadata?.innerAST?.metadata?.type === 'empty')
            || !sortExpr;
        if (isEmpty) return sourceExpr;
        const dir = String(this.direction || 'asc').toLowerCase();
        const suffix = dir === 'desc' ? ' desc' : '';
        return `${sourceExpr}.orderBy(${sortExpr}${suffix})`;
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
                const sortType = this.sortExpression.returnType;
                const isCollectionType = (typeName) => /^(List|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection)(<.+>)?$/.test(String(typeName || ''));
                const isDictionaryType = (typeName) => /^(Dictionary|IDictionary|IReadOnlyDictionary)<.+>$/.test(String(typeName || ''));
                const businessObjectTypes = [
                    'PlayerState',
                    'CardInstance',
                    'CardDefinition',
                    'EffectInstance',
                    'EventInstance'
                ];
                if (sortType && (isCollectionType(sortType) || isDictionaryType(sortType) || businessObjectTypes.includes(sortType))) {
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
        super('listDistinct', sourceNode.returnType);
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
        const sourceExpr = this.source ? this.source.toExpression() : '';
        const keyExpr = this.keyExpression ? this.keyExpression.toExpression() : '';
        return `${sourceExpr}.distinct(${keyExpr})`;
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

        const keyType = this.keyExpression.returnType;
        const isCollectionType = (typeName) => /^(List|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection)(<.+>)?$/.test(String(typeName || ''));
        const isDictionaryType = (typeName) => /^(Dictionary|IDictionary|IReadOnlyDictionary)<.+>$/.test(String(typeName || ''));
        const businessObjectTypes = [
            'PlayerState',
            'CardInstance',
            'CardDefinition',
            'EffectInstance',
            'EventInstance'
        ];
        if (keyType && (isCollectionType(keyType) || isDictionaryType(keyType) || businessObjectTypes.includes(keyType))) {
            errors.push(`Distinct去重键必须为非实体类型，当前返回: ${formatTypeName(keyType)}`);
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
        super('listSelect', sourceNode.returnType);
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
        const resolveCollectionInnerType = (typeName) => {
            const trimmed = String(typeName || "").trim();
            if (!trimmed) return null;
            if (trimmed === 'List' || trimmed === 'IEnumerable' || trimmed === 'IReadOnlyList' || trimmed === 'IReadOnlyCollection' || trimmed === 'ICollection') {
                return 'Object';
            }
            const match = trimmed.match(/^(List|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection)<(.+)>$/);
            return match ? match[2] : null;
        };
        const inner = resolveCollectionInnerType(this.source?.returnType);
        if (inner !== null) {
            const selectedType = node?.returnType || "Object";
            this.returnType = `List<${selectedType}>`;
        } else if (node?.returnType) {
            this.returnType = node.returnType;
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
        const sourceExpr = this.source ? this.source.toExpression() : '';
        const selectExpr = this.selectExpression ? this.selectExpression.toExpression() : '';
        return `${sourceExpr}.select(${selectExpr})`;
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
        super('listTake', sourceNode.returnType);
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
        const sourceExpr = this.source ? this.source.toExpression() : '';
        const strategy = String(this.strategy || 'first').toLowerCase();
        if (strategy === 'all') {
            return `${sourceExpr}.take(all)`;
        }
        const countExpr = this.countExpression ? this.countExpression.toExpression() : '';
        const isEmpty = !this.countExpression
            || this.countExpression.metadata?.type === 'empty'
            || (this.countExpression.metadata?.type === 'encapsulated'
                && this.countExpression.metadata?.innerAST?.metadata?.type === 'empty')
            || !countExpr;
        if (isEmpty) return sourceExpr;
        if (strategy && strategy !== 'first') {
            return `${sourceExpr}.take(${strategy} ${countExpr})`;
        }
        return `${sourceExpr}.take(${countExpr})`;
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

                const t = this.countExpression.returnType;
                const numericTypes = ['Int32', 'Int64', 'Single', 'Double', 'Number'];
                if (t && !numericTypes.includes(t)) {
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
