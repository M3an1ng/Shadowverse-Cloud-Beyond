/**
 * 重构的AST节点结构
 * 支持类型判断与后续节点生成功能
 */

/**
 * 基础AST节点类
 */
class ASTNode {
    constructor(type, value = null) {
        this.type = type;
        this.value = value;
        this.parent = null;
        this.isComplete = false;
        
        // 关键：每个节点都维护返回类型和实际类型
        this.expectedType = null;  // 期望类型（基于父节点上下文）
        this.actualType = null;    // 实际类型（基于节点内容计算）
        
        // 封装相关
        this.isEncapsulated = false;
        this.encapsulationLevel = 0;  // 嵌套层级
        
        // 类型匹配信息
        this.typeMatchInfo = {
            isTypeMatch: true,
            needsCorrection: false,
            hasTypeIssue: false
        };
    }

    /**
     * 计算节点的实际类型
     */
    calculateActualType() {
        // 子类实现
        return 'Object';
    }

    /**
     * 计算节点的期望类型（基于父节点上下文）
     */
    calculateExpectedType() {
        if (!this.parent) return null;
        
        if (this.parent.type === 'operator') {
            const parentOperator = this.parent.operator;
            const isRightOperand = this.parent.rightOperand === this;
            
            // 根据操作符确定期望类型
            if (['&&', '||'].includes(parentOperator)) {
                return 'Boolean';
            } else if (['>', '<', '>=', '<='].includes(parentOperator)) {
                if (isRightOperand && this.parent.leftOperand) {
                    return this.parent.leftOperand.actualType || 'Int32';
                }
                return 'Int32';
            } else if (['==', '!='].includes(parentOperator)) {
                if (isRightOperand && this.parent.leftOperand) {
                    return this.parent.leftOperand.actualType;
                }
            }
        }
        
        return null;
    }

    /**
     * 更新类型匹配信息
     */
    updateTypeMatchInfo() {
        this.expectedType = this.calculateExpectedType();
        this.actualType = this.calculateActualType();
        
        if (this.expectedType && this.actualType !== this.expectedType) {
            this.typeMatchInfo = {
                isTypeMatch: false,
                needsCorrection: true,
                hasTypeIssue: true
            };
        } else {
            this.typeMatchInfo = {
                isTypeMatch: true,
                needsCorrection: false,
                hasTypeIssue: false
            };
        }
    }

    /**
     * 获取最后一个节点（用于后续选项生成）
     */
    getLastNode() {
        // 默认返回自己
        return this;
    }

    /**
     * 是否是封装表达式的最外层
     */
    isOutermostEncapsulation() {
        return this.isEncapsulated && (!this.parent || !this.parent.isEncapsulated);
    }

    /**
     * 获取用于选项生成的类型
     */
    getTypeForOptions() {
        // 如果是封装表达式的最外层，返回实际类型
        if (this.isOutermostEncapsulation()) {
            return this.actualType;
        }
        
        // 否则获取最后一个节点的类型
        const lastNode = this.getLastNode();
        return lastNode.actualType;
    }
}

/**
 * 值节点类
 */
class ValueNode extends ASTNode {
    constructor(value, returnType = 'Object') {
        super('value', value);
        this.returnType = returnType;
        this.metadata = null;
    }

    calculateActualType() {
        // 如果是封装表达式，计算其内部AST的返回类型
        if (this.metadata && this.metadata.type === "encapsulated" && this.metadata.innerAST) {
            return this.metadata.innerAST.calculateActualType();
        }
        
        return this.returnType || 'Object';
    }

    getLastNode() {
        // 如果是封装表达式，获取内部AST的最后节点
        if (this.metadata && this.metadata.type === "encapsulated" && this.metadata.innerAST) {
            return this.metadata.innerAST.getLastNode();
        }
        
        return this;
    }
}

/**
 * 操作符节点类
 */
class OperatorNode extends ASTNode {
    constructor(operator) {
        super('operator');
        this.operator = operator;
        this.leftOperand = null;
        this.rightOperand = null;
    }

    calculateActualType() {
        // 根据操作符类型推断返回类型
        if (['&&', '||'].includes(this.operator)) {
            return 'Boolean';
        } else if (['>', '<', '>=', '<=', '==', '!='].includes(this.operator)) {
            return 'Boolean';
        } else if (['+', '-', '*', '/'].includes(this.operator)) {
            return 'Int32';
        }
        
        return 'Object';
    }

    getLastNode() {
        // 操作符节点的最后节点是右操作数的最后节点
        if (this.rightOperand) {
            return this.rightOperand.getLastNode();
        } else if (this.leftOperand) {
            return this.leftOperand.getLastNode();
        }
        
        return this;
    }

    /**
     * 设置左操作数
     */
    setLeftOperand(node) {
        this.leftOperand = node;
        if (node) {
            node.parent = this;
            node.updateTypeMatchInfo();
        }
    }

    /**
     * 设置右操作数
     */
    setRightOperand(node) {
        this.rightOperand = node;
        if (node) {
            node.parent = this;
            node.updateTypeMatchInfo();
        }
    }
}

/**
 * 封装表达式节点类
 */
class EncapsulatedNode extends ValueNode {
    constructor(expression, innerAST) {
        super(expression);
        this.isEncapsulated = true;
        this.metadata = {
            type: 'encapsulated',
            innerAST: innerAST
        };
        
        // 设置内部AST的父引用
        if (innerAST) {
            innerAST.parent = this;
            innerAST.encapsulationLevel = this.encapsulationLevel + 1;
        }
    }

    calculateActualType() {
        if (this.metadata.innerAST) {
            return this.metadata.innerAST.calculateActualType();
        }
        return 'Object';
    }

    getLastNode() {
        // 封装表达式的最后节点是其内部AST的最后节点
        if (this.metadata.innerAST) {
            return this.metadata.innerAST.getLastNode();
        }
        return this;
    }

    getTypeForOptions() {
        // 关键：封装表达式从外部访问时只返回最外层的性质
        if (this.isOutermostEncapsulation()) {
            return this.actualType;
        }
        
        // 如果不是最外层，获取最后一个节点的类型
        const lastNode = this.getLastNode();
        return lastNode.actualType;
    }
}

/**
 * AST工厂类
 */
class ASTFactory {
    static createValueNode(value, returnType) {
        return new ValueNode(value, returnType);
    }

    static createOperatorNode(operator) {
        return new OperatorNode(operator);
    }

    static createEncapsulatedNode(expression, innerAST) {
        return new EncapsulatedNode(expression, innerAST);
    }

    /**
     * 从现有节点创建封装节点
     */
    static encapsulateNode(node, expression) {
        const encapsulated = new EncapsulatedNode(expression, node);
        
        // 保持父子关系
        if (node.parent) {
            encapsulated.parent = node.parent;
            if (node.parent.leftOperand === node) {
                node.parent.leftOperand = encapsulated;
            } else if (node.parent.rightOperand === node) {
                node.parent.rightOperand = encapsulated;
            }
        }
        
        return encapsulated;
    }
}

export { ASTNode, ValueNode, OperatorNode, EncapsulatedNode, ASTFactory };
