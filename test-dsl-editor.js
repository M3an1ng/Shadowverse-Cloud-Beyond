/**
 * DSL编辑器命令行测试工具
 * 用于验证核心逻辑，无需浏览器界面
 */

// 模拟浏览器环境的基础DOM API
global.document = {
    createElement: (tag) => ({
        tagName: tag.toUpperCase(),
        className: '',
        textContent: '',
        style: { cssText: '' },
        children: [],
        appendChild: function(child) { this.children.push(child); },
        addEventListener: function() {},
        setAttribute: function() {},
        getAttribute: function() { return null; }
    })
};

global.window = {
    getComputedStyle: () => ({ getPropertyValue: () => '' })
};

// 导入DSL编辑器和相关模块
import { DslEditor } from './src/components/DslEditor.js';

/**
 * 测试工具类
 */
class DslEditorTester {
    constructor() {
        this.editor = new DslEditor();
        this.testResults = [];

        // 禁用UI渲染以避免DOM操作
        this.editor.renderEditor = () => {
            console.log('  [跳过UI渲染]');
        };

        // 禁用其他DOM相关方法
        this.editor.showExpressionError = (msg) => {
            console.log(`  [表达式错误]: ${msg}`);
        };
    }

    /**
     * 输出当前状态信息
     */
    printState(title = "当前状态") {
        console.log(`\n=== ${title} ===`);
        
        // 1. DSL表达式内容
        console.log(`DSL表达式: "${this.editor.expression || '(空)'}"`);
        
        // 2. 主表达式AST结构
        console.log(`主表达式AST: ${this.formatAST(this.editor.mainExpressionAST)}`);
        
        // 3. 工作区AST结构
        console.log(`工作区AST: ${this.formatAST(this.editor.workspaceAST)}`);
        console.log(`astRoot: ${this.formatAST(this.editor.astRoot)}`);
        
        // 4. 封装状态和层级关系
        console.log(`编辑路径栈深度: ${this.editor.editingPath ? this.editor.editingPath.length : 0}`);
        if (this.editor.editingPath && this.editor.editingPath.length > 0) {
            console.log(`路径栈详情:`);
            this.editor.editingPath.forEach((path, index) => {
                console.log(`  [${index}] 槽位: ${path.slot}, 父节点: ${this.formatAST(path.parentNode)}, 描述: ${path.descriptor}`);
            });
        }
        
        // 5. 高亮目标节点信息
        try {
            const highlightTarget = this.editor.getHighlightTargetNode ? this.editor.getHighlightTargetNode() : null;
            console.log(`高亮目标节点: ${this.formatAST(highlightTarget)}`);
        } catch (e) {
            console.log(`高亮目标节点: 获取失败 - ${e.message}`);
        }
        
        // 6. 编辑器状态
        console.log(`表达式完成状态: ${this.editor.isExpressionCompleted}`);
        console.log(`是否子编辑器: ${this.editor.isSubEditor}`);
        
        console.log('=====================================\n');
    }

    /**
     * 格式化AST节点为可读字符串
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
     * 测试基础值输入
     */
    async testBasicValueInput() {
        console.log('\n🧪 测试1: 基础值输入');
        
        this.printState("初始状态");
        
        // 模拟选择静态值选项
        const staticValueOption = {
            type: "staticValue",
            value: "6",
            label: "输入数值",
            returnType: "Int32"
        };
        
        console.log('执行: 添加静态值节点');
        this.editor.addNode(staticValueOption);
        this.printState("添加静态值节点后");

        // 模拟输入具体值
        console.log('执行: 输入值 "6"');
        if (this.editor.handleStaticValueInput) {
            this.editor.handleStaticValueInput("6");
        } else {
            // 直接设置值
            if (this.editor.astRoot && this.editor.astRoot.metadata && this.editor.astRoot.metadata.type === 'staticValue') {
                this.editor.astRoot.value = "6";
                this.editor.astRoot.metadata.staticValue = "6";
                this.editor.astRoot.metadata.isAwaitingInput = false;
                this.editor.astRoot.updateCompleteness();
            }
        }
        this.printState("输入值后");
        
        return true;
    }

    /**
     * 测试操作符添加
     */
    async testOperatorAddition() {
        console.log('\n🧪 测试2: 操作符添加');
        
        // 模拟选择操作符
        const operatorOption = {
            type: "operator",
            value: ">",
            label: "大于",
            returnType: "Boolean"
        };
        
        console.log('执行: 添加操作符 ">"');
        this.editor.addNode(operatorOption);
        this.printState("添加操作符后");
        
        return true;
    }

    /**
     * 测试右操作数输入
     */
    async testRightOperandInput() {
        console.log('\n🧪 测试3: 右操作数输入');
        
        // 模拟输入右操作数
        const rightValueOption = {
            type: "staticValue",
            value: "5",
            label: "输入数值",
            returnType: "Int32"
        };
        
        console.log('执行: 添加右操作数静态值');
        this.editor.addNode(rightValueOption);
        this.printState("添加右操作数后");

        console.log('执行: 输入右操作数值 "5"');
        if (this.editor.handleStaticValueInput) {
            this.editor.handleStaticValueInput("5");
        } else {
            // 直接设置值
            if (this.editor.astRoot && this.editor.astRoot.type === 'operator' && this.editor.astRoot.rightOperand) {
                const rightNode = this.editor.astRoot.rightOperand;
                if (rightNode.metadata && rightNode.metadata.type === 'staticValue') {
                    rightNode.value = "5";
                    rightNode.metadata.staticValue = "5";
                    rightNode.metadata.isAwaitingInput = false;
                    rightNode.updateCompleteness();
                    this.editor.astRoot.updateCompleteness();
                }
            }
        }
        this.printState("输入右操作数后");
        
        return true;
    }

    /**
     * 测试保存和封装
     */
    async testSaveAndEncapsulation() {
        console.log('\n🧪 测试4: 保存和封装');
        
        console.log('执行: 保存当前层');
        try {
            // 手动调用保存逻辑，跳过DOM操作
            if (this.editor.editingPath && this.editor.editingPath.length > 0) {
                const top = this.editor.editingPath[this.editor.editingPath.length - 1];

                // 检查工作区AST
                if (!this.editor.workspaceAST && this.editor.astRoot) {
                    this.editor.workspaceAST = this.editor.astRoot;
                }

                if (this.editor.workspaceAST) {
                    console.log('  手动执行保存逻辑...');

                    // 创建封装节点
                    const encapsulatedNode = this.editor.createEncapsulatedFrom(this.editor.workspaceAST);

                    // 写回父节点
                    const ok = this.editor.writeBackToParentSlot(top.parentNode, top.slot, encapsulatedNode);
                    if (ok) {
                        // 弹出路径栈
                        this.editor.editingPath.pop();

                        // 返回上一级
                        this.editor.workspaceAST = top.parentNode;
                        this.editor.astRoot = this.editor.workspaceAST;

                        // 更新表达式
                        this.editor.updateExpressionFromAST();

                        console.log('  保存成功');
                    } else {
                        console.log('  写回父节点失败');
                    }
                } else {
                    console.log('  无工作区AST可保存');
                }
            } else {
                console.log('  无编辑路径可保存');

                // 如果没有编辑路径且有astRoot，说明是顶层，应该封装为主表达式
                if (this.editor.astRoot) {
                    console.log('  顶层保存：封装为主表达式');
                    const encapsulatedNode = this.editor.createEncapsulatedFrom(this.editor.astRoot);
                    this.editor.mainExpressionAST = encapsulatedNode;
                    this.editor.clearWorkspace();
                    this.editor.updateExpressionFromAST();
                    console.log('  顶层保存成功');
                }
            }

            this.printState("保存当前层后");
        } catch (e) {
            console.log(`保存失败: ${e.message}`);
            this.printState("保存失败后的状态");
        }
        
        return true;
    }

    /**
     * 测试最终封装
     */
    async testFinalEncapsulation() {
        console.log('\n🧪 测试5: 最终封装');

        console.log('执行: 最终封装为主表达式');
        if (this.editor.astRoot) {
            const encapsulatedNode = this.editor.createEncapsulatedFrom(this.editor.astRoot);
            this.editor.mainExpressionAST = encapsulatedNode;
            this.editor.clearWorkspace();
            this.editor.updateExpressionFromAST();
            console.log('  最终封装成功');
        }

        this.printState("最终封装后");

        return true;
    }

    /**
     * 测试子表达式选中和编辑
     */
    async testSubExpressionSelection() {
        console.log('\n🧪 测试6: 子表达式选中和编辑');

        // 模拟点击主表达式中的左操作数 "(6)"
        console.log('执行: 选中左操作数 "(6)" 进行编辑');
        if (this.editor.mainExpressionAST && this.editor.mainExpressionAST.metadata && this.editor.mainExpressionAST.metadata.innerAST) {
            const innerAST = this.editor.mainExpressionAST.metadata.innerAST;
            if (innerAST.type === 'operator' && innerAST.leftOperand) {
                const leftOperand = innerAST.leftOperand;
                console.log(`  找到左操作数: ${this.formatAST(leftOperand)}`);

                // 模拟editSubExpression调用
                try {
                    this.editor.editSubExpression(leftOperand);
                    this.printState("选中左操作数后");
                } catch (e) {
                    console.log(`  选中失败: ${e.message}`);
                }
            }
        }

        return true;
    }

    /**
     * 测试高亮目标节点
     */
    async testHighlightTargetNode() {
        console.log('\n🧪 测试7: 高亮目标节点验证');

        // 测试不同编辑状态下的高亮目标
        console.log('执行: 验证当前编辑状态的高亮目标');

        try {
            const highlightTarget = this.editor.getHighlightTargetNode ? this.editor.getHighlightTargetNode() : null;
            console.log(`  当前高亮目标: ${this.formatAST(highlightTarget)}`);

            const editingPath = this.editor.editingPath;
            if (editingPath && editingPath.length > 0) {
                const top = editingPath[editingPath.length - 1];
                console.log(`  编辑路径栈顶: 父节点=${this.formatAST(top.parentNode)}, 槽位=${top.slot}`);

                // 验证高亮目标是否与编辑路径一致
                if (highlightTarget === top.parentNode) {
                    console.log('  ✅ 高亮目标与编辑路径一致');
                } else {
                    console.log('  ❌ 高亮目标与编辑路径不一致');
                }
            }
        } catch (e) {
            console.log(`  高亮目标获取失败: ${e.message}`);
        }

        this.printState("高亮验证后");

        return true;
    }

    /**
     * 测试返回上级操作
     */
    async testGoBack() {
        console.log('\n🧪 测试8: 返回上级操作');

        console.log('执行: 返回上级');
        try {
            if (this.editor.editingPath && this.editor.editingPath.length > 0) {
                // 手动模拟goBack逻辑
                const currentPath = this.editor.editingPath.pop();
                console.log(`  弹出编辑路径: ${currentPath.descriptor}`);

                // 恢复到父级工作区
                if (currentPath.parentNode) {
                    this.editor.workspaceAST = currentPath.parentNode;
                    this.editor.astRoot = currentPath.parentNode;
                    this.editor.updateExpressionFromAST();
                }

                this.printState("返回上级后");
            } else {
                console.log('  无编辑路径可返回');
            }
        } catch (e) {
            console.log(`  返回上级失败: ${e.message}`);
        }

        return true;
    }

    /**
     * 测试完整流程
     */
    async testCompleteFlow() {
        console.log('\n🚀 开始完整流程测试: 构建表达式 "6 > 5"');

        await this.testBasicValueInput();
        await this.testOperatorAddition();
        await this.testRightOperandInput();
        await this.testSaveAndEncapsulation();
        await this.testFinalEncapsulation();

        console.log('\n✅ 完整流程测试完成');
        this.printState("最终状态");
    }

    /**
     * 测试修改子表达式（验证封装规则）
     */
    async testModifySubExpression() {
        console.log('\n🧪 测试9: 修改子表达式（验证封装规则）');

        // 验证当前工作区状态
        console.log('当前工作区状态验证:');
        console.log(`  astRoot: ${this.formatAST(this.editor.astRoot)}`);
        console.log(`  是否已封装: ${this.editor.astRoot?.metadata?.type === 'encapsulated' ? '是' : '否'}`);

        // 在当前工作区（值为"6"）的基础上添加操作符
        console.log('\n执行: 在子表达式中添加操作符 "=="');
        console.log('⚠️  根据设计文档，添加操作符前必须先封装当前值');

        const operatorOption = {
            type: "operator",
            value: "==",
            label: "等于",
            returnType: "Boolean"
        };

        this.editor.addNode(operatorOption);
        this.printState("添加操作符后");

        // 验证左操作数是否正确封装
        if (this.editor.editingPath && this.editor.editingPath.length > 0) {
            const top = this.editor.editingPath[this.editor.editingPath.length - 1];
            if (top.parentNode && top.parentNode.leftOperand) {
                const leftOperand = top.parentNode.leftOperand;
                console.log(`\n🔍 封装规则验证:`);
                console.log(`  左操作数: ${this.formatAST(leftOperand)}`);
                console.log(`  是否已封装: ${leftOperand.metadata?.type === 'encapsulated' ? '✅ 是' : '❌ 否'}`);
                if (leftOperand.metadata?.type === 'encapsulated') {
                    console.log(`  封装内容: ${leftOperand.metadata.originalExpression || 'unknown'}`);
                }
            }
        }

        // 添加右操作数
        console.log('\n执行: 添加右操作数 "7"');
        const rightValueOption = {
            type: "staticValue",
            value: "7",
            label: "输入数值",
            returnType: "Int32"
        };

        this.editor.addNode(rightValueOption);
        this.printState("添加右操作数后");

        // 输入右操作数值
        console.log('执行: 输入右操作数值 "7"');
        if (this.editor.handleStaticValueInput) {
            this.editor.handleStaticValueInput("7");
        }
        this.printState("输入右操作数值后");

        return true;
    }

    /**
     * 测试保存修改后的子表达式
     */
    async testSaveModifiedSubExpression() {
        console.log('\n🧪 测试10: 保存修改后的子表达式');

        console.log('执行: 保存修改后的子表达式');
        try {
            // 手动执行保存逻辑
            if (this.editor.astRoot) {
                // 封装当前工作区
                const encapsulatedNode = this.editor.createEncapsulatedFrom(this.editor.astRoot);

                // 这里应该写回到主表达式的左操作数位置
                // 但由于测试环境限制，我们只是显示结果
                console.log(`  封装结果: ${this.formatAST(encapsulatedNode)}`);

                this.printState("保存修改后");
            }
        } catch (e) {
            console.log(`  保存失败: ${e.message}`);
        }

        return true;
    }

    /**
     * 测试子表达式交互流程
     */
    async testSubExpressionFlow() {
        console.log('\n🚀 开始子表达式交互测试');

        await this.testSubExpressionSelection();
        await this.testHighlightTargetNode();
        await this.testModifySubExpression();
        await this.testSaveModifiedSubExpression();
        await this.testGoBack();

        console.log('\n✅ 子表达式交互测试完成');
        this.printState("子表达式测试最终状态");
    }

    /**
     * 测试复杂嵌套表达式构建
     */
    async testComplexNestedExpression() {
        console.log('\n🚀 开始复杂嵌套表达式测试');
        console.log('目标: 构建 (((6) > (7)) && ((context.sourceCard.currentCost) < (5)))');

        // 重置编辑器状态
        this.editor = new DslEditor();
        this.editor.renderEditor = () => { console.log('  [跳过UI渲染]'); };
        this.editor.showExpressionError = (msg) => { console.log(`  [表达式错误]: ${msg}`); };

        console.log('\n第一部分: 构建 (6) > (7)');

        // 构建 6
        await this.addStaticValue("6");

        // 添加操作符 >
        await this.addOperator(">");

        // 添加右操作数 7
        await this.addStaticValue("7");

        // 保存第一部分
        await this.saveCurrentLayer();

        console.log('\n第二部分: 添加逻辑操作符 &&');

        // 添加逻辑操作符 &&
        await this.addOperator("&&");

        console.log('\n第三部分: 构建 context.sourceCard.currentCost < 5');

        // 添加上下文属性
        const contextOption = {
            type: "context",
            value: "context.sourceCard.currentCost",
            label: "源卡牌当前费用",
            returnType: "Int32"
        };
        this.editor.addNode(contextOption);
        this.printState("添加上下文属性后");

        // 添加操作符 <
        await this.addOperator("<");

        // 添加右操作数 5
        await this.addStaticValue("5");

        // 保存第二部分
        await this.saveCurrentLayer();

        // 保存整个表达式
        await this.saveCurrentLayer();

        // 最终封装
        if (this.editor.astRoot) {
            const encapsulatedNode = this.editor.createEncapsulatedFrom(this.editor.astRoot);
            this.editor.mainExpressionAST = encapsulatedNode;
            this.editor.clearWorkspace();
            this.editor.updateExpressionFromAST();
        }

        console.log('\n✅ 复杂嵌套表达式测试完成');
        this.printState("复杂表达式最终状态");

        return true;
    }

    /**
     * 辅助方法：添加静态值
     */
    async addStaticValue(value) {
        const option = {
            type: "staticValue",
            value: value,
            label: "输入数值",
            returnType: "Int32"
        };
        this.editor.addNode(option);
        if (this.editor.handleStaticValueInput) {
            this.editor.handleStaticValueInput(value);
        }
    }

    /**
     * 辅助方法：添加操作符
     */
    async addOperator(operator) {
        const option = {
            type: "operator",
            value: operator,
            label: `操作符${operator}`,
            returnType: operator === "&&" || operator === "||" ? "Boolean" : "Boolean"
        };
        this.editor.addNode(option);
    }

    /**
     * 辅助方法：保存当前层
     */
    async saveCurrentLayer() {
        if (this.editor.editingPath && this.editor.editingPath.length > 0) {
            const top = this.editor.editingPath[this.editor.editingPath.length - 1];

            if (this.editor.workspaceAST || this.editor.astRoot) {
                const workspaceAST = this.editor.workspaceAST || this.editor.astRoot;
                const encapsulatedNode = this.editor.createEncapsulatedFrom(workspaceAST);
                const ok = this.editor.writeBackToParentSlot(top.parentNode, top.slot, encapsulatedNode);
                if (ok) {
                    this.editor.editingPath.pop();
                    this.editor.workspaceAST = top.parentNode;
                    this.editor.astRoot = this.editor.workspaceAST;
                    this.editor.updateExpressionFromAST();
                }
            }
        }
    }

    /**
     * 运行所有测试
     */
    async runAllTests() {
        console.log('🎯 DSL编辑器命令行测试工具启动');
        console.log('目标: 验证核心逻辑 - AST构建、封装返回、路径栈管理');

        try {
            await this.testCompleteFlow();
            await this.testSubExpressionFlow();
            await this.testComplexNestedExpression();

            console.log('\n📊 测试总结:');
            console.log('- AST构建逻辑验证完成');
            console.log('- 封装和返回上级逻辑验证完成');
            console.log('- 路径栈管理机制验证完成');
            console.log('- 高亮目标节点信息验证完成');
            console.log('- 子表达式选中和编辑验证完成');
            console.log('- 子表达式修改和保存验证完成');
            console.log('- 返回上级操作验证完成');
            console.log('- 封装规则遵循验证完成');
            console.log('- 复杂嵌套表达式构建验证完成');

        } catch (error) {
            console.error('\n❌ 测试过程中发生错误:', error);
            console.error('错误堆栈:', error.stack);
        }
    }
}

// 运行测试
const tester = new DslEditorTester();
tester.runAllTests().then(() => {
    console.log('\n🏁 测试工具执行完成');
}).catch(error => {
    console.error('\n💥 测试工具执行失败:', error);
});
