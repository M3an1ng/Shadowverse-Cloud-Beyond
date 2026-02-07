/**
 * DSL编辑器全新架构设计
 * 彻底解决表达式层级、类型判断和操作符添加问题
 */

/**
 * 表达式上下文管理器
 * 负责管理当前编辑上下文和层级关系
 */
class ExpressionContext {
    constructor() {
        this.currentExpression = null;      // 当前正在编辑的表达式
        this.editingLevel = 0;              // 当前编辑层级
        this.parentContext = null;          // 父级上下文
        this.editingTarget = null;          // 当前编辑目标节点
        this.operationMode = 'append';      // 操作模式：append, replace, extend
    }

    /**
     * 设置编辑目标
     */
    setEditingTarget(node, mode = 'append') {
        this.editingTarget = node;
        this.operationMode = mode;
        
    }

    /**
     * 获取操作符添加的正确位置
     */
    getOperatorInsertionPoint() {
        if (!this.editingTarget) return null;

        // 基于编辑目标和模式确定插入位置
        if (this.operationMode === 'append') {
            // 在目标节点后追加操作符
            return {
                position: 'after',
                target: this.editingTarget,
                level: this.editingLevel
            };
        } else if (this.operationMode === 'extend') {
            // 扩展当前表达式
            return {
                position: 'extend',
                target: this.currentExpression,
                level: this.editingLevel
            };
        }

        return null;
    }
}

/**
 * 表达式节点基类
 * 统一的节点接口和行为
 */
class ExpressionNode {
    constructor(type, value) {
        this.type = type;
        this.value = value;
        this.parent = null;
        this.children = [];
        this.level = 0;                     // 节点层级
        this.expectedType = null;           // 期望类型
        this.actualType = null;             // 实际类型
        this.isComplete = false;            // 是否完整
        this.editingContext = null;         // 编辑上下文
    }

    /**
     * 添加子节点
     */
    addChild(child) {
        child.parent = this;
        child.level = this.level + 1;
        this.children.push(child);
        this.updateTypes();
    }

    /**
     * 获取最后一个可编辑的节点
     */
    getLastEditableNode() {
        // 如果当前节点是完整的封装表达式，返回自己
        if (this.isCompleteEncapsulation()) {
            return this;
        }

        // 否则递归查找最后一个子节点
        if (this.children.length > 0) {
            const lastChild = this.children[this.children.length - 1];
            return lastChild.getLastEditableNode();
        }

        return this;
    }

    /**
     * 判断是否是完整的封装表达式
     */
    isCompleteEncapsulation() {
        return this.type === 'encapsulated' && this.isComplete && !this.hasIncompleteChildren();
    }

    /**
     * 检查是否有未完成的子节点
     */
    hasIncompleteChildren() {
        return this.children.some(child => !child.isComplete);
    }

    /**
     * 更新类型信息
     */
    updateTypes() {
        this.actualType = this.calculateActualType();
        this.expectedType = this.calculateExpectedType();
    }

    /**
     * 计算实际类型（子类实现）
     */
    calculateActualType() {
        return 'Object';
    }

    /**
     * 计算期望类型（子类实现）
     */
    calculateExpectedType() {
        return null;
    }
}

/**
 * 值节点
 */
class ValueNode extends ExpressionNode {
    constructor(value, dataType = 'Object') {
        super('value', value);
        this.dataType = dataType;
        this.isComplete = true;
    }

    calculateActualType() {
        return this.dataType;
    }

    getLastEditableNode() {
        return this;
    }
}

/**
 * 操作符节点
 */
class OperatorNode extends ExpressionNode {
    constructor(operator) {
        super('operator', operator);
        this.operator = operator;
        this.leftOperand = null;
        this.rightOperand = null;
    }

    setLeftOperand(node) {
        this.leftOperand = node;
        this.addChild(node);
    }

    setRightOperand(node) {
        this.rightOperand = node;
        this.addChild(node);
        this.isComplete = true;
    }

    calculateActualType() {
        if (['&&', '||'].includes(this.operator)) {
            return 'Boolean';
        } else if (['>', '<', '>=', '<=', '==', '!='].includes(this.operator)) {
            return 'Boolean';
        } else if (['+', '-', '*', '/'].includes(this.operator)) {
            return 'Int32';
        }
        return 'Object';
    }

    getLastEditableNode() {
        if (!this.rightOperand) {
            // 操作符缺少右操作数，返回自己等待右操作数
            return this;
        }
        
        // 返回右操作数的最后可编辑节点
        return this.rightOperand.getLastEditableNode();
    }
}

/**
 * 封装表达式节点
 */
class EncapsulatedNode extends ExpressionNode {
    constructor(expression, innerNode) {
        super('encapsulated', expression);
        this.innerNode = innerNode;
        this.isComplete = true;
        
        if (innerNode) {
            this.addChild(innerNode);
        }
    }

    calculateActualType() {
        return this.innerNode ? this.innerNode.calculateActualType() : 'Object';
    }

    getLastEditableNode() {
        // 关键：封装表达式的行为取决于编辑上下文
        if (this.editingContext && this.editingContext.operationMode === 'extend') {
            // 如果是扩展模式，返回内部的最后节点
            return this.innerNode ? this.innerNode.getLastEditableNode() : this;
        } else {
            // 如果是追加模式，返回自己作为整体
            return this;
        }
    }
}

/**
 * 表达式构建器
 * 负责正确构建和修改表达式
 */
class ExpressionBuilder {
    constructor() {
        this.context = new ExpressionContext();
    }

    /**
     * 添加操作符到表达式
     */
    addOperator(currentExpression, operator) {

        // 获取最后一个可编辑的节点
        const lastNode = currentExpression.getLastEditableNode();

        // 基于最后节点的类型和操作符类型决定构建策略
        const strategy = this.determineConstructionStrategy(lastNode, operator);

        return this.executeConstructionStrategy(currentExpression, lastNode, operator, strategy);
    }

    /**
     * 确定构建策略
     */
    determineConstructionStrategy(lastNode, operator) {
        const lastNodeType = lastNode.actualType;
        const operatorGroup = this.getOperatorGroup(operator);


        // 如果最后节点是数值类型，且操作符是比较操作符
        if (lastNodeType === 'Int32' && operatorGroup === 'comparison') {
            return 'extend_last_node';  // 扩展最后一个节点
        }

        // 如果最后节点是布尔类型，且操作符是逻辑操作符
        if (lastNodeType === 'Boolean' && operatorGroup === 'logical') {
            return 'extend_expression';  // 扩展整个表达式
        }

        // 默认策略
        return 'extend_expression';
    }

    /**
     * 执行构建策略
     */
    executeConstructionStrategy(currentExpression, lastNode, operator, strategy) {
        switch (strategy) {
            case 'extend_last_node':
                return this.extendLastNode(currentExpression, lastNode, operator);
            
            case 'extend_expression':
                return this.extendExpression(currentExpression, operator);
            
            default:
                throw new Error(`Unknown construction strategy: ${strategy}`);
        }
    }

    /**
     * 扩展最后一个节点
     */
    extendLastNode(currentExpression, lastNode, operator) {

        // 创建新的操作符节点
        const operatorNode = new OperatorNode(operator);
        operatorNode.setLeftOperand(lastNode);

        // 如果最后节点有父节点，需要替换父节点中的引用
        if (lastNode.parent) {
            this.replaceNodeInParent(lastNode, operatorNode);
        } else {
            // 如果最后节点就是根节点，创建新的封装表达式
            const newExpression = new EncapsulatedNode(
                `(${lastNode.value} ${operator} )`,
                operatorNode
            );
            return newExpression;
        }

        return currentExpression;
    }

    /**
     * 扩展整个表达式
     */
    extendExpression(currentExpression, operator) {

        // 创建新的操作符节点
        const operatorNode = new OperatorNode(operator);
        operatorNode.setLeftOperand(currentExpression);

        // 创建新的封装表达式
        const newExpression = new EncapsulatedNode(
            `(${currentExpression.value} ${operator} )`,
            operatorNode
        );

        return newExpression;
    }

    /**
     * 在父节点中替换子节点
     */
    replaceNodeInParent(oldNode, newNode) {
        const parent = oldNode.parent;
        if (parent instanceof OperatorNode) {
            if (parent.leftOperand === oldNode) {
                parent.setLeftOperand(newNode);
            } else if (parent.rightOperand === oldNode) {
                parent.setRightOperand(newNode);
            }
        }
    }

    /**
     * 获取操作符分组
     */
    getOperatorGroup(operator) {
        if (['>', '<', '>=', '<=', '==', '!='].includes(operator)) {
            return 'comparison';
        } else if (['&&', '||'].includes(operator)) {
            return 'logical';
        } else if (['+', '-', '*', '/'].includes(operator)) {
            return 'arithmetic';
        }
        return 'unknown';
    }
}

export { 
    ExpressionContext, 
    ExpressionNode, 
    ValueNode, 
    OperatorNode, 
    EncapsulatedNode, 
    ExpressionBuilder 
};
