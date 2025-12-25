#!/usr/bin/env node

/**
 * DSL编辑器修复验证脚本
 * 验证关键问题的修复效果
 */

import { DslEditor } from './src/components/DslEditor.js';
import { createValueNode, createOperatorNode } from './src/components/dsl_editor/core.js';

class FixVerificationTest {
    constructor() {
        this.editor = new DslEditor();
        this.editor.renderEditor = () => { console.log('  [跳过UI渲染]'); };
        this.editor.showExpressionError = (msg) => { console.log(`  [表达式错误]: ${msg}`); };
    }

    /**
     * 格式化AST节点显示
     */
    formatAST(node) {
        if (!node) return '(null)';
        if (node.type === 'value') {
            const metadata = node.metadata ? ` [${node.metadata.type || 'unknown'}]` : '';
            return `值节点: "${node.value}"${metadata}`;
        } else if (node.type === 'operator') {
            const left = this.formatAST(node.leftOperand);
            const right = this.formatAST(node.rightOperand);
            return `操作符节点: ${node.operator} (左: ${left}, 右: ${right})`;
        }
        return `未知节点类型: ${node.type}`;
    }

    /**
     * 打印当前状态
     */
    printState(title) {
        console.log(`\n=== ${title} ===`);
        console.log(`DSL表达式: "${this.editor.expression || '(空)'}"`);
        console.log(`主表达式AST: ${this.formatAST(this.editor.mainExpressionAST)}`);
        console.log(`工作区AST: ${this.formatAST(this.editor.workspaceAST)}`);
        console.log(`astRoot: ${this.formatAST(this.editor.astRoot)}`);
        console.log(`编辑路径栈深度: ${this.editor.editingPath?.length || 0}`);
        if (this.editor.editingPath && this.editor.editingPath.length > 0) {
            console.log(`路径栈详情:`);
            this.editor.editingPath.forEach((path, index) => {
                console.log(`  [${index}] 槽位: ${path.slot}, 父节点: ${this.formatAST(path.parentNode)}, 描述: ${path.descriptor}`);
            });
        }
        console.log(`表达式完成状态: ${this.editor.isExpressionCompleted}`);
        console.log('=====================================\n');
    }

    /**
     * 测试1: 保存后工作区显示问题
     */
    async testWorkspaceDisplayAfterSave() {
        console.log('🧪 测试1: 保存后工作区显示问题');
        
        // 创建值节点
        const valueOption = {
            type: "staticValue",
            value: "6",
            label: "输入数值",
            returnType: "Int32"
        };
        
        this.editor.addNode(valueOption);
        this.editor.handleStaticValueInput("6");
        this.printState("输入值6后");
        
        // 保存表达式
        this.editor.encapsulateCurrentExpression();
        this.printState("保存后");
        
        // 验证：工作区应该显示 "(6)" 而不是 "6"
        const expectedWorkspace = "(6)";
        const actualWorkspace = this.editor.expression;
        
        if (actualWorkspace === expectedWorkspace) {
            console.log('✅ 测试1通过: 保存后工作区正确显示封装格式');
        } else {
            console.log(`❌ 测试1失败: 期望 "${expectedWorkspace}", 实际 "${actualWorkspace}"`);
        }
        
        return actualWorkspace === expectedWorkspace;
    }

    /**
     * 测试2: 主表达式更新问题
     */
    async testMainExpressionUpdate() {
        console.log('\n🧪 测试2: 主表达式更新问题');
        
        // 点击主表达式进行扩展
        this.editor.expandMainExpression();
        this.printState("扩展主表达式后");
        
        // 添加操作符
        const operatorOption = {
            type: "operator",
            value: ">",
            label: "大于",
            returnType: "Boolean"
        };
        
        this.editor.addNode(operatorOption);
        this.printState("添加操作符后");
        
        // 验证：主表达式应该更新显示操作符结构
        const hasMainExpression = this.editor.mainExpressionAST !== null;
        const mainExpressionContainsOperator = this.editor.mainExpressionAST?.toExpression?.().includes('>') || false;
        
        if (hasMainExpression && mainExpressionContainsOperator) {
            console.log('✅ 测试2通过: 主表达式正确更新显示操作符结构');
        } else {
            console.log(`❌ 测试2失败: 主表达式未正确更新`);
            console.log(`  主表达式存在: ${hasMainExpression}`);
            console.log(`  包含操作符: ${mainExpressionContainsOperator}`);
        }
        
        return hasMainExpression && mainExpressionContainsOperator;
    }

    /**
     * 测试3: 保存操作成功执行
     */
    async testSaveOperation() {
        console.log('\n🧪 测试3: 保存操作成功执行');
        
        // 添加右操作数
        const rightValueOption = {
            type: "staticValue",
            value: "5",
            label: "输入数值",
            returnType: "Int32"
        };
        
        this.editor.addNode(rightValueOption);
        this.editor.handleStaticValueInput("5");
        this.printState("添加右操作数后");
        
        // 尝试保存
        try {
            this.editor.encapsulateCurrentExpression();
            this.printState("保存操作后");
            
            console.log('✅ 测试3通过: 保存操作成功执行');
            return true;
        } catch (error) {
            console.log(`❌ 测试3失败: 保存操作失败 - ${error.message}`);
            return false;
        }
    }

    /**
     * 运行所有测试
     */
    async runAllTests() {
        console.log('🎯 DSL编辑器修复验证测试启动');
        console.log('目标: 验证关键问题的修复效果\n');
        
        const results = [];
        
        try {
            results.push(await this.testWorkspaceDisplayAfterSave());
            results.push(await this.testMainExpressionUpdate());
            results.push(await this.testSaveOperation());
            
            const passedTests = results.filter(r => r).length;
            const totalTests = results.length;
            
            console.log('\n📊 测试结果总结:');
            console.log(`通过测试: ${passedTests}/${totalTests}`);
            
            if (passedTests === totalTests) {
                console.log('🎉 所有测试通过！修复成功！');
            } else {
                console.log('⚠️  部分测试失败，需要进一步修复');
            }
            
        } catch (error) {
            console.error('\n❌ 测试过程中发生错误:', error);
            console.error('错误堆栈:', error.stack);
        }
        
        console.log('\n🏁 修复验证测试完成');
    }
}

// 运行测试
const test = new FixVerificationTest();
test.runAllTests();
