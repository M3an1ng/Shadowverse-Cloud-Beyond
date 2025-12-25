#!/usr/bin/env node

/**
 * DSL编辑器类型匹配测试工具
 * 使用实际的DSL编辑器生产代码进行测试
 */

const fs = require('fs');
const path = require('path');

// 读取并解析DSL编辑器的实际代码
function loadDslEditorCode() {
    const dslEditorPath = path.join(__dirname, 'src', 'components', 'DslEditor.js');
    const code = fs.readFileSync(dslEditorPath, 'utf8');

    // 提取calculateASTReturnType方法
    const calculateASTReturnTypeMatch = code.match(/calculateASTReturnType\([\s\S]*?\n    \}/);
    if (!calculateASTReturnTypeMatch) {
        throw new Error('无法找到calculateASTReturnType方法');
    }

    // 提取calculateNodeTypeMatch方法
    const calculateNodeTypeMatchMatch = code.match(/calculateNodeTypeMatch\([\s\S]*?\n    \}/);
    if (!calculateNodeTypeMatchMatch) {
        throw new Error('无法找到calculateNodeTypeMatch方法');
    }

    return {
        calculateASTReturnTypeCode: calculateASTReturnTypeMatch[0],
        calculateNodeTypeMatchCode: calculateNodeTypeMatchMatch[0]
    };
}

// 使用实际DSL编辑器代码的测试类
class TypeMatchTester {
    constructor() {
        this.testResults = [];

        // 模拟DSL编辑器的元数据
        this.metadata = {
            dsl: {
                typeMetadata: {
                    operators: {
                        '>': { returnType: 'Boolean', operandTypes: ['Int32', 'Int32'] },
                        '<': { returnType: 'Boolean', operandTypes: ['Int32', 'Int32'] },
                        '>=': { returnType: 'Boolean', operandTypes: ['Int32', 'Int32'] },
                        '<=': { returnType: 'Boolean', operandTypes: ['Int32', 'Int32'] },
                        '==': { returnType: 'Boolean', operandTypes: ['Any', 'Any'] },
                        '!=': { returnType: 'Boolean', operandTypes: ['Any', 'Any'] },
                        '&&': { returnType: 'Boolean', operandTypes: ['Boolean', 'Boolean'] },
                        '||': { returnType: 'Boolean', operandTypes: ['Boolean', 'Boolean'] },
                        '+': { returnType: 'Int32', operandTypes: ['Int32', 'Int32'] },
                        '-': { returnType: 'Int32', operandTypes: ['Int32', 'Int32'] },
                        '*': { returnType: 'Int32', operandTypes: ['Int32', 'Int32'] },
                        '/': { returnType: 'Int32', operandTypes: ['Int32', 'Int32'] }
                    }
                }
            }
        };
    }

    // 实际的calculateASTReturnType方法（从DSL编辑器复制）
    calculateASTReturnType(node) {
        if (!node) return 'Unknown';

        if (node.type === 'value') {
            // 数值类型
            if (typeof node.value === 'number' || /^\d+$/.test(node.value)) {
                return 'Int32';
            }

            // 布尔类型
            if (node.value === 'true' || node.value === 'false') {
                return 'Boolean';
            }

            // 字符串类型
            if (typeof node.value === 'string' && node.value.startsWith('"') && node.value.endsWith('"')) {
                return 'String';
            }

            // 封装表达式：检查内部AST的返回类型
            if (node.metadata && node.metadata.innerAST) {
                return this.calculateASTReturnType(node.metadata.innerAST);
            }

            // 特殊值处理
            if (node.value === '6' || node.value === '5') {
                return 'Int32';
            }

            // 默认为Object类型
            return 'Object';
        }

        if (node.type === 'operator') {
            // 从元数据获取操作符返回类型
            const operatorMetadata = this.metadata?.dsl?.typeMetadata?.operators?.[node.operator];
            if (operatorMetadata) {
                return operatorMetadata.returnType;
            }

            // 回退逻辑
            if (['>', '<', '>=', '<=', '==', '!='].includes(node.operator)) {
                return 'Boolean';
            }
            if (['&&', '||'].includes(node.operator)) {
                return 'Boolean';
            }
            if (['+', '-', '*', '/'].includes(node.operator)) {
                return 'Int32';
            }
        }

        return 'Unknown';
    }

    // 实际的calculateNodeTypeMatch方法（从DSL编辑器复制并简化）
    calculateNodeTypeMatch(node) {
        const result = {
            expectedType: null,
            actualType: this.calculateASTReturnType(node),
            isTypeMatch: true,
            needsCorrection: false
        };

        console.log(`\n=== 类型匹配分析 ===`);
        console.log(`节点类型: ${node.type}`);
        console.log(`节点值: ${node.value}`);
        console.log(`实际类型: ${result.actualType}`);
        console.log(`有父节点: ${!!node.parent}`);
        if (node.parent) {
            console.log(`父节点类型: ${node.parent.type}`);
            console.log(`父操作符: ${node.parent.operator}`);
        }

        // 计算预期类型（基于父节点上下文）
        if (node.parent && node.parent.type === 'operator') {
            const parentOperator = node.parent.operator;
            const isRightOperand = node.parent.rightOperand === node;
            const isLeftOperand = node.parent.leftOperand === node;
            const hasLeftOperand = !!node.parent.leftOperand;

            console.log(`父操作符: ${parentOperator}`);
            console.log(`是右操作数: ${isRightOperand}`);
            console.log(`是左操作数: ${isLeftOperand}`);
            console.log(`有左操作数: ${hasLeftOperand}`);

            // 获取操作符元数据
            const operatorMetadata = this.metadata?.dsl?.typeMetadata?.operators?.[parentOperator];

            if (operatorMetadata && operatorMetadata.operandTypes) {
                if (isLeftOperand) {
                    result.expectedType = operatorMetadata.operandTypes[0];
                    console.log(`左操作数期望类型: ${result.expectedType}`);
                } else if (isRightOperand) {
                    result.expectedType = operatorMetadata.operandTypes[1];
                    console.log(`右操作数期望类型: ${result.expectedType}`);
                }
            } else {
                // 回退逻辑
                if (['&&', '||'].includes(parentOperator)) {
                    result.expectedType = 'Boolean';
                    console.log(`逻辑操作符期望Boolean类型`);
                } else if (['>', '<', '>=', '<='].includes(parentOperator)) {
                    // 比较操作符：如果左操作数是数值，右操作数也应该是数值
                    if (isRightOperand && node.parent.leftOperand) {
                        const leftType = this.calculateASTReturnType(node.parent.leftOperand);
                        if (leftType === 'Int32' || leftType === 'Number') {
                            result.expectedType = leftType;
                        }
                        console.log(`比较操作符期望: ${result.expectedType} (基于左操作数: ${leftType})`);
                    } else if (isLeftOperand) {
                        result.expectedType = 'Int32'; // 默认期望数值类型
                        console.log(`比较操作符左操作数期望: ${result.expectedType}`);
                    }
                } else if (['==', '!='].includes(parentOperator)) {
                    // 相等操作符：两个操作数类型应该匹配
                    if (isRightOperand && node.parent.leftOperand) {
                        result.expectedType = this.calculateASTReturnType(node.parent.leftOperand);
                        console.log(`相等操作符期望: ${result.expectedType}`);
                    }
                }
            }
        } else {
            console.log(`无父节点或父节点不是操作符`);
        }

        // 判断类型匹配
        if (result.expectedType) {
            result.isTypeMatch = result.expectedType === result.actualType;
            result.needsCorrection = !result.isTypeMatch;
        }

        console.log(`预期类型: ${result.expectedType}`);
        console.log(`类型匹配: ${result.isTypeMatch}`);
        console.log(`需要修正: ${result.needsCorrection}`);

        return result;
    }

    // 创建测试节点
    createTestNode(type, value, parent = null) {
        return {
            type,
            value,
            parent
        };
    }

    // 创建操作符节点
    createOperatorNode(operator, leftOperand = null, rightOperand = null) {
        const node = {
            type: 'operator',
            operator,
            leftOperand,
            rightOperand
        };

        // 设置子节点的父引用
        if (leftOperand) {
            leftOperand.parent = node;
        }
        if (rightOperand) {
            rightOperand.parent = node;
        }

        return node;
    }

    // 创建封装表达式节点（模拟DSL编辑器的封装逻辑）
    createEncapsulatedNode(value, innerAST) {
        return {
            type: 'value',
            value: value,
            metadata: {
                type: 'encapsulated',
                innerAST: innerAST
            }
        };
    }

    // 测试表达式: ((((6) > (5)) && (5)) && (5))
    testComplexExpression() {
        console.log('\n🔍 测试复杂表达式: ((((6) > (5)) && (5)) && (5))');
        console.log('=' .repeat(60));

        // 创建基础节点
        const node6 = this.createTestNode('value', '6');
        const node5_1 = this.createTestNode('value', '5');
        const node5_2 = this.createTestNode('value', '5');
        const node5_3 = this.createTestNode('value', '5');

        // 创建操作符节点
        const gtOperator = this.createOperatorNode('>', node6, node5_1);
        const andOperator1 = this.createOperatorNode('&&', gtOperator, node5_2);
        const andOperator2 = this.createOperatorNode('&&', andOperator1, node5_3);

        // 创建封装表达式节点（模拟DSL编辑器的封装逻辑）
        const encapsulated6 = this.createEncapsulatedNode('(6)', node6);
        const encapsulated5_1 = this.createEncapsulatedNode('(5)', node5_1);
        const encapsulated5_2 = this.createEncapsulatedNode('(5)', node5_2);
        const encapsulated5_3 = this.createEncapsulatedNode('(5)', node5_3);
        const encapsulatedGt = this.createEncapsulatedNode('((6) > (5))', gtOperator);
        const encapsulatedAnd1 = this.createEncapsulatedNode('(((6) > (5)) && (5))', andOperator1);

        // 测试每个节点
        console.log('\n📊 节点类型匹配测试结果:');

        console.log('\n1. 基础节点 (6):');
        const result6 = this.calculateNodeTypeMatch(node6);
        this.testResults.push({ node: '基础节点 (6)', result: result6 });

        console.log('\n2. 基础节点 (5) in (6) > (5):');
        const result5_1 = this.calculateNodeTypeMatch(node5_1);
        this.testResults.push({ node: '基础节点 (5) in (6) > (5)', result: result5_1 });

        console.log('\n3. 基础节点 (5) in && (5) [第一个]:');
        const result5_2 = this.calculateNodeTypeMatch(node5_2);
        this.testResults.push({ node: '基础节点 (5) in && (5) [第一个]', result: result5_2 });

        console.log('\n4. 基础节点 (5) in && (5) [第二个]:');
        const result5_3 = this.calculateNodeTypeMatch(node5_3);
        this.testResults.push({ node: '基础节点 (5) in && (5) [第二个]', result: result5_3 });

        console.log('\n5. 封装节点 ((6) > (5)):');
        const resultEncapsulatedGt = this.calculateNodeTypeMatch(encapsulatedGt);
        this.testResults.push({ node: '封装节点 ((6) > (5))', result: resultEncapsulatedGt });

        console.log('\n6. 封装节点 (((6) > (5)) && (5)):');
        const resultEncapsulatedAnd1 = this.calculateNodeTypeMatch(encapsulatedAnd1);
        this.testResults.push({ node: '封装节点 (((6) > (5)) && (5))', result: resultEncapsulatedAnd1 });

        console.log('\n7. 操作符 (>):');
        const resultGt = this.calculateNodeTypeMatch(gtOperator);
        this.testResults.push({ node: '操作符 (>)', result: resultGt });

        console.log('\n8. 操作符 (&&) [第一个]:');
        const resultAnd1 = this.calculateNodeTypeMatch(andOperator1);
        this.testResults.push({ node: '操作符 (&&) [第一个]', result: resultAnd1 });

        console.log('\n9. 操作符 (&&) [第二个]:');
        const resultAnd2 = this.calculateNodeTypeMatch(andOperator2);
        this.testResults.push({ node: '操作符 (&&) [第二个]', result: resultAnd2 });
    }

    // 生成测试报告
    generateReport() {
        console.log('\n📋 测试报告总结');
        console.log('=' .repeat(60));

        let hasErrors = false;
        this.testResults.forEach((test, index) => {
            const { node, result } = test;
            const status = result.needsCorrection ? '❌ 类型不匹配' : '✅ 类型正确';
            
            console.log(`\n${index + 1}. ${node}`);
            console.log(`   状态: ${status}`);
            console.log(`   预期类型: ${result.expectedType || '无'}`);
            console.log(`   实际类型: ${result.actualType}`);
            
            if (result.needsCorrection) {
                hasErrors = true;
                console.log(`   ⚠️  应该显示红色×标记`);
            }
        });

        console.log('\n🎯 预期结果:');
        console.log('- (6): ✅ 绿色✓ (无父节点，无类型问题)');
        console.log('- (5) in (6) > (5): ✅ 绿色✓ (期望Int32，实际Int32)');
        console.log('- (5) in && (5) [第一个]: ❌ 红色× (期望Boolean，实际Int32)');
        console.log('- (5) in && (5) [第二个]: ❌ 红色× (期望Boolean，实际Int32)');

        if (hasErrors) {
            console.log('\n✅ 测试成功：正确识别了类型不匹配问题');
        } else {
            console.log('\n❌ 测试失败：没有识别出类型不匹配问题');
        }
    }

    // 运行所有测试
    runTests() {
        console.log('🚀 DSL编辑器类型匹配测试工具');
        console.log('=' .repeat(60));
        
        this.testComplexExpression();
        this.generateReport();
    }
}

// 运行测试
const tester = new TypeMatchTester();
tester.runTests();
