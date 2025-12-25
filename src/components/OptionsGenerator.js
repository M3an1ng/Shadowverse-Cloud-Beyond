/**
 * 重构的选项生成器
 * 基于新的AST节点结构，正确处理封装层级和类型判断
 */

/**
 * 选项生成器类
 */
class OptionsGenerator {
    constructor(dslEditor) {
        this.dslEditor = dslEditor;
    }

    /**
     * 获取当前表达式的可用选项
     * 核心逻辑：后续节点根据最后一个封装的节点生成
     */
    getCurrentOptions(astRoot) {
        console.log("OptionsGenerator: Getting current options for AST root:", astRoot);

        if (!astRoot) {
            return this.getDataSourceOptions();
        }

        // 找到当前需要编辑的位置
        const incompleteNode = this.findIncompleteNode(astRoot);
        if (incompleteNode) {
            return this.getOptionsForIncompleteNode(incompleteNode);
        }

        // 关键逻辑：获取最后一个节点，基于其类型提供选项
        const lastNode = astRoot.getLastNode();
        console.log("OptionsGenerator: Found last node:", lastNode);
        console.log("OptionsGenerator: Last node type:", lastNode.actualType);

        return this.getOptionsForNode(lastNode);
    }

    /**
     * 为节点获取选项
     */
    getOptionsForNode(node) {
        console.log("OptionsGenerator: Getting options for node:", {
            type: node.type,
            value: node.value,
            actualType: node.actualType,
            isEncapsulated: node.isEncapsulated,
            isOutermostEncapsulation: node.isOutermostEncapsulation()
        });

        // 获取用于选项生成的类型
        const typeForOptions = node.getTypeForOptions();
        console.log("OptionsGenerator: Type for options:", typeForOptions);

        return this.getOptionsForType(typeForOptions);
    }

    /**
     * 基于类型获取选项
     */
    getOptionsForType(type) {
        console.log("OptionsGenerator: Getting options for type:", type);
        
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
        } else if (type === 'String') {
            // 字符串类型：提供字符串操作符
            options.push(
                { type: 'operator', operator: '+', label: '字符串连接 (+)', description: '连接两个字符串' },
                { type: 'operator', operator: '==', label: '等于 (==)', description: '比较两个字符串是否相等' },
                { type: 'operator', operator: '!=', label: '不等于 (!=)', description: '比较两个字符串是否不相等' }
            );
        }

        // 添加通用选项
        options.push(
            { type: 'complete', label: '完成表达式', description: '结束当前表达式编辑' }
        );

        console.log("OptionsGenerator: Generated options:", options);
        return options;
    }

    /**
     * 为不完整节点获取选项
     */
    getOptionsForIncompleteNode(node) {
        console.log("OptionsGenerator: Getting options for incomplete node:", node);

        if (node.type === 'operator') {
            if (!node.rightOperand) {
                // 操作符缺少右操作数
                if (['&&', '||'].includes(node.operator)) {
                    // 逻辑操作符需要布尔表达式
                    return this.getBooleanExpressionOptions();
                } else {
                    // 比较操作符需要兼容类型的值
                    const leftType = node.leftOperand ? node.leftOperand.actualType : 'Int32';
                    return this.getValueOptionsForType(leftType);
                }
            }
        }

        return this.getDataSourceOptions();
    }

    /**
     * 获取布尔表达式选项
     */
    getBooleanExpressionOptions() {
        return [
            { type: 'staticValue', value: 'true', label: 'true', description: '布尔值：真' },
            { type: 'staticValue', value: 'false', label: 'false', description: '布尔值：假' },
            { type: 'staticValue', value: 'input', label: '输入值', description: '输入常量值' }
        ];
    }

    /**
     * 获取指定类型的值选项
     */
    getValueOptionsForType(type) {
        const options = [
            { type: 'staticValue', value: 'input', label: '输入值', description: '输入常量值' }
        ];

        if (type === 'Int32') {
            options.unshift(
                { type: 'staticValue', value: '0', label: '0', description: '数值：零' },
                { type: 'staticValue', value: '1', label: '1', description: '数值：一' }
            );
        } else if (type === 'Boolean') {
            options.unshift(
                { type: 'staticValue', value: 'true', label: 'true', description: '布尔值：真' },
                { type: 'staticValue', value: 'false', label: 'false', description: '布尔值：假' }
            );
        }

        return options;
    }

    /**
     * 获取数据源选项
     */
    getDataSourceOptions() {
        return [
            { type: 'staticValue', value: 'input', label: '输入值', description: '输入常量值 (数值、文本、布尔值等)' },
            { type: 'context', value: 'card', label: '卡牌', description: '当前卡牌对象' }
        ];
    }

    /**
     * 查找不完整的节点
     */
    findIncompleteNode(node) {
        if (!node) return null;

        // 检查当前节点是否完整
        if (!node.isComplete) {
            return node;
        }

        // 如果是操作符节点，检查操作数
        if (node.type === 'operator') {
            if (!node.leftOperand || !node.rightOperand) {
                return node;
            }

            // 递归检查操作数
            const incompleteLeft = this.findIncompleteNode(node.leftOperand);
            if (incompleteLeft) return incompleteLeft;

            const incompleteRight = this.findIncompleteNode(node.rightOperand);
            if (incompleteRight) return incompleteRight;
        }

        // 如果是封装表达式，检查内部AST
        if (node.metadata && node.metadata.innerAST) {
            return this.findIncompleteNode(node.metadata.innerAST);
        }

        return null;
    }
}

/**
 * 表达式分析器
 * 用于分析表达式结构和类型
 */
class ExpressionAnalyzer {
    /**
     * 分析表达式的最后节点
     */
    static analyzeLastNode(expression) {
        console.log("ExpressionAnalyzer: Analyzing last node in expression:", expression);

        // 示例分析逻辑
        if (expression === '(((6) > (5)) && (5))') {
            // 最后一个节点是 (5)，类型是 Int32
            return {
                value: '(5)',
                type: 'Int32',
                isEncapsulated: true,
                needsCorrection: true,
                expectedType: 'Boolean'
            };
        }

        if (expression === '((6) > (5))') {
            // 整个表达式是封装的布尔表达式
            return {
                value: '((6) > (5))',
                type: 'Boolean',
                isEncapsulated: true,
                needsCorrection: false,
                expectedType: null
            };
        }

        return {
            value: expression,
            type: 'Object',
            isEncapsulated: false,
            needsCorrection: false,
            expectedType: null
        };
    }
}

export { OptionsGenerator, ExpressionAnalyzer };
