// src/components/DslEditor.js - 可视化DSL编辑器
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md (第4节)
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md (第6节)
//

import { MetadataDrivenComponent } from "./MetadataDrivenComponent.js";

import { pushPath, popPath, peekPath, clearPath as clearPathStack, writeBackToParentSlot as writeBackSlot } from "./dsl_editor/path.js";
import { getOptionsForType as getOptionsForTypeFromModule } from "./dsl_editor/operators.js";
import { getSmartOperators as getSmartOperatorsFromModule, getTextOperators as getTextOperatorsFromModule, getEnumOperators as getEnumOperatorsFromModule } from "./dsl_editor/operators.js";

import { renderMainExpressionUI } from "./dsl_editor/ui-main.js";

import { renderWorkspaceUI } from "./dsl_editor/ui-workspace.js";
import { createValueNode, createOperatorNode, createFunctionCallNode, createListQueryNode, createListSortNode, createListTakeNode, inferOperatorReturnType, validateAST, findIncompleteNode, findRightmostLeafNode } from "./dsl_editor/core.js";
import { PIPELINE_STAGE_META, isListOperationNode, extractListPipeline, rebuildListChainFromStages, createStageNode } from "./dsl_editor/pipeline.js";
import { getUserSession } from "../userSession.js";



/**
 * 可视化DSL编辑器
 * 支持上下文感知的链式构建DSL表达式
 */
export class DslEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.expression = "";

        // 统一架构：引入虚拟根节点
        this.virtualRoot = { type: 'virtualRoot', main: null };
        this.workspaceAST = null;
        this.astRoot = null;
        this.isInitialized = false;
        this.currentNode = null;
        this.selectedNode = null;
        this.editingPath = [];
        this.isExpressionCompleted = false;
        this.onChange = null;
        this.container = null;
        this.currentOptions = []; // 当前可用的选项
        this.optionsContainer = null; // 选项容器的引用
        this.editorContainer = null; // 编辑器容器的引用
        this.expressionContainer = null; // 表达式显示容器的引用
        this.mainExpressionContainer = null; // 主表达式显示容器的引用
        this.lastEmittedExpression = null; // 最近一次派发给外部的表达式
        this.userId = null;
        this.favoriteState = {
            query: "",
            items: [],
            recentItems: [],
            loading: false,
            error: null
        };
        this.favoriteSearchTimer = null;
        this.favoriteMenuAnchor = null;
        this.favoriteMenuOutsideHandler = null;
        this.pipelineOpen = false;
        this.pipelineOpenSource = null;
        this.pipelineEditState = {
            node: null,
            slot: null,
            editor: null,
            draftExpression: ""
        };
        this.parseStatus = {
            state: "idle",
            message: "",
            errors: []
        };
    }

    normalizeTypeName(typeName) {
        if (!typeName) return null;
        const trimmed = String(typeName).trim();
        if (!trimmed) return null;
        const nullable = trimmed.match(/^Nullable<(.+)>$/);
        if (nullable) return nullable[1];
        if (trimmed === 'List') return 'List<Object>';
        if (trimmed === 'IEnumerable') return 'IEnumerable<Object>';
        return trimmed;
    }

        // ---------------------------
        // 编辑路径与状态管理（Phase A）
        // ---------------------------
        /**
         * 压栈：记录从主表达式到当前工作区节点的路径
         * @param {{ parentNode: any, slot: 'left'|'right'|'self', descriptor?: string, encapsulated?: boolean, index?: number }} entry
         */
        pushEditingPath(entry) {
            if (!this.editingPath) this.editingPath = [];
            // 委托给模块（保留日志以便定位）
            try {
                pushPath(this.editingPath, entry);
            } catch (e) {
                // 回退策略：本地实现
                this.editingPath.push(entry);
            }
            console.log('DslEditor: pushEditingPath ->', entry);
        }

        /**
         * 出栈：返回上一级编辑
         * @returns {object|undefined}
         */
        popEditingPath() {
            if (!this.editingPath) this.editingPath = [];
            let entry;
            try {
                entry = popPath(this.editingPath);
            } catch (e) {
                entry = this.editingPath.length > 0 ? this.editingPath.pop() : undefined;
            }
            console.log('DslEditor: popEditingPath <-', entry);
            return entry;
        }

        /**
         * 计算主表达式中需要高亮的目标节点（基于编辑路径栈顶）
         */
        getHighlightTargetNode() {
            const top = this.peekEditingPath ? this.peekEditingPath() : null;
            if (!top || !top.parentNode) return null;
            if (top.slot === 'main') return top.parentNode.main || null;
            if (top.slot === 'left') return top.parentNode.leftOperand || null;
            if (top.slot === 'right') return top.parentNode.rightOperand || null;
            if (top.slot === 'arg') {
                const argIndex = typeof top.index === 'number' ? top.index : 0;
                return (top.parentNode.args || [])[argIndex] || null;
            }
            if (top.slot === 'filterExpression') return top.parentNode.filterExpression || null;
            if (top.slot === 'sortExpression') return top.parentNode.sortExpression || null;
            if (top.slot === 'countExpression') return top.parentNode.countExpression || null;
            // self 表示编辑的就是该节点本身
            return top.parentNode || null;
        }

        /**
         * 查看当前栈顶
         */
        peekEditingPath() {
            if (!this.editingPath) this.editingPath = [];
            try {
                return peekPath(this.editingPath);
            } catch (e) {
                return this.editingPath.length === 0 ? undefined : this.editingPath[this.editingPath.length - 1];
            }
        }

        /** 清空编辑路径 */
        clearEditingPath() {
            try {
                clearPathStack(this.editingPath);
            } catch (e) {
                this.editingPath = [];
            }
            console.log('DslEditor: clearEditingPath');
        }

        /**
         * 保持 astRoot 始终指向工作区 AST
         */
        ensureAstRootSync() {
            if (this.astRoot !== this.workspaceAST) {
                this.astRoot = this.workspaceAST;
                console.log('DslEditor: ensureAstRootSync -> astRoot reassigned to workspaceAST');
            }
        }

        /**
         * 将封装后的 newValueNode 写回父节点指定槽位
         * 注意：slot='self' 需要由上一层路径项决定如何替换，此处只处理 left/right
         */
        writeBackToParentSlot(parentNode, slot, newValueNode) {
            try {
                return writeBackSlot(parentNode, slot, newValueNode);
            } catch (e) {
                if (!parentNode || !newValueNode) return false;
                if (slot === 'left' && typeof parentNode.setLeftOperand === 'function') {
                    parentNode.setLeftOperand(newValueNode);
                    return true;
                }
                if (slot === 'right' && typeof parentNode.setRightOperand === 'function') {
                    parentNode.setRightOperand(newValueNode);
                    return true;
                }
                if (slot === 'self') {
                    console.warn('DslEditor: writeBackToParentSlot(slot=self) requires upper-level path context.');
                    return false;
                }
                console.warn('DslEditor: writeBackToParentSlot failed: invalid slot or parentNode');
                return false;
            }
        }

    /**
     * 初始化或重置工作区为一个空的、待填充的封装表达式
     */
    initializeWorkspace() {
        // 1. 创建一个空的封装节点
        const emptyEncapsulatedNode = this.createEncapsulatedFrom(
            createValueNode("", "Void", { type: 'empty' })
        );

        // 2. 将其设置到虚拟根节点的 main 槽位
        this.virtualRoot.main = emptyEncapsulatedNode;

        console.log("DslEditor: Workspace initialized with a virtual root.");

        // 3. 统一入口：立即进入对 main 槽位的编辑
        this.handleEnterEditModeForSlot(this.virtualRoot, 'main');
    }


    // 已迁移到 core.js 模块

    // 已迁移到 core.js 模块

    /**
     * 获取当前编辑位置的可用选项
     */
    getCurrentEditingOptions() {
        console.log("DslEditor: getCurrentEditingOptions called");
        console.log("DslEditor: astRoot:", this.astRoot);
        console.log("DslEditor: isExpressionCompleted:", this.isExpressionCompleted);

        if (!this.astRoot || (this.astRoot.type === 'value' && this.astRoot.metadata?.type === 'empty')) {
            // 空表达式，提供数据源选项
            console.log("DslEditor: No AST root or empty node, returning data source options");
            const opts = this.getDataSourceOptions();
            // 关键 UX：即使存在 expectedType，也不能在“起点选择”阶段做严格过滤。
            // 例如 expectedType=Boolean 时，玩家需要从上下文对象/事件字段起步，再通过比较/逻辑运算构造布尔表达式。
            return opts;
        }

        // 找到当前需要编辑的位置
        const incompleteNode = findIncompleteNode(this.astRoot);
        console.log("DslEditor: Found incomplete node:", incompleteNode);
        if (incompleteNode) {
            const options = this.getOptionsForIncompleteNode(incompleteNode);
            console.log("DslEditor: Returning options for incomplete node:", options);
            return options;
        }

        // 关键修复：基于整个表达式的根节点来决定后续选项
        console.log("DslEditor: Using root node for options:", this.astRoot);
        console.log("DslEditor: Root node structure:", {
            type: this.astRoot.type,
            value: this.astRoot.value,
            returnType: this.astRoot.returnType,
            metadata: this.astRoot.metadata,
            expression: this.astRoot.toExpression?.() || null
        });

        // 使用根节点获取选项，这样可以基于整个表达式的返回类型来决定后续操作
        const options = this.getOptionsForCompleteNode(this.astRoot);
        console.log("DslEditor: Returning options for complete expression:", options);
        return options;
    }

    // 已迁移到 core.js 模块

    // 已迁移到 core.js 模块

    /**
     * 为不完整节点获取选项
     */
    getOptionsForIncompleteNode(node) {
        if (node.type === 'functionCall') {
            // If a function call is incomplete, guide the user to fill arguments.
            const params = node.parameters || [];
            const args = node.args || [];
            const argIndex = params.findIndex((p, i) => !p?.isOptional && !args[i]);
            if (argIndex >= 0) {
                this.slotExpectedType = this.normalizeTypeName(params[argIndex]?.typeName) || null;
            }
            return this.getDataSourceOptions();
        }
        if (node.type === 'operator') {
            if (!node.rightOperand) {
                // 操作符缺少右操作数
                if (this.isLogicalOperator(node.operator)) {
                    // 逻辑操作符需要布尔表达式
                    return this.getBooleanExpressionOptions();
                } else {
                    // 比较操作符需要兼容类型的值
                    return this.getDataSourceOptions();
                }
            }
        }

        if (node.type === 'listQuery') {
            this.slotExpectedType = 'Boolean';
            if (!this.contextOverride?.name) {
                const innerType = this.getCollectionInnerType(node.source?.returnType);
                if (innerType) {
                    this.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
                }
            }
            return this.getBooleanExpressionOptions();
        }

        if (node.type === 'listSort') {
            if (!this.contextOverride?.name) {
                const innerType = this.getCollectionInnerType(node.source?.returnType);
                if (innerType) {
                    this.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
                }
            }
            return this.getDataSourceOptions();
        }

        if (node.type === 'listTake') {
            this.slotExpectedType = 'Int32';
            return this.getDataSourceOptions();
        }

        // 默认提供数据源选项
        return this.getDataSourceOptions();
    }

    /**
     * 为完整节点获取后续选项
     */
    getOptionsForCompleteNode(node) {
        console.log("DslEditor: getOptionsForCompleteNode called with node:", node);

        // 关键修复：基于表达式的最后一个节点来提供选项
        const lastNode = this.findLastNodeInExpression(node);
        console.log("DslEditor: Found last node:", lastNode);

        if (lastNode && lastNode !== node) {
            // 如果最后一个节点不是根节点，基于最后节点的类型提供选项
            const lastNodeType = this.getNodeActualReturnType(lastNode);
            console.log("DslEditor: Last node type:", lastNodeType);
            console.log("DslEditor: Providing options based on last node type:", lastNodeType);

            return this.getOptionsForType(lastNodeType);
        }

        // 回退：检查是否是封装的表达式或操作符节点
        const isEncapsulated = node.metadata && node.metadata.type === "encapsulated";
        const isOperatorNode = node.type === 'operator';
        console.log("DslEditor: Node is encapsulated:", isEncapsulated, "isOperatorNode:", isOperatorNode);

        if (isEncapsulated || isOperatorNode) {
            // 封装的表达式或操作符节点：基于实际返回类型提供适当的操作符
            const actualReturnType = this.getNodeActualReturnType(node);
            console.log("DslEditor: Encapsulated/Operator expression return type:", actualReturnType);
            console.log("DslEditor: Providing options based on actual return type:", actualReturnType);

            return this.getOptionsForType(actualReturnType);
        } else {
            // 未封装的表达式：只能继续属性访问或完成表达式
            const actualReturnType = this.getNodeActualReturnType(node);
            console.log("DslEditor: Non-encapsulated expression return type:", actualReturnType);

            // 只提供属性访问选项（如果有的话）
            const typeOptions = getOptionsForTypeFromModule(this, actualReturnType);

            if (this.isCollectionType(actualReturnType)) {
                // 集合类型不过滤，返回所有可用操作（包括筛选、排序等）
                console.log("DslEditor: Returning all options for collection type:", typeOptions);
                return typeOptions;
            } else {
                // 其他未封装类型，只允许属性和函数访问
                const filteredOptions = typeOptions.filter(option =>
                    option.type === 'property' || option.type === 'function'
                );
                console.log("DslEditor: Filtered options (properties/functions only):", filteredOptions);
                return filteredOptions;
            }
        }
    }



    /**
     * 找到表达式中的最后一个节点
     */
    findLastNodeInExpression(node) {
        if (!node) return null;

        console.log("DslEditor: Finding last node in expression:", node);

        // 关键修复：如果是封装表达式且没有父节点，说明它是完整的表达式，不应该查找内部
        if (node.metadata && node.metadata.type === "encapsulated" && !node.parent) {
            console.log("DslEditor: Complete encapsulated expression, returning itself:", node);
            return node;
        }

        // 如果是操作符节点，找到最右边的操作数
        if (node.type === 'operator') {
            if (node.rightOperand) {
                console.log("DslEditor: Operator node, checking right operand:", node.rightOperand);
                // 递归查找右操作数中的最后一个节点
                return this.findLastNodeInExpression(node.rightOperand);
            } else if (node.leftOperand) {
                console.log("DslEditor: Operator node, no right operand, checking left operand:", node.leftOperand);
                return this.findLastNodeInExpression(node.leftOperand);
            }
        }

        // 如果是封装表达式且有父节点，检查其内部AST
        if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
            console.log("DslEditor: Encapsulated node with parent, checking inner AST:", node.metadata.innerAST);
            return this.findLastNodeInExpression(node.metadata.innerAST);
        }

        // 如果是值节点，就是最后一个节点
        console.log("DslEditor: Found last node (value node):", node);
        return node;
    }

    /**
     * 基于类型获取选项
     */
    getOptionsForType(type) {
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
            const typeOptions = getOptionsForTypeFromModule(this, type);
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

    /**
     * 获取节点的实际返回类型（处理封装表达式）
     */
    getNodeActualReturnType(node) {
        if (!node) return 'Object';

        // 如果是封装的表达式，使用其内部AST的返回类型
        if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
            return this.calculateASTReturnType(node.metadata.innerAST);
        }

        // 否则计算节点的实际返回类型
        return this.calculateASTReturnType(node);
    }

    /**
     * 计算节点的预期类型和实际类型
     */
    calculateNodeTypeMatch(node, externalExpectedType = null) {
        const result = {
            expectedType: null,
            actualType: null,
            isTypeMatch: true,
            needsCorrection: false
        };

        if (!node) {
            console.log("DslEditor: calculateNodeTypeMatch - node is null");
            return result;
        }

        console.log("DslEditor: calculateNodeTypeMatch - analyzing node:", {
            type: node.type,
            value: node.value,
            parent: node.parent ? {type: node.parent.type, operator: node.parent.operator} : null
        });

        // 计算实际类型
        result.actualType = this.calculateASTReturnType(node);
        console.log("DslEditor: calculateNodeTypeMatch - actual type:", result.actualType);

        const hasEffectiveParent = node.parent && node.parent.type !== 'virtualRoot';

        // 计算预期类型（基于父节点上下文）
        if (node.parent && node.parent.type === 'operator') {
            const parentOperator = node.parent.operator;
            const isRightOperand = node.parent.rightOperand === node;

            console.log("DslEditor: calculateNodeTypeMatch - parent context:", {
                parentOperator,
                isRightOperand,
                hasLeftOperand: !!node.parent.leftOperand
            });

            // 根据操作符确定预期类型
            if (['&&', '||'].includes(parentOperator)) {
                result.expectedType = 'Boolean';
                console.log("DslEditor: calculateNodeTypeMatch - logical operator expects Boolean");
            } else if (['>', '<', '>=', '<='].includes(parentOperator)) {
                // 比较操作符：如果左操作数是数值，右操作数也应该是数值
                if (isRightOperand && node.parent.leftOperand) {
                    const leftType = this.calculateASTReturnType(node.parent.leftOperand);
                    if (leftType === 'Int32' || leftType === 'Number') {
                        result.expectedType = leftType;
                    }
                    console.log("DslEditor: calculateNodeTypeMatch - comparison operator expects:", result.expectedType);
                }
            } else if (['==', '!='].includes(parentOperator)) {
                // 相等操作符：两个操作数类型应该匹配
                if (isRightOperand && node.parent.leftOperand) {
                    result.expectedType = this.calculateASTReturnType(node.parent.leftOperand);
                    console.log("DslEditor: calculateNodeTypeMatch - equality operator expects:", result.expectedType);
                }
            }
        } else if (node.parent && node.parent.type === 'functionCall') {
            const parent = node.parent;
            const args = parent.args || [];
            const index = args.indexOf(node);
            const p = (parent.parameters || [])[index];
            if (p?.typeName) {
                result.expectedType = this.normalizeTypeName(p.typeName);
                console.log("DslEditor: calculateNodeTypeMatch - functionCall arg expects:", result.expectedType);
            }
        } else if (node.parent && node.parent.type === 'listQuery') {
            if (node.parent.filterExpression === node) {
                result.expectedType = 'Boolean';
                console.log("DslEditor: calculateNodeTypeMatch - listQuery filter expects Boolean");
            }
        } else if (node.parent && node.parent.type === 'listTake') {
            if (node.parent.countExpression === node) {
                result.expectedType = 'Int32';
                console.log("DslEditor: calculateNodeTypeMatch - listTake count expects Int32");
            }
        } else {
            console.log("DslEditor: calculateNodeTypeMatch - no parent or parent is not operator");
        }

        // 关键：如果存在外部预期类型且当前节点是根节点，则使用外部类型
        if (externalExpectedType && !hasEffectiveParent) {
            result.expectedType = externalExpectedType;
            console.log("DslEditor: calculateNodeTypeMatch - using external expected type:", result.expectedType);
        }

        // 检查类型匹配
        if (result.expectedType && !this.areTypesCompatible(result.actualType, result.expectedType)) {
            result.isTypeMatch = false;
            result.needsCorrection = true;
        }

        return result;
    }

    /**
     * 递归查找AST树中的所有类型问题
     */
    findAllTypeIssuesInAST(node) {
        const issues = [];

        if (!node) return issues;

        // 检查当前节点的类型匹配
        if (node._typeMatchInfo) {
            const typeMatch = node._typeMatchInfo;
            if (!typeMatch.isTypeMatch && typeMatch.needsCorrection) {
                issues.push(`类型不匹配：期望 ${typeMatch.expectedType}，实际 ${typeMatch.actualType}`);
            }
        }

        // 递归检查子节点
        if (node.type === 'operator') {
            if (node.leftOperand) {
                issues.push(...this.findAllTypeIssuesInAST(node.leftOperand));
            }
            if (node.rightOperand) {
                issues.push(...this.findAllTypeIssuesInAST(node.rightOperand));
            }
        }
        if (node.type === 'functionCall') {
            const params = node.parameters || [];
            const args = node.args || [];
            for (let i = 0; i < args.length; i++) {
                const arg = args[i];
                if (!arg) continue;
                const argValidation = this.validateExpressionSemantics(arg);
                if (argValidation.hasSemanticIssues) {
                    result.hasSemanticIssues = true;
                    result.issues.push(...argValidation.issues);
                }

                const expected = params[i]?.typeName ? this.normalizeTypeName(params[i].typeName) : null;
                if (expected) {
                    const match = this.calculateNodeTypeMatch(arg, expected);
                    if (match && match.isTypeMatch === false) {
                        result.hasSemanticIssues = true;
                        result.issues.push(`函数 ${node.functionName} 参数 ${params[i]?.name || i}: 类型不匹配（期望 ${expected}，实际 ${match.actualType}）`);
                    }
                }
            }
        }

        // 检查封装节点的内部AST
        if (node.metadata && node.metadata.innerAST) {
            issues.push(...this.findAllTypeIssuesInAST(node.metadata.innerAST));
        }

        return issues;
    }

    /**
     * 验证表达式语义有效性（简化版，主要用于类型匹配检查）
     */
    validateExpressionSemantics(node) {
        const result = {
            hasSemanticIssues: false,
            issues: [],
            fixSuggestions: []
        };

        // 检查当前节点的类型匹配
        const typeMatch = this.calculateNodeTypeMatch(node);
        if (!typeMatch.isTypeMatch) {
            result.hasSemanticIssues = true;
            result.issues.push(`类型不匹配：期望 ${typeMatch.expectedType}，实际 ${typeMatch.actualType}`);
        }

        // 递归检查子节点
        if (node.type === 'operator') {
            if (node.leftOperand) {
                const leftValidation = this.validateExpressionSemantics(node.leftOperand);
                if (leftValidation.hasSemanticIssues) {
                    result.hasSemanticIssues = true;
                    result.issues.push(...leftValidation.issues);
                }
            }
            if (node.rightOperand) {
                const rightValidation = this.validateExpressionSemantics(node.rightOperand);
                if (rightValidation.hasSemanticIssues) {
                    result.hasSemanticIssues = true;
                    result.issues.push(...rightValidation.issues);
                }
            }
        }

        // 如果是封装表达式，检查内部AST
        if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
            const innerValidation = this.validateExpressionSemantics(node.metadata.innerAST);
            if (innerValidation.hasSemanticIssues) {
                result.hasSemanticIssues = true;
                result.issues.push(...innerValidation.issues);
            }
        }

        return result;
    }

    /**
     * 计算AST节点的实际返回类型
     */
    calculateASTReturnType(node) {
        if (!node) return 'Object';

        console.log("DslEditor: Calculating return type for node:", node);

        if (node.type === 'value') {
            // 关键修复：如果是封装表达式，计算其内部AST的返回类型
            if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
                const innerReturnType = this.calculateASTReturnType(node.metadata.innerAST);
                console.log("DslEditor: Encapsulated expression inner return type:", innerReturnType);
                return innerReturnType;
            }

            // 值节点：使用其声明的返回类型
            const returnType = node.returnType || 'Object';
            console.log("DslEditor: Value node return type:", returnType);
            return returnType;
        }

        if (node.type === 'operator') {
            // 操作符节点：根据操作符类型推断
            const operatorReturnType = inferOperatorReturnType(node.operator);
            console.log("DslEditor: Operator", node.operator, "return type:", operatorReturnType);
            return operatorReturnType;
        } else if (node.type === 'functionCall') {
            const returnType = node.returnType || 'Object';
            return returnType;
        } else if (node.type === 'listQuery' || node.type === 'listSort' || node.type === 'listTake') {
            // 列表操作节点：返回类型与其源节点相同
            if (node.source) {
                const sourceReturnType = this.calculateASTReturnType(node.source);
                console.log(`DslEditor: List operation node (${node.type}) return type is same as source:`, sourceReturnType);
                return sourceReturnType;
            }
        }

        console.log("DslEditor: Unknown node type, defaulting to Object");
        return 'Object';
    }

    /**
     * 渲染DSL编辑器
     * @param {HTMLElement} container - 容器元素
     * @param {string} expression - 当前DSL表达式
     * @param {Function} onChange - 表达式变化回调
     */
    async render(container, expression = "", onChange = () => { }, options = {}) {
        try {
            await this.initialize();

            this.container = container;
            this.expression = expression;
            this.onChange = onChange;
            this.expectedType = options.expectedType || null;
            this.triggeringEventContext = options.triggeringEventContext || null;
            const session = options.userSession || getUserSession();
            this.userId = options.userId || session?.userId || null;

            this.clearContainer(container);

            console.log("DslEditor: Starting render with expression:", expression);
            
            // 关键修复：如果表达式为空，则始终执行初始化流程
            if (!expression) {
                this.setParseStatus("empty", "");
                this.initializeWorkspace();
            } else {
                await this.loadExpressionWithParseApi(expression);
                this.renderEditor();
            }

            console.log("DslEditor: Render completed successfully");
            // 初次渲染后对外通知当前表达式
            this.emitExpressionChange(this.expression || "");
        } catch (error) {
            console.error("DslEditor: Render failed:", error);
            this.showError(`渲染DSL编辑器失败: ${error.message}`, container);
        }
    }

    buildAstFromExpression(expression) {
        const raw = (expression ?? "").trim();
        if (!raw) return null;

        const isWrappedBySingleParens = (s) => {
            const t = (s ?? "").trim();
            if (!t.startsWith('(') || !t.endsWith(')')) return false;
            let depth = 0;
            for (let i = 0; i < t.length; i++) {
                const ch = t[i];
                if (ch === '(') depth++;
                if (ch === ')') depth--;
                if (depth === 0 && i < t.length - 1) return false;
            }
            return depth === 0;
        };

        const unwrapOuterParens = (s) => {
            let t = (s ?? "").trim();
            while (isWrappedBySingleParens(t)) {
                t = t.slice(1, -1).trim();
            }
            return t;
        };

        const inferReturnTypeFromRawExpression = (s) => {
            const t = unwrapOuterParens(s);

            // 字面量
            if (/^(true|false)$/i.test(t)) return 'Boolean';
            if (/^-?\d+$/.test(t)) return 'Int32';
            if (/^-?\d*\.\d+$/.test(t)) return 'Double';
            if ((t.startsWith('"') && t.endsWith('"')) || (t.startsWith("'") && t.endsWith("'"))) return 'String';

            // 常见事件字段访问函数
            const m = t.match(/^([A-Za-z_][A-Za-z0-9_]*)\s*\(/);
            if (m) {
                const fn = m[1];
                if (fn === 'eventBool') return 'Boolean';
                if (fn === 'eventString') return 'String';
                if (fn === 'eventGuid') return 'Guid';
                if (fn === 'eventInt') return 'Int32';

                const funcs = this.metadata?.dsl?.functions || [];
                const meta = funcs.find(f => String(f.name || f.Name) === String(fn));
                const rt = meta?.returnType || meta?.returnTypeName || meta?.ReturnType || meta?.ReturnTypeName;
                if (rt) return this.normalizeTypeName(rt) || 'Object';
            }

            // 粗粒度运算符推断（用于无法结构化解析的表达式）
            if (/[<>]=?|==|!=/.test(t) || /&&|\|\|/.test(t)) return 'Boolean';
            if (/[+\-*/]/.test(t)) return 'Number';

            return 'Object';
        };

        const parseLiteralOrPropertyChain = (s, forcedType = null) => {
            const t = unwrapOuterParens(s);

            // 字面量：作为静态值节点
            const litType = this.inferStaticValueType(t);
            if (litType !== 'Object' && (litType !== 'String' || (t.startsWith('"') || t.startsWith("'")))) {
                return createValueNode(t, forcedType || litType, {
                    type: 'staticValue',
                    staticValue: t,
                    isAwaitingInput: false
                });
            }

            // 属性链：尝试基于 metadata 推断每段类型
            const parts = t.split('.').filter(Boolean);
            const isIdent = (seg) => /^[A-Za-z_][A-Za-z0-9_]*$/.test(seg);
            if (parts.length > 0 && parts.every(isIdent)) {
                const ctx = this.getDslContextObject(parts[0]);
                let currentType = this.normalizeTypeName(ctx?.typeName) || 'Object';
                let currentPath = parts[0];
                let currentNode = createValueNode(currentPath, currentType, { type: 'context', name: parts[0], displayName: ctx?.displayName || parts[0] });

                for (let i = 1; i < parts.length; i++) {
                    const seg = parts[i];
                    currentPath = `${currentPath}.${seg}`;
                    const tm = this.getTypeMetadata(currentType);
                    const prop = (tm?.properties || []).find(p => String(p.name).toLowerCase() === String(seg).toLowerCase());
                    currentType = this.normalizeTypeName(prop?.typeName) || 'Object';
                    currentNode = createValueNode(currentPath, currentType, { type: 'property', fullPath: currentPath });
                }

                return currentNode;
            }

            // 回退：原样作为值节点，但尽量推断返回类型
            return createValueNode(t, forcedType || inferReturnTypeFromRawExpression(t), { type: 'rawText' });
        };

        const parseTrailingListOp = (s) => {
            const t = (s ?? "").trim();
            if (!t.endsWith(')')) return null;

            // 找到最后一对 (...) 的起点
            let depth = 0;
            let openIndex = -1;
            for (let i = t.length - 1; i >= 0; i--) {
                const ch = t[i];
                if (ch === ')') depth++;
                else if (ch === '(') {
                    depth--;
                    if (depth === 0) {
                        openIndex = i;
                        break;
                    }
                }
            }
            if (openIndex < 0) return null;

            const before = t.slice(0, openIndex).trim();
            const arg = t.slice(openIndex + 1, -1);
            const callMatch = before.match(/^(.*)\.(query|orderBy|take)$/);
            if (!callMatch) return null;

            return { source: callMatch[1], op: callMatch[2], arg };
        };

        // 解析：先去掉最外层括号，避免 (x) -> ((x)) 漂移
        let toParse = unwrapOuterParens(raw);
        const ops = [];
        while (true) {
            const parsed = parseTrailingListOp(toParse);
            if (!parsed) break;
            ops.unshift(parsed);
            toParse = parsed.source;
        }

        let rootNode = parseLiteralOrPropertyChain(toParse);

        for (const op of ops) {
            if (op.op === 'query') {
                const queryNode = createListQueryNode(rootNode);
                const filterAst = parseLiteralOrPropertyChain(op.arg);
                queryNode.setFilterExpression(this.createEncapsulatedFrom(filterAst));
                rootNode = queryNode;
            } else if (op.op === 'orderBy') {
                const sortNode = createListSortNode(rootNode);
                const sortAst = parseLiteralOrPropertyChain(op.arg);
                sortNode.setSortExpression(this.createEncapsulatedFrom(sortAst));
                rootNode = sortNode;
            } else if (op.op === 'take') {
                const takeNode = createListTakeNode(rootNode);
                const countAst = parseLiteralOrPropertyChain(op.arg);
                takeNode.setCountExpression(this.createEncapsulatedFrom(countAst));
                rootNode = takeNode;
            }
        }

        return rootNode;
    }

    // ---------------------------
    // Pipeline editor helpers
    // ---------------------------
    unwrapEncapsulatedNode(node) {
        if (!node) return null;
        if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
            return node.metadata.innerAST;
        }
        return node;
    }

    getPipelineContext() {
        const root = this.unwrapEncapsulatedNode(this.astRoot);
        if (!root) return null;
        if (!this.pipelineOpen) return null;
        if (root.type === "value" && root.metadata?.type === "rawText") {
            const rawText = String(root.value || root.metadata?.rawText || "");
            if (/\.(query|orderBy|take)\s*\(/i.test(rawText)) {
                return {
                    root,
                    source: null,
                    stages: [{ type: "raw", node: root, rawText }],
                    allowEmpty: false
                };
            }
        }
        if (isListOperationNode(root)) {
            const pipeline = extractListPipeline(root);
            if (!pipeline) return null;
            this.pipelineOpenSource = pipeline.source || null;
            return {
                root,
                source: pipeline.source,
                stages: pipeline.stages,
                allowEmpty: true
            };
        }

        const returnType = root.returnType || this.calculateASTReturnType(root);
        if (!this.isCollectionType(returnType)) {
            this.pipelineOpen = false;
            this.pipelineOpenSource = null;
            return null;
        }

        this.pipelineOpenSource = root;
        return {
            root,
            source: root,
            stages: [],
            allowEmpty: true
        };
    }

    openPipelineFromCurrent() {
        const root = this.unwrapEncapsulatedNode(this.astRoot);
        if (!root) {
            this.showExpressionError("当前没有可编辑的集合表达式。");
            return;
        }
        const returnType = root.returnType || this.calculateASTReturnType(root);
        if (!this.isCollectionType(returnType)) {
            this.showExpressionError("当前表达式不是集合，无法打开管线。");
            return;
        }
        this.pipelineOpen = true;
        this.pipelineOpenSource = root;
    }

    getPipelineStageLabel(stage) {
        const meta = PIPELINE_STAGE_META[stage?.type];
        if (meta?.dslMethod) {
            return `${meta.label} (.${meta.dslMethod})`;
        }
        return meta?.label || stage?.type || "阶段";
    }

    getPipelineStageSummary(stage) {
        if (!stage?.node) return "未设置";
        if (stage.type === "raw") {
            const rawText = stage.rawText || stage.node.value || stage.node.metadata?.rawText || "";
            return rawText ? `原始: ${rawText}` : "原始片段";
        }
        let exprNode = null;
        if (stage.node.type === "listQuery") exprNode = stage.node.filterExpression;
        if (stage.node.type === "listSort") exprNode = stage.node.sortExpression;
        if (stage.node.type === "listTake") exprNode = stage.node.countExpression;
        const readable = exprNode ? this.toReadableText(exprNode) : null;
        if (stage.node.type === "listQuery") {
            return readable ? `条件: ${readable}` : "未设置筛选条件";
        }
        if (stage.node.type === "listSort") {
            return readable ? `排序键: ${readable}` : "未设置排序键";
        }
        if (stage.node.type === "listTake") {
            return readable ? `取前: ${readable}` : "未设置数量";
        }
        return readable || "未设置";
    }

    getPipelineStageSlot(node) {
        if (!node) return null;
        if (node.type === "listQuery") return "filterExpression";
        if (node.type === "listSort") return "sortExpression";
        if (node.type === "listTake") return "countExpression";
        return null;
    }

    getPipelineStageExpressionNode(node, slot) {
        if (!node || !slot) return null;
        if (slot === "filterExpression") return node.filterExpression;
        if (slot === "sortExpression") return node.sortExpression;
        if (slot === "countExpression") return node.countExpression;
        return null;
    }

    getPipelineStageExpressionText(node, slot) {
        const exprNode = this.getPipelineStageExpressionNode(node, slot);
        if (!exprNode || exprNode.metadata?.type === "empty") return "";
        if (exprNode.metadata?.type === "encapsulated" && exprNode.metadata?.innerAST?.toExpression) {
            return exprNode.metadata.innerAST.toExpression();
        }
        return exprNode.toExpression?.() || "";
    }

    isPipelineStageEditing(stage) {
        return Boolean(this.pipelineEditState?.node && stage?.node === this.pipelineEditState.node);
    }

    editPipelineStage(stage) {
        if (!stage?.node) return;
        if (stage.type === "raw") return;
        const slot = this.getPipelineStageSlot(stage.node);
        if (!slot) return;
        const current = this.pipelineEditState || {};
        if (current.node === stage.node && current.slot === slot) {
            this.pipelineEditState.node = null;
            this.pipelineEditState.slot = null;
            this.pipelineEditState.draftExpression = "";
            this.renderEditor();
            return;
        }
        if (!current.editor) {
            current.editor = new DslEditor();
            current.editor.isSubEditor = true;
            current.editor.parentEditor = this;
        }
        this.pipelineEditState = {
            node: stage.node,
            slot,
            editor: current.editor,
            draftExpression: this.getPipelineStageExpressionText(stage.node, slot)
        };
        this.renderEditor();
    }

    renderPipelineStageInlineEditor(stage, container) {
        if (!this.isPipelineStageEditing(stage)) return;
        const state = this.pipelineEditState;
        const inlineEditor = state.editor || new DslEditor();
        state.editor = inlineEditor;

        inlineEditor.isSubEditor = true;
        inlineEditor.parentEditor = this;
        inlineEditor.pipelineOpen = false;
        inlineEditor.pipelineOpenSource = null;

        const expression = state.draftExpression ?? this.getPipelineStageExpressionText(state.node, state.slot);
        const slot = state.slot;
        const expectedType = slot === "filterExpression"
            ? "Boolean"
            : slot === "countExpression"
                ? "Int32"
                : null;

        const sourceType = state.node?.source?.returnType || this.calculateASTReturnType(state.node?.source);
        const innerType = this.getCollectionInnerType(sourceType);
        const contextOverride = innerType
            ? { name: "self", displayName: "当前项 (self)", typeName: innerType }
            : null;

        const renderPromise = inlineEditor.render(
            container,
            expression,
            (expr) => {
                this.pipelineEditState.draftExpression = expr;
            },
            {
                expectedType,
                triggeringEventContext: this.triggeringEventContext,
                userSession: getUserSession(),
                userId: this.userId
            }
        );

        const applyInlineStyles = () => {
            inlineEditor.contextOverride = contextOverride;
            inlineEditor.renderEditor();

            const mainExpression = container.querySelector(".main-expression");
            if (mainExpression) {
                mainExpression.style.display = "none";
            }
            const header = container.querySelector(".workspace-header");
            if (header) {
                header.style.marginBottom = "6px";
            }
        };

        if (renderPromise && typeof renderPromise.then === "function") {
            renderPromise.then(applyInlineStyles).catch(() => applyInlineStyles());
        } else {
            applyInlineStyles();
        }
    }

    commitPipelineStageEdit() {
        const state = this.pipelineEditState;
        if (!state?.node || !state.slot) return;
        this.applyPipelineStageExpression(state.node, state.slot, state.draftExpression);
        this.pipelineEditState.node = null;
        this.pipelineEditState.slot = null;
        this.pipelineEditState.draftExpression = "";
        this.updateExpressionFromAST();
    }

    cancelPipelineStageEdit() {
        if (!this.pipelineEditState) return;
        this.pipelineEditState.node = null;
        this.pipelineEditState.slot = null;
        this.pipelineEditState.draftExpression = "";
        this.renderEditor();
    }

    applyPipelineStageExpression(stageNode, slot, expression) {
        if (!stageNode || !slot) return;
        const trimmed = String(expression || "").trim();
        if (!trimmed) {
            const placeholder = createValueNode("", "Void", { type: "empty" });
            if (slot === "filterExpression") {
                stageNode.setFilterExpression(placeholder);
            } else if (slot === "sortExpression") {
                stageNode.setSortExpression(placeholder);
            } else if (slot === "countExpression") {
                stageNode.setCountExpression(placeholder);
            }
            return;
        }

        const ast = this.buildAstFromExpression(trimmed);
        if (!ast) return;
        const encapsulated = this.createEncapsulatedFrom(ast);
        if (slot === "filterExpression") {
            stageNode.setFilterExpression(encapsulated);
        } else if (slot === "sortExpression") {
            stageNode.setSortExpression(encapsulated);
        } else if (slot === "countExpression") {
            stageNode.setCountExpression(encapsulated);
        }
    }

    getPipelineStageTypeIssue(stage) {
        if (!stage?.node) return null;
        if (stage.type === "raw") return null;
        let exprNode = null;
        let expectedType = null;
        if (stage.node.type === "listQuery") {
            exprNode = stage.node.filterExpression;
            expectedType = "Boolean";
        } else if (stage.node.type === "listTake") {
            exprNode = stage.node.countExpression;
            expectedType = "Int32";
        }

        if (!exprNode || !expectedType) return null;
        const actualType = this.calculateASTReturnType(exprNode);
        if (!actualType) return null;

        let hasIssue = false;
        if (expectedType === "Int32") {
            hasIssue = !this.isNumericType(actualType);
        } else if (!this.areTypesCompatible(actualType, expectedType)) {
            hasIssue = true;
        }

        if (!hasIssue) return null;
        return { expectedType, actualType };
    }

    insertPipelineStage(stageType, insertIndex = null) {
        const pipeline = this.getPipelineContext();
        if (!pipeline || !pipeline.source) return;
        const newNode = createStageNode(stageType, pipeline.source);
        if (!newNode) return;

        const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
        const index = typeof insertIndex === "number" ? insertIndex : stages.length;
        stages.splice(index, 0, { type: stageType, node: newNode });

        const newRoot = rebuildListChainFromStages(pipeline.source, stages);
        this.workspaceAST = newRoot;
        this.astRoot = newRoot;
        this.maintainASTRelationships(this.astRoot);
        this.updateExpressionFromAST();
        this.renderEditor();
    }

    removePipelineStage(index) {
        const pipeline = this.getPipelineContext();
        if (!pipeline) return;
        const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
        if (index < 0 || index >= stages.length) return;
        stages.splice(index, 1);

        const newRoot = stages.length > 0
            ? rebuildListChainFromStages(pipeline.source, stages)
            : pipeline.source;

        this.workspaceAST = newRoot;
        this.astRoot = newRoot;
        this.maintainASTRelationships(this.astRoot);
        this.updateExpressionFromAST();
        this.renderEditor();
    }

    movePipelineStage(fromIndex, toIndex) {
        const pipeline = this.getPipelineContext();
        if (!pipeline) return;
        const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
        if (fromIndex < 0 || fromIndex >= stages.length) return;
        if (toIndex < 0 || toIndex >= stages.length) return;
        if (fromIndex === toIndex) return;

        const [moved] = stages.splice(fromIndex, 1);
        stages.splice(toIndex, 0, moved);

        const newRoot = rebuildListChainFromStages(pipeline.source, stages);
        this.workspaceAST = newRoot;
        this.astRoot = newRoot;
        this.maintainASTRelationships(this.astRoot);
        this.updateExpressionFromAST();
        this.renderEditor();
    }

    /**
     * 解析表达式字符串为AST (V2 - 支持链式调用和query)
     * @param {string} expression - 表达式字符串
     */
    parseExpression(expression) {
        const raw = (expression ?? "").trim();
        console.log(`DslEditor: Parsing expression string V2: "${raw}"`);
        if (!raw) {
            this.initializeWorkspace();
            return;
        }

        const rootNode = this.buildAstFromExpression(raw);
        if (!rootNode) {
            this.initializeWorkspace();
            return;
        }

        // 最终封装结果
        const finalNode = this.createEncapsulatedFrom(rootNode);

        this.virtualRoot.main = finalNode;
        this.workspaceAST = this.cloneAST(finalNode);
        this.astRoot = this.workspaceAST;
        this.expression = this.astRoot?.toExpression?.() || raw;

        console.log("DslEditor: Parsed expression into AST:", this.virtualRoot.main);
    }



    /**
     * 渲染编辑器界面
     */
    renderEditor() {
        // 查找现有的编辑器容器，如果不存在则创建
        let editorContainer = this.container.querySelector(".dsl-editor");
        if (!editorContainer) {
            editorContainer = document.createElement("div");
            editorContainer.className = "dsl-editor";
            this.container.appendChild(editorContainer);
        }

        // 清空现有内容
        editorContainer.innerHTML = "";

        // 渲染主表达式（上方）
        // 统一渲染：mainExpressionAST 现在是 virtualRoot.main 的别名
        renderMainExpressionUI(this, editorContainer);

        // 渲染工作区（模块化）
        renderWorkspaceUI(this, editorContainer);
    }

    getFavoriteExpectedType() {
        const expected = this.slotExpectedType || this.expectedType || null;
        return expected ? this.normalizeTypeName(expected) : null;
    }

    async fetchFavorites(query = "", expectedType = null, limit = 12, page = 1) {
        if (!this.userId) {
            return { items: [], total: 0 };
        }

        const params = new URLSearchParams();
        params.set("userId", this.userId);
        params.set("page", String(page));
        params.set("limit", String(limit));
        if (expectedType) {
            params.set("expectedType", expectedType);
        }
        if (query) {
            params.set("query", query);
        }

        const res = await fetch(`/api/dsl-favorites?${params.toString()}`);
        if (!res.ok) {
            throw new Error(`收藏加载失败 (${res.status})`);
        }
        const data = await res.json();
        const items = data.items || data.Items || [];
        const total = data.total ?? data.Total ?? items.length;
        return { items, total };
    }

    async refreshFavorites(query = this.favoriteState.query) {
        this.favoriteState.loading = true;
        this.favoriteState.error = null;
        this.favoriteState.query = query;

        try {
            const expectedType = this.getFavoriteExpectedType();
            const result = await this.fetchFavorites(query, expectedType);
            this.favoriteState.items = result.items;
            if (query) {
                this.favoriteState.recentItems = [];
            } else {
                this.favoriteState.recentItems = result.items
                    .filter(item => item.lastUsedAt || item.usageCount > 0)
                    .slice(0, 6);
            }
        } catch (error) {
            console.error("DslEditor: Failed to fetch favorites", error);
            this.favoriteState.error = error?.message || "收藏加载失败";
            this.favoriteState.items = [];
            this.favoriteState.recentItems = [];
        } finally {
            this.favoriteState.loading = false;
        }
    }

    setParseStatus(state, message = "", errors = []) {
        this.parseStatus = {
            state,
            message,
            errors: Array.isArray(errors) ? errors : []
        };
    }

    getParseStatusInfo() {
        const state = this.parseStatus?.state;
        if (!state || state === "idle" || state === "empty") return null;

        const message = this.parseStatus?.message || "";
        const errors = this.parseStatus?.errors || [];
        const detail = [message, ...errors].filter(Boolean).join("\n");

        if (state === "parsed") {
            return { label: "解析成功", color: "#2e7d32", bg: "#e8f5e9", border: "#a5d6a7", detail };
        }
        if (state === "loading") {
            return { label: "解析中...", color: "#1565c0", bg: "#e3f2fd", border: "#90caf9", detail };
        }
        if (state === "fallback") {
            return { label: "解析失败(降级)", color: "#c62828", bg: "#ffebee", border: "#ef9a9a", detail };
        }

        return { label: "解析异常", color: "#ad1457", bg: "#fce4ec", border: "#f8bbd0", detail };
    }

    async loadExpressionWithParseApi(expression) {
        const raw = (expression ?? "").trim();
        if (!raw) {
            this.setParseStatus("empty", "");
            this.initializeWorkspace();
            return;
        }

        const parseResult = await this.requestParseAst(raw);
        if (!parseResult.success || !parseResult.root) {
            const fallbackMessage = parseResult.message || "解析失败，已进入原始文本模式。";
            this.applyRawTextFallback(raw, fallbackMessage, parseResult.errors);
            return;
        }

        const mapped = this.mapDslAstDtoToUiAst(parseResult.root);
        if (!mapped) {
            this.applyRawTextFallback(raw, "解析结果无法映射为可视化结构，已进入原始文本模式。");
            return;
        }

        this.applyParsedAst(mapped, raw);
    }

    async requestParseAst(expression) {
        try {
            const res = await fetch("/api/dsl/parse", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ expression })
            });
            if (!res.ok) {
                return {
                    success: false,
                    message: `解析服务请求失败 (${res.status})`,
                    errors: []
                };
            }
            const data = await res.json();
            const errors = data.errors || data.Errors || [];
            const success = Boolean(data.success ?? data.Success);
            return {
                success,
                root: data.root || data.Root || null,
                message: success ? "" : (errors[0] || "解析失败"),
                errors
            };
        } catch (error) {
            return {
                success: false,
                message: "解析服务不可用，已进入原始文本模式。",
                errors: [error?.message || String(error)]
            };
        }
    }

    applyParsedAst(rootNode, expression) {
        const finalNode = this.createEncapsulatedFrom(rootNode);
        this.virtualRoot.main = finalNode;
        this.workspaceAST = this.cloneAST(finalNode);
        this.astRoot = this.workspaceAST;
        this.expression = this.astRoot?.toExpression?.() || expression;
        this.maintainASTRelationships(this.virtualRoot.main);
        this.setParseStatus("parsed", "解析成功");
    }

    applyRawTextFallback(expression, message, errors = []) {
        const expected = this.normalizeTypeName(this.expectedType);
        const rawNode = createValueNode(expression, expected || "Object", {
            type: "rawText",
            rawText: expression
        });
        const finalNode = this.createEncapsulatedFrom(rawNode);
        this.virtualRoot.main = finalNode;
        this.workspaceAST = this.cloneAST(finalNode);
        this.astRoot = this.workspaceAST;
        this.expression = this.astRoot?.toExpression?.() || expression;
        this.maintainASTRelationships(this.virtualRoot.main);
        this.setParseStatus("fallback", message, errors);
    }

    mapDslAstDtoToUiAst(dto) {
        if (!dto || !dto.nodeType && !dto.NodeType) return null;

        const nodeType = dto.nodeType || dto.NodeType;

        if (nodeType === "Literal") {
            return this.mapLiteralDtoToNode(dto);
        }
        if (nodeType === "Identifier") {
            return this.mapIdentifierDtoToNode(dto);
        }
        if (nodeType === "MemberAccess") {
            const path = this.flattenMemberAccessPath(dto);
            if (!path || path.length === 0) return null;
            return this.createPropertyChainNode(path);
        }
        if (nodeType === "BinaryOperation") {
            return this.mapBinaryOperationDtoToNode(dto);
        }
        if (nodeType === "UnaryOperation") {
            return this.mapUnaryOperationDtoToNode(dto);
        }
        if (nodeType === "FunctionCall") {
            return this.mapFunctionCallDtoToNode(dto);
        }
        if (nodeType === "ConditionalExpression" || nodeType === "ArrayAccess" || nodeType === "Block") {
            return null;
        }

        return null;
    }

    mapLiteralDtoToNode(dto) {
        const value = dto.value ?? dto.Value;
        const dataType = (dto.dataType ?? dto.DataType ?? "").toString().toLowerCase();
        let returnType = "Object";
        if (dataType === "boolean") {
            returnType = "Boolean";
        } else if (dataType === "string") {
            returnType = "String";
        } else if (dataType === "number") {
            if (typeof value === "number" && Number.isInteger(value)) {
                returnType = "Int32";
            } else {
                returnType = "Double";
            }
        }

        const formattedValue = this.formatLiteralValue(value, returnType);
        const displayValue = typeof value === "string" ? value : String(value ?? "");

        return createValueNode(formattedValue, returnType, {
            type: "staticValue",
            staticValue: displayValue,
            isAwaitingInput: false
        });
    }

    mapIdentifierDtoToNode(dto) {
        const path = dto.path || dto.Path;
        const name = dto.name || dto.Name;
        const segments = Array.isArray(path) && path.length > 0 ? path : (name ? [name] : []);
        if (segments.length === 0) return null;
        return this.createPropertyChainNode(segments);
    }

    mapBinaryOperationDtoToNode(dto) {
        const leftDto = dto.left || dto.Left;
        const rightDto = dto.right || dto.Right;
        const opToken = dto.operator || dto.Operator;
        const operator = this.mapOperatorTokenToSymbol(opToken);
        if (!operator) return null;

        const leftNode = this.mapDslAstDtoToUiAst(leftDto);
        const rightNode = this.mapDslAstDtoToUiAst(rightDto);
        if (!leftNode || !rightNode) return null;

        const opNode = createOperatorNode(operator);
        opNode.setLeftOperand(leftNode);
        opNode.setRightOperand(rightNode);
        return opNode;
    }

    mapUnaryOperationDtoToNode(dto) {
        const opToken = dto.operator || dto.Operator;
        const operandDto = dto.operand || dto.Operand;
        const operator = this.mapOperatorTokenToSymbol(opToken);
        if (!operator || !operandDto) return null;

        if (operator === "-" && (operandDto.nodeType || operandDto.NodeType) === "Literal") {
            const literal = this.mapLiteralDtoToNode(operandDto);
            if (!literal) return null;
            if (!this.isNumericType(literal.returnType)) return null;
            const rawValue = literal.metadata?.staticValue;
            const negatedValue = rawValue ? `-${rawValue}` : null;
            if (!negatedValue) return null;
            return createValueNode(negatedValue, literal.returnType, {
                type: "staticValue",
                staticValue: negatedValue,
                isAwaitingInput: false
            });
        }

        return null;
    }

    mapFunctionCallDtoToNode(dto) {
        const name = dto.name || dto.Name;
        if (!name) return null;

        const receiver = dto.receiver || dto.Receiver;
        const isMethodCall = dto.isMethodCall ?? dto.IsMethodCall;
        if (receiver || isMethodCall) {
            const lowerName = String(name).toLowerCase();
            if (['query', 'orderby', 'take'].includes(lowerName)) {
                return this.mapListOperationFromMethodCall(lowerName, receiver, dto);
            }
        }

        const eventAccessor = this.tryMapEventFieldCall(name, dto);
        if (eventAccessor) return eventAccessor;

        const argsDto = dto.arguments || dto.Arguments || [];
        const args = Array.isArray(argsDto) ? argsDto.map(arg => this.mapDslAstDtoToUiAst(arg)) : [];
        if (args.some(arg => !arg)) return null;

        const signature = this.getFunctionSignature(name);
        const returnType = this.normalizeTypeName(signature?.returnType || signature?.returnTypeName) || "Object";
        const parameters = signature?.parameters || signature?.Parameters || [];
        const normalizedParameters = Array.isArray(parameters) ? [...parameters] : [];
        if (normalizedParameters.length < args.length) {
            for (let i = normalizedParameters.length; i < args.length; i++) {
                normalizedParameters.push({
                    name: `arg${i + 1}`,
                    typeName: "Object",
                    isOptional: true
                });
            }
        }
        const fnNode = createFunctionCallNode(name, returnType, normalizedParameters);
        args.forEach((arg, idx) => {
            if (arg) fnNode.setArg(idx, arg);
        });
        return fnNode;
    }

    mapListOperationFromMethodCall(methodName, receiverDto, dto) {
        if (!receiverDto) return null;
        const sourceNode = this.mapDslAstDtoToUiAst(receiverDto);
        if (!sourceNode) return null;

        const argsDto = dto.arguments || dto.Arguments || [];
        const argNode = Array.isArray(argsDto) && argsDto.length > 0 ? this.mapDslAstDtoToUiAst(argsDto[0]) : null;

        if (methodName === "query") {
            const queryNode = createListQueryNode(sourceNode);
            if (argNode) {
                queryNode.setFilterExpression(this.createEncapsulatedFrom(argNode));
            }
            return queryNode;
        }

        if (methodName === "orderby") {
            const sortNode = createListSortNode(sourceNode);
            if (argNode) {
                sortNode.setSortExpression(this.createEncapsulatedFrom(argNode));
            }
            return sortNode;
        }

        if (methodName === "take") {
            const takeNode = createListTakeNode(sourceNode);
            if (argNode) {
                takeNode.setCountExpression(this.createEncapsulatedFrom(argNode));
            }
            return takeNode;
        }

        return null;
    }

    tryMapEventFieldCall(name, dto) {
        const accessor = String(name || "");
        const mapReturnType = (fn) => {
            if (fn === "eventBool") return "Boolean";
            if (fn === "eventString") return "String";
            if (fn === "eventGuid") return "Guid";
            if (fn === "eventInt") return "Int32";
            return null;
        };
        const returnType = mapReturnType(accessor);
        if (!returnType) return null;

        const argsDto = dto.arguments || dto.Arguments || [];
        if (!Array.isArray(argsDto) || argsDto.length === 0) return null;

        const arg = argsDto[0];
        const argType = arg?.nodeType || arg?.NodeType;
        if (argType !== "Literal") return null;

        const propertyName = arg.value ?? arg.Value;
        if (typeof propertyName !== "string" || !propertyName) return null;

        const eventMeta = this.getEventFieldMeta(propertyName, returnType);
        const displayValue = eventMeta?.displayValue || propertyName;
        const expression = `${accessor}(\"${propertyName}\")`;

        return createValueNode(expression, returnType, {
            type: "eventField",
            displayValue,
            eventType: eventMeta?.eventType || null,
            propertyName,
            scope: eventMeta?.scope || null
        });
    }

    getEventFieldMeta(propertyName, returnType) {
        const ctx = this.triggeringEventContext;
        const eventTypes = ctx?.eventTypes || [];
        if (!Array.isArray(eventTypes) || eventTypes.length === 0) {
            return null;
        }

        const selected = eventTypes[0];
        const ev = (this.metadata?.events || []).find(e =>
            String(e.eventType ?? e.EventType ?? '').toLowerCase() === String(selected).toLowerCase()
        );
        if (!ev) return null;

        const parameters = ev.parameters || ev.Parameters || [];
        const param = parameters.find(p => {
            const name = p.propertyName || p.PropertyName || p.name;
            return String(name || "").toLowerCase() === String(propertyName).toLowerCase();
        });

        if (!param) return null;

        const eventDisplayName = ev.displayName || ev.DisplayName || String(selected);
        const fieldDisplayName = param.displayName || param.DisplayName || propertyName;
        return {
            eventType: selected,
            displayValue: `${eventDisplayName}.${fieldDisplayName}`,
            scope: param.scope || param.Scope || null,
            returnType
        };
    }

    getFunctionSignature(name) {
        const functions = this.metadata?.dsl?.functions || [];
        return functions.find(f => String(f.name ?? f.Name ?? '').toLowerCase() === String(name).toLowerCase());
    }

    mapOperatorTokenToSymbol(token) {
        const value = String(token || "");
        const map = {
            Equal: "==",
            NotEqual: "!=",
            GreaterThan: ">",
            GreaterEqual: ">=",
            LessThan: "<",
            LessEqual: "<=",
            And: "&&",
            Or: "||",
            Plus: "+",
            Minus: "-",
            Multiply: "*",
            Divide: "/"
        };
        return map[value] || null;
    }

    formatLiteralValue(value, returnType) {
        if (value === null || value === undefined) {
            return "null";
        }
        if (returnType === "String") {
            return JSON.stringify(String(value));
        }
        if (returnType === "Boolean") {
            return String(value).toLowerCase();
        }
        if (returnType === "Int32" || returnType === "Double" || returnType === "Number") {
            return String(value);
        }
        return String(value);
    }

    createPropertyChainNode(segments) {
        if (!Array.isArray(segments) || segments.length === 0) return null;
        const ctx = this.getDslContextObject(segments[0]);
        let currentType = this.normalizeTypeName(ctx?.typeName) || "Object";
        let currentPath = segments[0];
        let node = createValueNode(currentPath, currentType, {
            type: "context",
            name: segments[0],
            displayName: ctx?.displayName || segments[0]
        });

        for (let i = 1; i < segments.length; i++) {
            const seg = segments[i];
            currentPath = `${currentPath}.${seg}`;
            const tm = this.getTypeMetadata(currentType);
            const prop = (tm?.properties || []).find(p =>
                String(p.name || "").toLowerCase() === String(seg).toLowerCase()
            );
            currentType = this.normalizeTypeName(prop?.typeName) || "Object";
            node = createValueNode(currentPath, currentType, {
                type: "property",
                displayName: prop?.displayName || seg,
                fullPath: currentPath
            });
        }

        return node;
    }

    flattenMemberAccessPath(dto) {
        const nodeType = dto?.nodeType || dto?.NodeType;
        if (nodeType === "MemberAccess") {
            const left = this.flattenMemberAccessPath(dto.target || dto.Target);
            const right = this.flattenMemberAccessPath(dto.member || dto.Member);
            if (!left || !right) return null;
            return [...left, ...right];
        }
        if (nodeType === "Identifier") {
            const path = dto.path || dto.Path;
            const name = dto.name || dto.Name;
            if (Array.isArray(path) && path.length > 0) return path;
            if (name) return [name];
        }
        return null;
    }

    async markFavoriteUsed(favoriteId) {
        if (!this.userId || !favoriteId) return;
        const res = await fetch(`/api/dsl-favorites/${favoriteId}/use?userId=${encodeURIComponent(this.userId)}`, {
            method: "POST"
        });
        if (!res.ok) {
            console.warn("DslEditor: Failed to update favorite usage.", res.status);
        }
    }

    async createFavoriteFromCurrentExpression() {
        if (!this.userId) {
            this.showExpressionError("登录后才能使用收藏功能。");
            return;
        }

        if (!this.astRoot) {
            this.showExpressionError("当前没有可收藏的表达式。");
            return;
        }

        if (findIncompleteNode(this.astRoot)) {
            this.showExpressionError("表达式未完成，无法收藏。");
            return;
        }

        const expression = this.astRoot.toExpression?.();
        if (!expression) {
            this.showExpressionError("表达式为空，无法收藏。");
            return;
        }

        const defaultLabel = this.getReadablePreviewText() || expression;
        const label = window.prompt("请输入收藏名称", defaultLabel);
        if (!label) {
            return;
        }

        const payload = {
            userId: this.userId,
            label: label.trim(),
            expression,
            expectedType: this.getFavoriteExpectedType()
        };

        try {
            const res = await fetch("/api/dsl-favorites", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                const msg = await res.text();
                throw new Error(msg || `收藏失败 (${res.status})`);
            }

            const created = await res.json();
            await this.markFavoriteUsed(created?.id || created?.Id);
            await this.refreshFavorites(this.favoriteState.query);
        } catch (error) {
            console.error("DslEditor: Failed to create favorite", error);
            this.showExpressionError(error?.message || "收藏失败");
        }
    }

    insertFavoriteExpression(expression) {
        const rootNode = this.buildAstFromExpression(expression);
        if (!rootNode) {
            this.showExpressionError("收藏表达式为空，无法插入。");
            return;
        }

        this.selectedNode = null;
        this.workspaceAST = this.cloneAST(rootNode);
        this.astRoot = this.workspaceAST;
        this.isExpressionCompleted = false;

        this.updateExpressionFromAST();
        this.renderEditor();

        const validation = this.validateExpressionForCompletion();
        if (!validation.isValid) {
            this.showExpressionError(validation.error);
        }
    }

    removeFavoritesMenu() {
        const existingMenu = document.querySelector(".dsl-favorites-menu");
        if (existingMenu) {
            existingMenu.remove();
        }
        if (this.favoriteMenuOutsideHandler) {
            document.removeEventListener("click", this.favoriteMenuOutsideHandler);
        }
        this.favoriteMenuOutsideHandler = null;
        this.favoriteMenuAnchor = null;
    }

    showFavoritesMenu(anchor) {
        if (!anchor) return;

        const menu = document.createElement("div");
        menu.className = "dsl-favorites-menu";

        const closeMenu = () => {
            this.removeFavoritesMenu();
        };

        this.renderFavoritesSection(menu, closeMenu);

        const rect = anchor.getBoundingClientRect();
        const menuWidth = 320;
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;
        const spaceBelow = viewportHeight - rect.bottom - 10;
        const spaceAbove = rect.top - 10;

        let maxMenuHeight = Math.min(420, Math.max(0, spaceBelow));
        let menuTop = rect.bottom + 4;
        if (spaceBelow < 180 && spaceAbove > spaceBelow) {
            maxMenuHeight = Math.min(420, Math.max(0, spaceAbove));
            menuTop = rect.top - maxMenuHeight - 4;
        }
        if (menuTop < 10) {
            menuTop = 10;
        }

        let menuLeft = rect.right - menuWidth;
        if (menuLeft < 10) {
            menuLeft = 10;
        }
        if (menuLeft + menuWidth > viewportWidth - 10) {
            menuLeft = viewportWidth - menuWidth - 10;
        }

        menu.style.cssText = `
            position: fixed;
            left: ${menuLeft}px;
            top: ${menuTop}px;
            z-index: 1100;
            width: ${menuWidth}px;
            max-height: ${maxMenuHeight}px;
            overflow-y: auto;
            overflow-x: hidden;
            background: #ffffff;
            border: 1px solid #bdc3c7;
            border-radius: 8px;
            box-shadow: 0 8px 18px rgba(0,0,0,0.2);
        `;

        document.body.appendChild(menu);

        const outsideHandler = (event) => {
            if (!menu.contains(event.target) && event.target !== anchor) {
                closeMenu();
            }
        };

        this.favoriteMenuOutsideHandler = outsideHandler;
        this.favoriteMenuAnchor = anchor;

        setTimeout(() => document.addEventListener("click", outsideHandler), 0);
    }

    toggleFavoritesMenu(anchor) {
        const existingMenu = document.querySelector(".dsl-favorites-menu");
        const isSameAnchor = this.favoriteMenuAnchor === anchor;
        if (existingMenu) {
            this.removeFavoritesMenu();
            if (isSameAnchor) {
                return;
            }
        }
        this.showFavoritesMenu(anchor);
    }

    renderFavoritesToolbar() {
        const toolbar = document.createElement("div");
        toolbar.className = "dsl-favorites-toolbar";
        toolbar.style.cssText = "display: inline-flex; align-items: center; gap: 6px;";

        const addWrapper = document.createElement("span");
        addWrapper.style.cssText = "display: inline-flex;";
        const addButton = document.createElement("button");
        addButton.type = "button";
        addButton.textContent = "收藏当前";
        addButton.style.cssText = `
            border: 1px solid #ffb300;
            background: #fff8e1;
            color: #8d6e63;
            font-size: 12px;
            padding: 4px 10px;
            border-radius: 12px;
            cursor: pointer;
        `;
        if (!this.userId) {
            addWrapper.title = "登录后可使用收藏";
            addButton.disabled = true;
            addButton.style.opacity = "0.6";
            addButton.style.cursor = "not-allowed";
            addButton.style.pointerEvents = "none";
        }
        addButton.addEventListener("click", async (event) => {
            event.preventDefault();
            await this.createFavoriteFromCurrentExpression();
        });
        addWrapper.appendChild(addButton);
        toolbar.appendChild(addWrapper);

        const listButton = document.createElement("button");
        listButton.type = "button";
        listButton.textContent = "收藏列表";
        listButton.style.cssText = `
            border: 1px solid #90caf9;
            background: #e3f2fd;
            color: #1565c0;
            font-size: 12px;
            padding: 4px 10px;
            border-radius: 12px;
            cursor: pointer;
        `;
        listButton.addEventListener("click", (event) => {
            event.preventDefault();
            this.toggleFavoritesMenu(listButton);
        });
        toolbar.appendChild(listButton);

        return toolbar;
    }

    /**
     * 处理“提升层级”操作
     */
    handleElevateExpression(nodeToWrap) {
        console.log("DslEditor: Elevating expression for node:", nodeToWrap);

        // 如果没有节点可以包裹，则不执行任何操作
        if (!nodeToWrap) {
            console.warn("DslEditor: handleElevateExpression called with no node to wrap.");
            return;
        }

        // 1. 创建一个新的封装节点来包裹当前节点
        const encapsulatedNode = this.createEncapsulatedFrom(nodeToWrap);

        // 2. 在AST中用新节点替换旧节点
        // 由于我们是在工作区操作，直接更新workspaceAST
        this.workspaceAST = encapsulatedNode;
        this.astRoot = encapsulatedNode; // 保持同步

        // 3. 更新工作区和UI
        this.updateExpressionFromAST();
        this.renderEditor();
    }
    /**
     * 渲染工作区标题
     */
    renderWorkspaceHeader(container) {
        const workspaceHeader = document.createElement("div");
        workspaceHeader.className = "workspace-header";
        workspaceHeader.style.cssText = `
            background: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 4px;
            padding: 8px 12px;
            margin-bottom: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
            flex-wrap: wrap;
        `;

        const leftGroup = document.createElement("div");
        leftGroup.style.cssText = "display: flex; align-items: center; gap: 8px;";

        const icon = document.createElement("span");
        icon.textContent = "🔧";
        icon.style.fontSize = "16px";

        const text = document.createElement("span");
        const top = typeof this.peekEditingPath === 'function' ? this.peekEditingPath() : null;
        text.textContent = top?.descriptor ? `工作区：${top.descriptor}` : "工作区：构建新表达式";
        text.style.cssText = `
            font-weight: 500;
            color: #856404;
            font-size: 14px;
        `;

        leftGroup.appendChild(icon);
        leftGroup.appendChild(text);
        workspaceHeader.appendChild(leftGroup);

        const rightGroup = document.createElement("div");
        rightGroup.style.cssText = "margin-left: auto; display: flex; align-items: center; gap: 8px; flex-wrap: wrap;";

        const hint = document.createElement("span");
        const expected = this.slotExpectedType || this.expectedType || null;
        hint.textContent = expected ? `期望类型：${this.getDisplayTypeName(expected)}` : "完成后将添加到上方的已完成表达式中";
        hint.style.cssText = `
            font-size: 12px;
            color: #856404;
        `;

        const statusInfo = this.getParseStatusInfo ? this.getParseStatusInfo() : null;
        if (statusInfo) {
            const status = document.createElement("span");
            status.textContent = statusInfo.label;
            status.title = statusInfo.detail || statusInfo.label;
            status.style.cssText = `
                font-size: 12px;
                font-weight: 600;
                padding: 2px 8px;
                border-radius: 10px;
                color: ${statusInfo.color};
                background: ${statusInfo.bg};
                border: 1px solid ${statusInfo.border};
            `;
            rightGroup.appendChild(status);
        }

        rightGroup.appendChild(hint);
        rightGroup.appendChild(this.renderFavoritesToolbar());
        workspaceHeader.appendChild(rightGroup);
        container.appendChild(workspaceHeader);
    }

    /**
     * 渲染可点击的主表达式
     */
    renderClickableMainExpression(node, container) {
        // 主表达式渲染入口：renderClickableMainExpression -> renderEncapsulatedExpression -> renderClickableSubExpressions/renderClickableASTNode。
        // 工作区渲染入口：renderASTNodeRecursive -> renderValueNode/renderOperatorNodeAST/renderListQueryNodeAST。
        // 可点击入口仅限封装胶囊/嵌套胶囊/空占位符，普通节点仅用于展示。
        if (!node) return;

        if (node.type === 'value') {
            // 值节点：可点击编辑
            const valueSpan = document.createElement("span");

            // 如果是封装表达式，直接调用封装渲染函数
            if (node.metadata && node.metadata.type === 'encapsulated') {
                this.renderEncapsulatedExpression(node, container);
                return;
            }

            // --- 原有的普通值节点渲染逻辑 ---
            valueSpan.textContent = node.value;
            valueSpan.style.cssText = `
                padding: 2px 4px;
                border-radius: 3px;
                display: inline-block;
            `;


            // 选中高亮
            if (this.selectedNode === node) {
                valueSpan.style.border = '3px solid #27ae60';
                valueSpan.style.fontWeight = '700';
                valueSpan.style.borderRadius = '4px';
            }

            container.appendChild(valueSpan);
        } else if (node.type === 'operator') {
            // 操作符节点：渲染左操作数、操作符、右操作数
            if (node.leftOperand) {
                this.renderClickableMainExpression(node.leftOperand, container);
            } else {
                // 左操作数为空：渲染占位符
                this.renderEmptySlotPlaceholder(node, 'left', container);
            }

            // 操作符
            const operatorSpan = document.createElement("span");
            operatorSpan.textContent = ` ${node.operator} `;
            operatorSpan.style.cssText = "color: #666; font-weight: bold; margin: 0 4px;";
            container.appendChild(operatorSpan);

            if (node.rightOperand) {
                this.renderClickableMainExpression(node.rightOperand, container);
            } else {
                // 右操作数为空：渲染占位符
                this.renderEmptySlotPlaceholder(node, 'right', container);
            }
        }
    }

    /**
     * 编辑主表达式中的子表达式
     */

    /**
     * 扩展主表达式（添加逻辑操作符）
     */
    expandMainExpression() {
        console.log("DslEditor: Expanding main expression");
    
        // 1. 设置正确的编辑上下文，指向 virtualRoot 的 main 槽位
        this.clearEditingPath();
        this.pushEditingPath({
            parentNode: this.virtualRoot,
            slot: 'main',
            descriptor: '扩展主表达式'
        });
    
        // 2. 将当前的主表达式加载到工作区，准备作为操作符的左操作数
        this.workspaceAST = this.cloneAST(this.virtualRoot.main);
        this.astRoot = this.workspaceAST;
        this.isExpressionCompleted = false;
    
        // 3. 重新渲染，此时工作区会显示主表达式的内容，等待用户选择操作符
        this.renderEditor();
    
        console.log("DslEditor: Main expression loaded into workspace for expansion.");
    }

    // 已迁移到 ui-workspace.js 的 renderNodeChainUI

    /**
     * 递归渲染AST节点
     */
    renderASTNodeRecursive(node, container) {
        if (!node) return;
    
        console.log("DslEditor: renderASTNodeRecursive called for:", {
            type: node.type,
            value: node.value,
            metadataType: node.metadata?.type
        });
    
        // 关键修复：在顶层渲染函数中处理属性链
        if (node.type === 'value' && (node.metadata?.type === 'property' || (node.metadata?.type === 'context' && node.value.includes('.')))) {
            this.renderPropertyChainUI(node, container);
            return;
        }
    
        // 如果节点被标记为已完成的表达式，添加视觉包装
        if (node.isCompletedExpression) {
            const completedWrapper = document.createElement("span");
            completedWrapper.className = "dsl-completed-expression";
            completedWrapper.style.cssText = `
                display: inline-block;
                background: linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%);
                border: 2px solid #27ae60;
                border-radius: 6px;
                padding: 4px 8px;
                margin: 2px;
                box-shadow: 0 2px 4px rgba(39, 174, 96, 0.2);
                position: relative;
            `;
            const openParen = document.createElement("span");
            openParen.textContent = "(";
            openParen.style.cssText = "font-weight: bold; color: #27ae60; margin-right: 4px;";
            completedWrapper.appendChild(openParen);
            const contentContainer = document.createElement("span");
            this.renderNodeContent(node, contentContainer);
            completedWrapper.appendChild(contentContainer);
            const closeParen = document.createElement("span");
            closeParen.textContent = ")";
            closeParen.style.cssText = "font-weight: bold; color: #27ae60; margin-left: 4px;";
            completedWrapper.appendChild(closeParen);
            const completedLabel = document.createElement("span");
            completedLabel.textContent = "✓";
            completedLabel.style.cssText = `
                position: absolute;
                top: -8px;
                right: -8px;
                background: #27ae60;
                color: white;
                border-radius: 50%;
                width: 16px;
                height: 16px;
                font-size: 10px;
                display: flex;
                align-items: center;
                justify-content: center;
                font-weight: bold;
            `;
            completedWrapper.appendChild(completedLabel);
            container.appendChild(completedWrapper);
        } else {
            // 正常渲染
            this.renderNodeContent(node, container);
        }
    }

    /**
     * 渲染节点内容（不包含完成状态包装）
     */
    renderNodeContent(node, container) {
        if (node.type === 'value') {
            this.renderValueNode(node, container);
        } else if (node.type === 'operator') {
            this.renderOperatorNodeAST(node, container);
        } else if (node.type === 'functionCall') {
            this.renderFunctionCallNodeAST(node, container);
        } else if (node.type === 'listQuery' || node.type === 'listSort' || node.type === 'listTake') {
            this.renderListQueryNodeAST(node, container);
        }
    }

    renderPipelineBlock(node, container, renderNodeFn) {
        const pipeline = extractListPipeline(node);
        if (!pipeline || !pipeline.source) return false;

        const pipelineWrapper = document.createElement("span");
        pipelineWrapper.className = "dsl-pipeline-wrap";
        pipelineWrapper.style.cssText = `
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 4px 8px;
            margin: 2px;
            background: #e8f4ff;
            border: 1px solid #90caf9;
            border-radius: 14px;
            flex-wrap: wrap;
        `;

        const sourceIsEncapsulated = pipeline.source?.type === 'value' && pipeline.source?.metadata?.type === 'encapsulated';
        if (sourceIsEncapsulated) {
            renderNodeFn(pipeline.source, pipelineWrapper);
        } else {
            const sourceWrapper = document.createElement("span");
            sourceWrapper.style.cssText = `
                display: inline-flex;
                align-items: center;
                gap: 4px;
                background: linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%);
                border: 2px solid #27ae60;
                border-radius: 18px;
                padding: 4px 10px;
                color: #2c3e50;
                font-weight: 600;
                box-shadow: 0 2px 4px rgba(39, 174, 96, 0.2);
            `;
            renderNodeFn(pipeline.source, sourceWrapper);
            pipelineWrapper.appendChild(sourceWrapper);
        }

        pipeline.stages.forEach((stage) => {
            const meta = PIPELINE_STAGE_META[stage.type];
            const label = meta?.label || stage.type || "阶段";
            const stageLabel = document.createElement("span");
            stageLabel.textContent = label;
            stageLabel.style.cssText = `
                display: inline-flex;
                align-items: center;
                gap: 4px;
                padding: 3px 8px;
                border-radius: 10px;
                background: #f3f9ff;
                border: 1px solid #bbdefb;
                color: #0d47a1;
                font-size: 12px;
                font-weight: 600;
            `;
            pipelineWrapper.appendChild(stageLabel);

            let expressionNode = null;
            let slotName = '';
            if (stage.node?.type === 'listQuery') {
                expressionNode = stage.node.filterExpression;
                slotName = 'filterExpression';
            } else if (stage.node?.type === 'listSort') {
                expressionNode = stage.node.sortExpression;
                slotName = 'sortExpression';
            } else if (stage.node?.type === 'listTake') {
                expressionNode = stage.node.countExpression;
                slotName = 'countExpression';
            }

            if (expressionNode && expressionNode.metadata?.type !== 'empty') {
                renderNodeFn(expressionNode, pipelineWrapper);
            } else if (stage.node && slotName) {
                this.renderEmptySlotPlaceholder(stage.node, slotName, pipelineWrapper);
            }
        });

        container.appendChild(pipelineWrapper);
        return true;
    }

    renderFunctionCallNodeAST(node, container) {
        const wrapper = document.createElement('span');
        wrapper.className = 'dsl-node dsl-function-call';
        wrapper.style.cssText = `
            background: #e8f5e8;
            color: #388e3c;
            padding: 4px 8px;
            border-radius: 4px;
            font-weight: 600;
            margin: 2px;
            display: inline-flex;
            align-items: center;
            gap: 2px;
        `;

        const name = document.createElement('span');
        name.textContent = node.functionName;
        wrapper.appendChild(name);

        const open = document.createElement('span');
        open.textContent = '(';
        wrapper.appendChild(open);

        const args = node.args || [];
        for (let i = 0; i < args.length; i++) {
            if (i > 0) {
                const comma = document.createElement('span');
                comma.textContent = ', ';
                wrapper.appendChild(comma);
            }

            const arg = args[i];
            if (arg) {
                this.renderASTNodeRecursive(arg, wrapper);
            } else {
                this.renderEmptySlotPlaceholder(node, 'arg', wrapper, i);
            }
        }

        const close = document.createElement('span');
        close.textContent = ')';
        wrapper.appendChild(close);

        container.appendChild(wrapper);
    }

    /**
     * 渲染值节点
     */
    renderValueNode(node, container) {
        console.log("DslEditor: renderValueNode called for:", {
            type: node.type,
            value: node.value,
            hasMetadata: !!node.metadata,
            metadataType: node.metadata?.type,
            isEncapsulated: node.metadata && node.metadata.type === "encapsulated"
        });

        if (node.metadata?.type === 'empty') {
            const placeholderInfo = this.resolveEmptySlot(node.parent, node);
            if (placeholderInfo.slot) {
                this.renderEmptySlotPlaceholder(node.parent, placeholderInfo.slot, container, placeholderInfo.index);
            }
            return;
        }

        // 特殊处理封装的完成表达式
        if (node.metadata && node.metadata.type === "encapsulated") {
            console.log("DslEditor: Rendering as encapsulated expression");
            this.renderEncapsulatedExpression(node, container);
            return;
        }

        const nodeElement = document.createElement("span");
        nodeElement.className = "dsl-node dsl-value-node";

        // 根据值节点的类型设置样式和内容
        if (node.metadata && node.metadata.type === "context") {
            nodeElement.textContent = this.getReadableValueLabel(node);
            nodeElement.style.cssText = `
                background: #e3f2fd;
                color: #1976d2;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 500;
                margin: 2px;
                display: inline-block;
            `;
        } else if (node.metadata.type === "property") {
            nodeElement.textContent = this.getReadableValueLabel(node);
            nodeElement.style.cssText = `
                background: #f3e5f5;
                color: #7b1fa2;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 500;
                margin: 2px;
                display: inline-block;
            `;
        } else if (node.metadata.type === "staticValue") {
            if (node.metadata.isAwaitingInput) {
                // 渲染输入框
                this.renderStaticValueInput(node, container);
                return;
            } else {
                nodeElement.textContent = this.getReadableValueLabel(node);
                nodeElement.style.cssText = `
                    background: #fff3e0;
                    color: #f57c00;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-weight: 500;
                    margin: 2px;
                    display: inline-block;
                `;
            }
        } else if (node.metadata.type === "rawText") {
            this.renderRawTextInput(node, container);
            return;
        } else if (node.metadata.type === "function") {
            nodeElement.textContent = this.getReadableValueLabel(node);
            nodeElement.style.cssText = `
                background: #e8f5e8;
                color: #388e3c;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 500;
                margin: 2px;
                display: inline-block;
            `;
        } else if (node.metadata.type === "eventField") {
            nodeElement.textContent = this.getReadableValueLabel(node);
            nodeElement.style.cssText = `
                background: #ede7f6;
                color: #5e35b1;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 600;
                margin: 2px;
                display: inline-block;
            `;
        }

        container.appendChild(nodeElement);
    }

    /**
     * 渲染封装的完成表达式（胶囊样式）
     */
    renderEncapsulatedExpression(node, container) {
        // 关键修复：检查是否为空占位符
        if (node.metadata.innerAST && node.metadata.innerAST.metadata?.type === 'empty') {
            this.renderPlaceholderCapsule(node, container);
            return;
        }

        const capsuleElement = document.createElement("span");
        capsuleElement.className = "dsl-encapsulated-expression";
    
        const style = this.getEncapsulationStyle(node);
        const isSelected = this.selectedNode === node;
    
        capsuleElement.style.cssText = `
            display: inline-flex;
            align-items: center;
            background: ${style.background};
            border: ${isSelected ? '3px' : '2px'} solid ${style.borderColor};
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: ${isSelected ? '900' : '500'};
            color: ${style.color};
            box-shadow: ${style.boxShadow};
            position: relative;
            cursor: pointer;
            transition: all 0.2s ease;
        `;
    
        capsuleElement.addEventListener('mouseenter', () => {
            capsuleElement.style.transform = 'scale(1.02)';
            capsuleElement.style.boxShadow = style.hoverBoxShadow;
        });
    
        capsuleElement.addEventListener('mouseleave', () => {
            capsuleElement.style.transform = 'scale(1)';
            capsuleElement.style.boxShadow = style.boxShadow;
        });
    
        const contentSpan = document.createElement("span");
        contentSpan.style.cssText = `color: ${style.color}; font-weight: ${isSelected ? '900' : '500'};`;
    
        if (node.metadata.innerAST) {
            this.renderClickableSubExpressions(node.metadata.innerAST, contentSpan, node, isSelected);
        } else {
            contentSpan.textContent = this.removeBrackets(node.value);
        }
    
        capsuleElement.appendChild(contentSpan);
    
        const badge = this.createStatusBadge(style);
        if (badge) {
            capsuleElement.appendChild(badge);
        }
    
        capsuleElement.addEventListener('click', (e) => {
            e.stopPropagation();
            console.log("DslEditor: Clicked main encapsulated expression. Entering edit mode for virtualRoot's main slot.");
            // 关键修复：在进入编辑模式前，设置当前选中节点
            this.selectedNode = node;
            // 统一逻辑：点击主表达式的封装节点，即进入对 virtualRoot 的 main 槽位的编辑模式
            this.handleEnterEditModeForSlot(this.virtualRoot, 'main');
        });
    
        container.appendChild(capsuleElement);
    }
/**
     * 渲染一个“待编辑”状态的占位符胶囊
     * @param {ASTNode} node - 封装了 empty 节点的父节点
     * @param {HTMLElement} container - 渲染的目标容器
     */
    renderPlaceholderCapsule(node, container) {
        const placeholderCapsule = document.createElement("span");
        placeholderCapsule.className = "dsl-placeholder-capsule";
        const isSelected = this.selectedNode === node;

        placeholderCapsule.style.cssText = `
            display: inline-flex;
            align-items: center;
            background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%);
            border: ${isSelected ? '3px' : '2px'} solid #ffc107;
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: 700;
            color: #856404;
            box-shadow: 0 0 8px rgba(255, 193, 7, 0.6);
            cursor: pointer;
            transition: all 0.2s ease;
            position: relative;
            animation: pulse 2s infinite;
        `;
 
        // 确保脉冲动画的 keyframes 存在
        if (!document.getElementById('placeholder-pulse-style')) {
            const style = document.createElement('style');
            style.id = 'placeholder-pulse-style';
            style.textContent = `
                @keyframes pulse {
                    0% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                    50% { box-shadow: 0 0 16px rgba(255, 193, 7, 0.9); }
                    100% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                }
            `;
            document.head.appendChild(style);
        }
 
        placeholderCapsule.textContent = "?";
 
        placeholderCapsule.addEventListener('click', (e) => {
            e.stopPropagation();
            this.selectedNode = node;
            this.handleEnterEditModeForSlot(this.virtualRoot, 'main');
        });

        container.appendChild(placeholderCapsule);
    }

    /**
     * 根据节点状态获取封装表达式的样式
     * @param {ASTNode} node - 封装节点
     * @returns {object} - 包含样式属性的对象
     */
    getEncapsulationStyle(node) {
        const typeMatch = node._typeMatchInfo || this.calculateNodeTypeMatch(node);
        const hasTypeIssue = !typeMatch.isTypeMatch && typeMatch.needsCorrection;

        if (hasTypeIssue) {
            return {
                background: "linear-gradient(135deg, #ffebee 0%, #ffcdd2 100%)",
                borderColor: "#e74c3c",
                color: "#c62828",
                boxShadow: "0 2px 4px rgba(231, 76, 60, 0.2)",
                hoverBoxShadow: "0 4px 8px rgba(231, 76, 60, 0.3)",
                badgeSymbol: "×",
                badgeColor: "#e74c3c",
                badgeTitle: `类型不匹配：期望 ${typeMatch.expectedType}，实际 ${typeMatch.actualType}`,
                hasIssue: true
            };
        }

        return {
            background: "linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%)",
            borderColor: "#27ae60",
            color: "#2c3e50",
            boxShadow: "0 2px 4px rgba(39, 174, 96, 0.2)",
            hoverBoxShadow: "0 4px 8px rgba(39, 174, 96, 0.3)",
            badgeSymbol: "✓",
            badgeColor: "#27ae60",
            badgeTitle: "表达式已完成",
            hasIssue: false
        };
    }

    /**
     * 创建状态徽章元素
     * @param {object} style - 从 getEncapsulationStyle 获取的样式对象
     * @returns {HTMLElement|null}
     */
    createStatusBadge(style) {
        const badge = document.createElement("span");
        badge.textContent = style.badgeSymbol;
        badge.title = style.badgeTitle;

        badge.style.cssText = `
            position: absolute;
            top: -6px;
            right: -6px;
            background: ${style.badgeColor};
            color: white;
            border-radius: 50%;
            width: 16px;
            height: 16px;
            font-size: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            border: 2px solid white;
            ${style.hasIssue ? 'animation: pulse 2s infinite;' : ''}
        `;
        return badge;
    }

    /**
     * 渲染嵌套的封装表达式（内部样式框）
     */
    renderNestedEncapsulatedExpression(innerNode, container, parentEncapsulatedNode) {
        const nestedCapsule = document.createElement("span");
        nestedCapsule.className = "dsl-nested-encapsulated-expression";

        // 使用与外层相同的绿色样式
        const isSelected = this.selectedNode === innerNode;
        nestedCapsule.style.cssText = `
            display: inline-flex;
            align-items: center;
            background: linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%);
            border: ${isSelected ? '3px' : '2px'} solid #27ae60;
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: ${isSelected ? '900' : '500'};
            color: #2c3e50;
            box-shadow: 0 2px 4px rgba(39, 174, 96, 0.2);
            cursor: pointer;
            transition: all 0.2s ease;
            position: relative;
        `;

        // 悬停效果
        nestedCapsule.addEventListener('mouseenter', () => {
            if (this.selectedNode !== innerNode) {
                nestedCapsule.style.transform = 'scale(1.02)';
                nestedCapsule.style.boxShadow = '0 4px 8px rgba(39, 174, 96, 0.3)';
            }
        });

        nestedCapsule.addEventListener('mouseleave', () => {
            if (this.selectedNode !== innerNode) {
                nestedCapsule.style.transform = 'scale(1)';
                nestedCapsule.style.boxShadow = '0 2px 4px rgba(39, 174, 96, 0.2)';
            }
        });

        // 唯一的点击事件：只在嵌套样式框上绑定
        nestedCapsule.addEventListener('click', (e) => {
            e.stopPropagation();
            console.log('点击嵌套封装表达式:', innerNode);

            // 设置选中状态
            this.selectedNode = innerNode;

            // 重新渲染以显示选中效果
            this.renderEditor();

            // 编辑嵌套的子表达式
            this.editSubExpression(innerNode);
        });

        // 渲染嵌套内容
        const nestedContent = document.createElement("span");
        nestedContent.style.cssText = `color: #2c3e50; font-weight: ${isSelected ? '900' : '500'};`;

        // 递归渲染嵌套内容
        if (innerNode.metadata.innerAST) {
            this.renderClickableSubExpressions(innerNode.metadata.innerAST, nestedContent, innerNode, isSelected);
        } else {
            const displayText = innerNode.metadata.displayValue || innerNode.value;
            nestedContent.textContent = this.removeBrackets(displayText);
        }

        nestedCapsule.appendChild(nestedContent);

        // 关键修复：检查嵌套节点的类型匹配
        let hasTypeIssue = false;
        let typeIssueDetails = null;

        // 关键修复：直接使用innerNode，它应该有正确的父子关系和类型匹配信息
        console.log("DslEditor: Nested encapsulated expression analysis:", {
            nodeValue: innerNode.value,
            nodeType: innerNode.type,
            hasParent: !!innerNode.parent,
            parentOperator: innerNode.parent?.operator,
            hasTypeMatchInfo: !!innerNode._typeMatchInfo,
            hasMetadata: !!innerNode.metadata,
            metadataType: innerNode.metadata?.type
        });

        // 检查当前节点的类型匹配
        if (innerNode._typeMatchInfo) {
            const typeMatch = innerNode._typeMatchInfo;
            hasTypeIssue = !typeMatch.isTypeMatch && typeMatch.needsCorrection;
            if (hasTypeIssue) {
                typeIssueDetails = `类型不匹配：期望 ${typeMatch.expectedType}，实际 ${typeMatch.actualType}`;
            }
            console.log("DslEditor: Nested encapsulated expression type check:", {
                nodeValue: innerNode.value,
                expectedType: typeMatch.expectedType,
                actualType: typeMatch.actualType,
                isTypeMatch: typeMatch.isTypeMatch,
                needsCorrection: typeMatch.needsCorrection,
                hasTypeIssue: hasTypeIssue
            });
        } else {
            console.log("DslEditor: No type match info for nested encapsulated expression");
        }

        // 添加完成标记（根据类型匹配情况显示✓或×）
        const completedBadge = document.createElement("span");
        completedBadge.textContent = hasTypeIssue ? "×" : "✓";

        // 根据类型匹配情况设置样式
        const badgeColor = hasTypeIssue ? "#e74c3c" : "#27ae60";
        const badgeBackground = hasTypeIssue ? "#ffebee" : "#e8f5e8";

        completedBadge.style.cssText = `
            position: absolute;
            top: -6px;
            right: -6px;
            background: ${badgeColor};
            color: white;
            border-radius: 50%;
            width: 16px;
            height: 16px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 10px;
            font-weight: bold;
            box-shadow: 0 1px 2px rgba(0,0,0,0.2);
            ${hasTypeIssue ? 'animation: pulse 2s infinite;' : ''}
        `;

        // 添加悬停提示
        if (hasTypeIssue) {
            completedBadge.title = typeIssueDetails;
            // 添加脉冲动画样式
            if (!document.getElementById('pulse-animation-style')) {
                const style = document.createElement('style');
                style.id = 'pulse-animation-style';
                style.textContent = `
                    @keyframes pulse {
                        0% { box-shadow: 0 0 0 0 rgba(231, 76, 60, 0.7); }
                        70% { box-shadow: 0 0 0 10px rgba(231, 76, 60, 0); }
                        100% { box-shadow: 0 0 0 0 rgba(231, 76, 60, 0); }
                    }
                `;
                document.head.appendChild(style);
            }
        }

        nestedCapsule.appendChild(completedBadge);

        container.appendChild(nestedCapsule);
    }

    /**
     * 移除文本中的外层括号
     */
    removeBrackets(text) {
        if (typeof text !== 'string') return text;

        // 移除最外层的括号
        if (text.startsWith('(') && text.endsWith(')')) {
            return text.slice(1, -1);
        }
        return text;
    }

    /**
     * 渲染可点击的子表达式（支持嵌套样式框）
     */
    renderClickableSubExpressions(innerAST, container, encapsulatedNode, isParentSelected = false) {
        if (!innerAST) {
            const displayText = encapsulatedNode.metadata.displayValue || encapsulatedNode.value;
            container.textContent = this.removeBrackets(displayText);
            return;
        }

        // 关键修复：如果内部AST也是封装表达式，需要渲染为嵌套样式框
        if (innerAST.type === 'value' && innerAST.metadata && innerAST.metadata.type === 'encapsulated') {
            // 内部也是封装表达式，渲染为嵌套样式框
            this.renderNestedEncapsulatedExpression(innerAST, container, encapsulatedNode);
        } else {
            // 普通AST节点，直接渲染
            this.renderClickableASTNode(innerAST, container, encapsulatedNode, isParentSelected);
        }
    }

    /**
     * 渲染可点击的AST节点
     */
    renderClickableASTNode(node, container, encapsulatedNode, isParentSelected = false) {
        if (!node) return;

        const highlightTarget = this.getHighlightTargetNode ? this.getHighlightTargetNode() : null;

        if (node.type === 'value') {
            // 关键修复：优先处理空节点占位符
            if (node.metadata?.type === 'empty') {
                const placeholderInfo = this.resolveEmptySlot(node.parent, node);
                if (placeholderInfo.slot) {
                    this.renderEmptySlotPlaceholder(node.parent, placeholderInfo.slot, container, placeholderInfo.index);
                }
                return;
            }

            // 关键修复：如果是封装表达式，渲染为嵌套样式框
            if (node.metadata && node.metadata.type === 'encapsulated') {
                this.renderNestedEncapsulatedExpression(node, container, encapsulatedNode);
                return;
            }

            // 普通值节点：可点击编辑
            const valueSpan = document.createElement("span");
            valueSpan.textContent = this.getReadableValueLabel(node);

            valueSpan.style.cssText = `
                padding: 2px 4px;
                border-radius: 3px;
            `;
            // 精准高亮
            if (highlightTarget === node) {
                valueSpan.style.outline = '2px solid #0d6efd';
                valueSpan.style.outlineOffset = '2px';
            }

            // 选中高亮：加粗边框和文字
            if (this.selectedNode === node) {
                valueSpan.style.border = '3px solid #27ae60';
                valueSpan.style.fontWeight = '900';
                valueSpan.style.borderRadius = '4px';
            } else if (isParentSelected) {
               valueSpan.style.fontWeight = '900';
            }

            // 移除内部点击事件 - 点击应该由外层样式框处理

            container.appendChild(valueSpan);
        } else if (node.type === 'operator') {
            // 操作符节点：简单渲染，不添加点击事件（由外层样式框处理）

            // 渲染左操作数
            if (node.leftOperand) {
                this.renderClickableASTNode(node.leftOperand, container, encapsulatedNode);
            }

            // 操作符
            const operatorSpan = document.createElement("span");
            operatorSpan.textContent = ` ${node.operator} `;
            operatorSpan.style.cssText = "color: #666; font-weight: bold; margin: 0 4px;";

            // 选中高亮：如果当前操作符被选中，加粗显示
            if (this.selectedNode === node || isParentSelected) {
                operatorSpan.style.fontWeight = '900';
                operatorSpan.style.color = '#27ae60';
            }

            container.appendChild(operatorSpan);

            // 渲染右操作数
            if (node.rightOperand) {
                this.renderClickableASTNode(node.rightOperand, container, encapsulatedNode);
            } else {
                // 右操作数为空：渲染占位符
                this.renderEmptySlotPlaceholder(node, 'right', container);
            }
        } else if (node.type === 'listQuery' || node.type === 'listSort' || node.type === 'listTake') {
            // 关键修复：渲染列表操作节点
            this.renderClickableListNode(node, container, encapsulatedNode);
        }
    }

    /**
     * 编辑子表达式（修复版：正确处理子表达式编辑范围）
     */
    editSubExpression(subNode) {
        console.log("DslEditor: Editing sub-expression:", subNode);
    
        // 设置选中状态，用于高亮显示（持久选中）
        this.selectedNode = subNode;
    
        // 兼容：如果传入的是封装节点的 innerAST，自动提升到封装节点本身
        if (subNode?.parent?.type === 'value' && subNode?.parent?.metadata?.type === 'encapsulated' && subNode?.parent?.metadata?.innerAST === subNode) {
            this.selectedNode = subNode.parent;
            return this.editSubExpression(subNode.parent);
        }

        // 根节点：统一走 main 槽位编辑
        if (!subNode?.parent) {
            this.handleEnterEditModeForSlot(this.virtualRoot, 'main');
            return;
        }

        const parent = subNode.parent;

        // 操作符左右槽位
        if (parent.type === 'operator') {
            if (parent.leftOperand === subNode) {
                this.handleEnterEditModeForSlot(parent, 'left');
                return;
            }
            if (parent.rightOperand === subNode) {
                this.handleEnterEditModeForSlot(parent, 'right');
                return;
            }
        }

        // 函数参数槽位
        if (parent.type === 'functionCall') {
            const args = parent.args || [];
            const idx = args.indexOf(subNode);
            if (idx >= 0) {
                this.handleEnterEditModeForSlot(parent, 'arg', idx);
                return;
            }
        }

        // 集合操作槽位
        if (parent.type === 'listQuery' && parent.filterExpression === subNode) {
            this.handleEnterEditModeForSlot(parent, 'filterExpression');
            return;
        }
        if (parent.type === 'listSort' && parent.sortExpression === subNode) {
            this.handleEnterEditModeForSlot(parent, 'sortExpression');
            return;
        }
        if (parent.type === 'listTake' && parent.countExpression === subNode) {
            this.handleEnterEditModeForSlot(parent, 'countExpression');
            return;
        }

        console.error("DslEditor: Cannot determine editable slot for subNode in its parent.", { subNode, parent });
    }

    /**
     * 判断是否是简单值节点（如 (5), (6) 等单纯的数值）
     */
    isSimpleValueNode(node) {
        if (!node.metadata || !node.metadata.innerAST) {
            return false; // 没有内部AST，不是封装节点
        }

        const innerAST = node.metadata.innerAST;

        // 如果内部AST是简单值节点且没有进一步的嵌套
        if (innerAST.type === 'value' &&
            (!innerAST.metadata ||
             (innerAST.metadata.type !== 'encapsulated' &&
              innerAST.metadata.type !== 'operator'))) {
            return true;
        }

        return false;
    }

    /**
     * 判断是否是叶子表达式（不包含子表达式的简单表达式）
     */
    isLeafExpression(node) {
        if (!node.metadata || !node.metadata.innerAST) {
            return true; // 没有内部AST，是叶子表达式
        }

        const innerAST = node.metadata.innerAST;

        // 如果内部AST是简单值节点（没有子结构），是叶子表达式
        if (innerAST.type === 'value' && (!innerAST.metadata || innerAST.metadata.type !== 'encapsulated')) {
            return true;
        }

        // 如果内部AST是操作符节点，不是叶子表达式
        if (innerAST.type === 'operator') {
            return false;
        }

        // 如果内部AST是封装表达式，不是叶子表达式
        if (innerAST.type === 'value' && innerAST.metadata && innerAST.metadata.type === 'encapsulated') {
            return false;
        }

        // 其他情况默认为叶子表达式
        return true;
    }




    /**
     * 克隆AST节点
     */
    cloneAST(node) {
        if (!node) return null;
    
        // 深克隆 metadata
        let deepClonedMetadata = {};
        if (node.metadata) {
            for (const key in node.metadata) {
                if (Object.prototype.hasOwnProperty.call(node.metadata, key)) {
                    if (key === 'innerAST') {
                        // 递归克隆 innerAST
                        deepClonedMetadata[key] = this.cloneAST(node.metadata[key]);
                    } else if (typeof node.metadata[key] === 'object' && node.metadata[key] !== null) {
                        // 对其他对象进行简单的结构化克隆（适用于JSON兼容对象）
                        try {
                            deepClonedMetadata[key] = JSON.parse(JSON.stringify(node.metadata[key]));
                        } catch (e) {
                            deepClonedMetadata[key] = node.metadata[key]; // 回退
                        }
                    } else {
                        deepClonedMetadata[key] = node.metadata[key];
                    }
                }
            }
        }
    
        if (node.type === 'value') {
            const cloned = createValueNode(node.value, node.returnType, deepClonedMetadata);
            cloned.isComplete = node.isComplete;
            return cloned;
        } else if (node.type === 'operator') {
            const cloned = createOperatorNode(node.operator, node.returnType);
            if (node.leftOperand) {
                cloned.setLeftOperand(this.cloneAST(node.leftOperand));
            }
            if (node.rightOperand) {
                cloned.setRightOperand(this.cloneAST(node.rightOperand));
            }
            cloned.isComplete = node.isComplete;
            return cloned;
        } else if (node.type === 'functionCall') {
            const cloned = createFunctionCallNode(node.functionName, node.returnType, Array.isArray(node.parameters) ? [...node.parameters] : []);
            (node.args || []).forEach((arg, idx) => {
                if (arg) cloned.setArg(idx, this.cloneAST(arg));
            });
            cloned.isComplete = node.isComplete;
            return cloned;
        } else if (node.type === 'listQuery') {
            const cloned = createListQueryNode(this.cloneAST(node.source));
            if (node.filterExpression) {
                cloned.setFilterExpression(this.cloneAST(node.filterExpression));
            }
            return cloned;
        } else if (node.type === 'listSort') {
            const cloned = createListSortNode(this.cloneAST(node.source));
            if (node.sortExpression) {
                cloned.setSortExpression(this.cloneAST(node.sortExpression));
            }
            return cloned;
        } else if (node.type === 'listTake') {
            const cloned = createListTakeNode(this.cloneAST(node.source));
            if (node.countExpression) {
                cloned.setCountExpression(this.cloneAST(node.countExpression));
            }
            return cloned;
        }
    
        return null;
    }

    /**
     * 渲染可点击的列表操作节点（如 .query(), .sort()）
     */
    renderClickableListNode(node, container, encapsulatedNode) {
        if (node.parent && isListOperationNode(node.parent)) {
            return;
        }
        const rendered = this.renderPipelineBlock(node, container, (target, targetContainer) => {
            this.renderClickableASTNode(target, targetContainer, encapsulatedNode);
        });
        if (rendered) return;

        // 渲染源节点
        if (node.source) {
            this.renderClickableASTNode(node.source, container, encapsulatedNode);
        }

        // 渲染操作符（例如 .query）
        const operatorSpan = document.createElement("span");
        let operatorName = node.type.replace('list', '').toLowerCase();
        if (node.type === 'listSort') {
            operatorName = 'orderBy';
        }
        operatorSpan.textContent = `.${operatorName}`;
        // 关键修复：应用与标准操作符完全相同的样式，但移除外边距以实现紧密连接
        operatorSpan.style.cssText = ""; // Remove bold style
        container.appendChild(operatorSpan);

        // 渲染参数表达式或占位符
        let expressionNode = null;
        let slotName = '';

        if (node.type === 'listQuery') {
            expressionNode = node.filterExpression;
            slotName = 'filterExpression';
        } else if (node.type === 'listSort') {
            expressionNode = node.sortExpression;
            slotName = 'sortExpression';
        } else if (node.type === 'listTake') {
            expressionNode = node.countExpression;
            slotName = 'countExpression';
        }

        if (expressionNode && expressionNode.metadata?.type !== 'empty') {
            this.renderClickableASTNode(expressionNode, container, encapsulatedNode);
        } else {
            // 关键修复：调用标准的占位符渲染函数
            this.renderEmptySlotPlaceholder(node, slotName, container);
        }
    }

    renderListQueryNodeAST(node, container) {
        if (node.parent && isListOperationNode(node.parent)) {
            return;
        }
        const rendered = this.renderPipelineBlock(node, container, this.renderASTNodeRecursive.bind(this));
        if (rendered) return;

        const queryContainer = document.createElement("div");
        queryContainer.className = "dsl-list-query-container";
        queryContainer.style.cssText = `
            display: inline-flex;
            align-items: center;
            margin: 2px;
        `;

        // 渲染源节点
        if (node.source) {
            this.renderASTNodeRecursive(node.source, queryContainer);
        }

        // 渲染 .query 箭头标签
        const queryElement = document.createElement("div");
        queryElement.style.cssText = `
            display: flex;
            align-items: center;
            position: relative;
            background: #e1f5fe;
            color: #0277bd;
            padding: 4px 12px;
            font-weight: 500;
            margin: 0 12px; /* 为两侧箭头留出空间 */
        `;
        let operatorName = node.type.replace('list', '').toLowerCase();
        if (node.type === 'listSort') {
            operatorName = 'orderBy';
        }
        queryElement.textContent = `.${operatorName}`;

        const arrowLeft = document.createElement("div");
        arrowLeft.style.cssText = `
            position: absolute;
            left: -10px;
            top: 50%;
            transform: translateY(-50%);
            width: 0;
            height: 0;
            border-top: 10px solid transparent;
            border-bottom: 10px solid transparent;
            border-right: 10px solid #e1f5fe;
        `;

        const arrowRight = document.createElement("div");
        arrowRight.style.cssText = `
            position: absolute;
            right: -10px;
            top: 50%;
            transform: translateY(-50%);
            width: 0;
            height: 0;
            border-top: 10px solid transparent;
            border-bottom: 10px solid transparent;
            border-left: 10px solid #e1f5fe;
        `;

        queryElement.appendChild(arrowLeft);
        queryElement.appendChild(arrowRight);
        queryContainer.appendChild(queryElement);

        // 智能渲染参数部分
        let expressionNode = null;
        if (node.type === 'listQuery') {
            expressionNode = node.filterExpression;
        } else if (node.type === 'listSort') {
            expressionNode = node.sortExpression;
        } else if (node.type === 'listTake') {
            expressionNode = node.countExpression;
        }

        // 统一调用渲染，renderASTNodeRecursive 会正确处理 empty 节点
        if (expressionNode) {
            this.renderASTNodeRecursive(expressionNode, queryContainer);
        } else {
            const slotName = node.type === 'listQuery'
                ? 'filterExpression'
                : node.type === 'listSort'
                    ? 'sortExpression'
                    : 'countExpression';
            this.renderEmptySlotPlaceholder(node, slotName, queryContainer);
        }

        container.appendChild(queryContainer);
    }

    renderOperatorNodeAST(node, container) {
        const operatorContainer = document.createElement("div");
        operatorContainer.className = "dsl-operator-container";
        operatorContainer.style.cssText = `
            display: inline-flex;
            align-items: center;
            gap: 8px;
            margin: 2px;
        `;

        // 源节点或左操作数
        const leftSource = node.source || node.leftOperand;
        if (leftSource) {
            this.renderASTNodeRecursive(leftSource, operatorContainer);
        }

        // 操作符或方法
        const operatorElement = document.createElement("span");
        operatorElement.className = "dsl-operator";
        if (node.type === 'operator') {
            operatorElement.textContent = node.operator;
            operatorElement.style.cssText = `
                background: #ffebee;
                color: #c62828;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: bold;
                margin: 0 4px;
            `;
        } else {
            operatorElement.textContent = `.${node.type.replace('list', '').toLowerCase()}`;
            operatorElement.style.cssText = `
                background: #e0f7fa;
                color: #00796b;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: bold;
            `;
        }
        operatorContainer.appendChild(operatorElement);

        // 参数或右操作数
        const parenLeft = document.createElement("span");
        parenLeft.textContent = "(";
        operatorContainer.appendChild(parenLeft);

        let rightOperand = null;
        let slotName = '';
        if (node.type === 'operator') {
            rightOperand = node.rightOperand;
            slotName = 'right';
        } else if (node.type === 'listQuery') {
            rightOperand = node.filterExpression;
            slotName = 'filterExpression';
        } else if (node.type === 'listSort') {
            rightOperand = node.sortExpression;
            slotName = 'sortExpression';
        } else if (node.type === 'listTake') {
            rightOperand = node.countExpression;
            slotName = 'countExpression';
        }

        if (rightOperand && rightOperand.metadata?.type !== 'empty') {
            this.renderASTNodeRecursive(rightOperand, operatorContainer);
        } else {
            this.renderEmptySlotPlaceholder(node, slotName, operatorContainer);
        }

        const parenRight = document.createElement("span");
        parenRight.textContent = ")";
        operatorContainer.appendChild(parenRight);

        container.appendChild(operatorContainer);
    }

    /**
     * 渲染静态值输入框 (AST版本)
     */
    renderStaticValueInput(node, container) {
        const inputContainer = document.createElement("div");
        inputContainer.className = "dsl-static-input-container";
        inputContainer.style.cssText = `
            display: inline-block;
            margin: 2px;
        `;

        const inputElement = document.createElement("input");
        inputElement.type = "text";
        inputElement.className = "dsl-static-input";
        inputElement.placeholder = "输入值";
        inputElement.value = node.metadata.staticValue || "";
        inputElement.style.cssText = `
            border: 1px solid #ddd;
            border-radius: 4px;
            padding: 4px 8px;
            font-size: 12px;
            width: 80px;
            background: #fff3e0;
        `;

        // 输入事件处理
        inputElement.addEventListener("input", (e) => {
            const value = e.target.value.trim();

            // 更新节点值
            node.metadata.staticValue = value;
            if (value) {
                node.value = this.formatStaticValue(value, this.inferStaticValueType(value));
                node.returnType = this.inferStaticValueType(value);
                node.metadata.isAwaitingInput = false;
            } else {
                node.value = "[输入值]";
                node.returnType = "Object";
                node.metadata.isAwaitingInput = true;
            }

            // 更新节点完整性
            node.updateCompleteness();

            // 如果节点有父节点（操作符），也更新父节点的完整性
            if (node.parent) {
                node.parent.updateCompleteness();
            }

            // 更新表达式
            this.updateExpressionFromAST();

            if (this.onChange) {
                this.onChange(this.expression);
            }
        });

        inputContainer.appendChild(inputElement);
        container.appendChild(inputContainer);

        // 自动聚焦
        setTimeout(() => inputElement.focus(), 100);
    }

    /**
     * 渲染原始文本输入框（解析失败降级）
     */
    renderRawTextInput(node, container) {
        const inputContainer = document.createElement("div");
        inputContainer.className = "dsl-rawtext-input-container";
        inputContainer.style.cssText = `
            display: inline-block;
            margin: 2px;
            width: 100%;
        `;

        const inputElement = document.createElement("input");
        inputElement.type = "text";
        inputElement.className = "dsl-rawtext-input";
        inputElement.placeholder = "输入 DSL 表达式";
        inputElement.value = node.value || node.metadata?.rawText || "";
        inputElement.style.cssText = `
            border: 1px solid #90caf9;
            border-radius: 4px;
            padding: 4px 8px;
            font-size: 12px;
            min-width: 240px;
            width: 100%;
            background: #e3f2fd;
        `;

        const commit = async () => {
            const value = inputElement.value.trim();
            if (!value) {
                this.showExpressionError("表达式不能为空。");
                return;
            }
            if (value === (node.value || "").trim()) {
                return;
            }
            await this.applyRawTextEdit(value);
        };

        inputElement.addEventListener("keydown", (e) => {
            if (e.key === "Enter") {
                e.preventDefault();
                inputElement.blur();
            }
        });

        inputElement.addEventListener("blur", () => {
            commit();
        });

        inputContainer.appendChild(inputElement);
        container.appendChild(inputContainer);
    }

    async applyRawTextEdit(expression) {
        this.setParseStatus("loading", "正在解析...");
        await this.loadExpressionWithParseApi(expression);
        this.updateExpressionFromAST();
    }


    /**
     * 为列表查询操作渲染.query()的子编辑器
     */

    /**
     * 退出子编辑器，返回到父编辑器
     */
    exitSubEditor() {
        if (this.isSubEditor && this.parentEditor) {
            console.log('DslEditor: Exiting sub-editor, returning to parent');
            // 清空子编辑器的表达式
            if (this.onChange) {
                this.onChange("");
            }
            // 重新渲染父编辑器以移除子编辑器焦点
            this.parentEditor.renderEditor();
        }
    }

    /**
     * 退回上级节点 (AST重构版)
     */
    goBack() {
        // 如果是子编辑器且没有AST，则退出子编辑器
        if (this.isSubEditor && !this.astRoot) {
            this.exitSubEditor();
            return;
        }

        if (!this.astRoot) return;

        console.log('DslEditor: Going back from AST:', this.astRoot);

        // 重置表达式完成状态
        this.isExpressionCompleted = false;

        // 实现AST的撤回逻辑
        const backResult = this.performASTGoBack();

        if (backResult.success) {
            this.astRoot = backResult.newRoot;

            // 如果是子编辑器，在回到根节点后检查是否需要退出
            if (this.isSubEditor && !this.astRoot) {
                // 在子编辑器中，从context回到根节点后，再次点击back应该退出子编辑器
                // 这里不直接退出，让用户再次点击back按钮来退出
            }
        }

        console.log('DslEditor: After going back, new AST root:', this.astRoot);
        console.log('DslEditor: Is sub-editor:', this.isSubEditor);

        this.updateExpressionFromAST();
        this.renderEditor();
    }

    /**
     * 执行AST的撤回操作
     */
    performASTGoBack() {
        if (!this.astRoot) {
            return { success: false, newRoot: null };
        }

        // 如果是单个值节点
        if (this.astRoot.type === 'value') {
            // 如果是封装的表达式，解封装回到内部AST
            if (this.astRoot.metadata && this.astRoot.metadata.type === "encapsulated" && this.astRoot.metadata.innerAST) {
                console.log("DslEditor: Unencapsulating expression:", this.astRoot.metadata.innerAST);
                this.isExpressionCompleted = false; // 重置完成状态
                return { success: true, newRoot: this.astRoot.metadata.innerAST };
            }
            // 普通值节点，直接清空
            return { success: true, newRoot: null };
        }

        // 如果是操作符节点
        if (this.astRoot.type === 'operator') {
            // 如果右操作数存在，移除右操作数
            if (this.astRoot.rightOperand) {
                this.astRoot.setRightOperand(null);
                return { success: true, newRoot: this.astRoot };
            }

            // 如果只有左操作数，返回左操作数
            if (this.astRoot.leftOperand) {
                const leftOperand = this.astRoot.leftOperand;
                leftOperand.parent = null; // 清除父节点引用
                return { success: true, newRoot: leftOperand };
            }

            // 如果没有操作数，清空
            return { success: true, newRoot: null };
        }

        return { success: false, newRoot: this.astRoot };
    }

    /**
     * 显示添加节点菜单
     */
    showAddNodeMenu(button) {
        // 移除现有菜单
        const existingMenu = document.querySelector(".dsl-add-menu");
        if (existingMenu) {
            existingMenu.remove();
        }

        const menu = document.createElement("div");
        menu.className = "dsl-options-dropdown";

        const removeMenu = () => {
            // 清理所有子菜单
            const groupItems = menu.querySelectorAll('.dsl-option-group');
            groupItems.forEach(item => {
                if (item._submenuCleanup) {
                    item._submenuCleanup();
                }
            });

            document.querySelectorAll('.dsl-submenu').forEach(submenu => {
                submenu.remove();
            });

            menu.remove();
            document.removeEventListener("click", closeMenu);
        };

        const closeMenu = (e) => {
            if (!menu.contains(e.target) && e.target !== button) {
                removeMenu();
            }
        };

        // 根据当前状态确定可用选项
        const options = this.getCurrentEditingOptions() || [];

        console.log(
            "DslEditor: Showing options menu with",
            options.length,
            "options:",
            options
        );

        if (options.length === 0) {
            const noOptions = document.createElement("div");
            noOptions.className = "dsl-option";
            noOptions.innerHTML =
                '<span class="dsl-option-label">暂无可用选项</span>';
            menu.appendChild(noOptions);
        } else {
            const createDirectOptionItem = (option) => {
                const item = document.createElement("div");
                item.className = "dsl-option";
                item.style.cssText = `
                    padding: 12px 16px;
                    cursor: pointer;
                    border-bottom: 1px solid #e9ecef;
                    background: #ffffff;
                    transition: background-color 0.15s ease;
                `;

                const label = document.createElement("span");
                label.className = "dsl-option-label";
                label.textContent = option.label;
                label.style.cssText = `
                    font-weight: 600;
                    color: #1a1a1a;
                    font-size: 14px;
                    display: block;
                `;
                item.appendChild(label);

                if (option.description) {
                    const description = document.createElement("div");
                    description.className = "dsl-option-description";
                    description.textContent = option.description;
                    description.style.cssText = `
                        font-size: 12px;
                        color: #4a5568;
                        margin-top: 3px;
                        line-height: 1.3;
                    `;
                    item.appendChild(description);
                }

                item.addEventListener("mouseenter", () => {
                    item.style.background = "#2196f3";
                    label.style.color = "#ffffff";
                    if (option.description) {
                        const desc = item.querySelector('.dsl-option-description');
                        if (desc) desc.style.color = "#e3f2fd";
                    }
                });

                item.addEventListener("mouseleave", () => {
                    item.style.background = "#ffffff";
                    label.style.color = "#1a1a1a";
                    if (option.description) {
                        const desc = item.querySelector('.dsl-option-description');
                        if (desc) desc.style.color = "#4a5568";
                    }
                });

                item.addEventListener("click", () => {
                    console.log("DslEditor: Option selected:", option);
                    this.addNode(option);
                    removeMenu();
                });

                return item;
            };

            // 按类型分组选项
            const groupedOptions = this.groupOptionsByType(options);
            console.log('DslEditor: Grouped options:', groupedOptions);

            // 渲染分级菜单
            Object.entries(groupedOptions).forEach(([groupName, groupOptions]) => {
                console.log(`DslEditor: Rendering group "${groupName}" with ${groupOptions.length} options:`, groupOptions);
                const shouldRenderDirect = groupOptions.length === 1 && groupOptions[0].type === "listPipeline";
                if (shouldRenderDirect) {
                    menu.appendChild(createDirectOptionItem(groupOptions[0]));
                    return;
                }
                if (groupOptions.length > 0) {
                    const groupItem = document.createElement("div");
                    groupItem.className = "dsl-option-group";
                    groupItem.style.cssText = `
                        position: relative;
                        padding: 12px 16px;
                        cursor: pointer;
                        border-bottom: 1px solid #e9ecef;
                        background: #f8f9fa;
                        font-weight: 600;
                        color: #495057;
                        display: flex;
                        justify-content: space-between;
                        align-items: center;
                        transition: background-color 0.15s ease;
                        font-size: 14px;
                    `;

                    const groupLabel = document.createElement("span");
                    groupLabel.textContent = groupName;
                    groupItem.appendChild(groupLabel);

                    const arrow = document.createElement("span");
                    arrow.textContent = "▶";
                    arrow.style.cssText = "font-size: 12px; color: #495057; margin-left: 8px;";
                    groupItem.appendChild(arrow);

                    // 主菜单项悬停效果
                    groupItem.addEventListener("mouseenter", () => {
                        groupItem.style.background = "#e9ecef";
                    });

                    groupItem.addEventListener("mouseleave", () => {
                        groupItem.style.background = "#f8f9fa";
                    });

                    // 鼠标悬停事件 - 显示独立的子菜单
                    let hoverTimeout;
                    let currentSubmenu = null;

                    const showSubmenu = () => {
                        clearTimeout(hoverTimeout);

                        // 移除现有的子菜单
                        if (currentSubmenu) {
                            currentSubmenu.remove();
                        }

                        // 创建新的独立子菜单
                        currentSubmenu = this.createSubmenu(groupOptions, groupItem, menu);
                        document.body.appendChild(currentSubmenu);

                        // 为子菜单添加鼠标事件
                        currentSubmenu.addEventListener("mouseenter", () => {
                            clearTimeout(hoverTimeout);
                        });

                        currentSubmenu.addEventListener("mouseleave", () => {
                            hoverTimeout = setTimeout(() => {
                                if (currentSubmenu) {
                                    currentSubmenu.remove();
                                    currentSubmenu = null;
                                }
                            }, 300); // 增加延迟时间
                        });
                    };

                    const hideSubmenu = () => {
                        hoverTimeout = setTimeout(() => {
                            if (currentSubmenu) {
                                currentSubmenu.remove();
                                currentSubmenu = null;
                            }
                        }, 300); // 增加延迟时间
                    };

                    groupItem.addEventListener("mouseenter", showSubmenu);
                    groupItem.addEventListener("mouseleave", hideSubmenu);

                    // 存储引用以便清理
                    groupItem._submenuCleanup = () => {
                        clearTimeout(hoverTimeout);
                        if (currentSubmenu) {
                            currentSubmenu.remove();
                            currentSubmenu = null;
                        }
                    };
                    menu.appendChild(groupItem);
                }
            });
        }

        // 智能定位菜单并添加滚动支持
        const rect = button.getBoundingClientRect();
        const viewportHeight = window.innerHeight;
        const viewportWidth = window.innerWidth;

        // 计算最佳位置
        let menuTop = rect.bottom + 5;
        let menuLeft = rect.left;
        const menuWidth = 250; // 预估菜单宽度
        const maxMenuHeight = Math.min(400, viewportHeight - rect.bottom - 20);

        // 检查是否超出右边界
        if (menuLeft + menuWidth > viewportWidth) {
            menuLeft = viewportWidth - menuWidth - 10;
        }

        // 检查是否超出下边界
        if (menuTop + maxMenuHeight > viewportHeight) {
            menuTop = rect.top - maxMenuHeight - 5; // 显示在按钮上方
            if (menuTop < 10) {
                menuTop = 10; // 确保不超出顶部
            }
        }

        menu.style.cssText = `
            position: fixed;
            left: ${menuLeft}px;
            top: ${menuTop}px;
            z-index: 1000;
            max-height: ${maxMenuHeight}px;
            overflow-y: auto;
            overflow-x: hidden;
            background: #ffffff;
            border: 1px solid #bdc3c7;
            border-radius: 6px;
            box-shadow: 0 6px 16px rgba(0,0,0,0.2);
            min-width: 200px;
            max-width: 300px;
        `;

        // 添加自定义滚动条样式
        this.addScrollbarStyles(menu);

        document.body.appendChild(menu);

        setTimeout(() => document.addEventListener("click", closeMenu), 0);
    }

    renderFavoritesSection(menu, closeMenu) {
        const section = document.createElement("div");
        section.className = "dsl-favorites-panel";
        section.style.cssText = `
            padding: 10px 12px;
            border-bottom: 1px solid #e9ecef;
            background: #fafafa;
        `;

        const header = document.createElement("div");
        header.style.cssText = `
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 8px;
            margin-bottom: 8px;
        `;

        const title = document.createElement("div");
        title.textContent = "收藏";
        title.style.cssText = "font-weight: 700; color: #495057; font-size: 14px;";
        header.appendChild(title);

        const addWrapper = document.createElement("span");
        addWrapper.style.cssText = "display: inline-flex;";
        const addButton = document.createElement("button");
        addButton.type = "button";
        addButton.textContent = "收藏当前表达式";
        addButton.style.cssText = `
            border: 1px solid #ffc107;
            background: #fff3cd;
            color: #856404;
            font-size: 12px;
            padding: 4px 8px;
            border-radius: 12px;
            cursor: pointer;
        `;
        addButton.disabled = !this.userId;
        if (!this.userId) {
            addWrapper.title = "登录后可使用收藏";
            addButton.style.opacity = "0.6";
            addButton.style.cursor = "not-allowed";
            addButton.style.pointerEvents = "none";
        }
        addButton.addEventListener("click", async (event) => {
            event.stopPropagation();
            await this.createFavoriteFromCurrentExpression();
            await this.refreshFavorites(this.favoriteState.query);
            renderLists();
        });
        addWrapper.appendChild(addButton);
        header.appendChild(addWrapper);

        const searchInput = document.createElement("input");
        searchInput.type = "text";
        searchInput.placeholder = this.userId ? "搜索收藏" : "登录后可使用收藏";
        searchInput.value = this.favoriteState.query || "";
        searchInput.disabled = !this.userId;
        searchInput.style.cssText = `
            width: 100%;
            padding: 6px 8px;
            border: 1px solid #ced4da;
            border-radius: 6px;
            font-size: 12px;
            margin-bottom: 8px;
            box-sizing: border-box;
            background: ${this.userId ? "#ffffff" : "#f1f3f5"};
        `;

        const recentTitle = document.createElement("div");
        recentTitle.textContent = "最近使用";
        recentTitle.style.cssText = "font-weight: 600; color: #6c757d; font-size: 12px; margin: 8px 0 4px;";

        const recentList = document.createElement("div");
        const favoritesTitle = document.createElement("div");
        favoritesTitle.textContent = "收藏列表";
        favoritesTitle.style.cssText = "font-weight: 600; color: #6c757d; font-size: 12px; margin: 8px 0 4px;";

        const favoritesList = document.createElement("div");

        const createStateLine = (container, message) => {
            const line = document.createElement("div");
            line.textContent = message;
            line.style.cssText = "font-size: 12px; color: #868e96; padding: 4px 2px;";
            container.appendChild(line);
        };

        const createFavoriteItem = (favorite) => {
            const item = document.createElement("div");
            item.style.cssText = `
                padding: 6px 8px;
                border-radius: 6px;
                cursor: pointer;
                margin-bottom: 4px;
                border: 1px solid #e9ecef;
                background: #ffffff;
                transition: background-color 0.15s ease;
            `;

            const label = document.createElement("div");
            label.textContent = favorite.label || favorite.Label || "(未命名)";
            label.style.cssText = "font-weight: 600; font-size: 12px; color: #343a40;";
            item.appendChild(label);

            const desc = document.createElement("div");
            desc.textContent = favorite.expression || favorite.Expression || "";
            desc.style.cssText = "font-size: 11px; color: #6c757d; margin-top: 2px;";
            item.appendChild(desc);

            item.addEventListener("mouseenter", () => {
                item.style.background = "#f1f3f5";
            });
            item.addEventListener("mouseleave", () => {
                item.style.background = "#ffffff";
            });
            item.addEventListener("click", () => {
                const expr = favorite.expression || favorite.Expression || "";
                if (expr) {
                    this.insertFavoriteExpression(expr);
                    this.markFavoriteUsed(favorite.id || favorite.Id);
                    closeMenu();
                }
            });

            return item;
        };

        const renderList = (container, items, emptyText) => {
            container.innerHTML = "";
            if (!this.userId) {
                createStateLine(container, "登录后可使用收藏功能。");
                return;
            }
            if (this.favoriteState.loading) {
                createStateLine(container, "收藏加载中...");
                return;
            }
            if (this.favoriteState.error) {
                createStateLine(container, this.favoriteState.error);
                return;
            }
            if (!items || items.length === 0) {
                createStateLine(container, emptyText);
                return;
            }
            items.forEach(item => container.appendChild(createFavoriteItem(item)));
        };

        const renderLists = () => {
            const hasQuery = !!this.favoriteState.query;
            recentTitle.style.display = hasQuery ? "none" : "block";
            recentList.style.display = hasQuery ? "none" : "block";
            renderList(recentList, this.favoriteState.recentItems, "暂无最近使用。");
            renderList(favoritesList, this.favoriteState.items, "暂无收藏。");
        };

        searchInput.addEventListener("input", () => {
            const value = searchInput.value.trim();
            clearTimeout(this.favoriteSearchTimer);
            this.favoriteSearchTimer = setTimeout(async () => {
                await this.refreshFavorites(value);
                renderLists();
            }, 300);
        });

        section.appendChild(header);
        section.appendChild(searchInput);
        section.appendChild(recentTitle);
        section.appendChild(recentList);
        section.appendChild(favoritesTitle);
        section.appendChild(favoritesList);

        menu.appendChild(section);

        if (this.userId) {
            this.refreshFavorites(this.favoriteState.query).then(renderLists);
        } else {
            renderLists();
        }
    }

    /**
     * 创建独立的子菜单
     */
    createSubmenu(options, parentItem, mainMenu) {
        const submenu = document.createElement("div");
        submenu.className = "dsl-submenu";

        // 智能计算子菜单位置
        const parentRect = parentItem.getBoundingClientRect();
        const mainMenuRect = mainMenu.getBoundingClientRect();
        const viewportHeight = window.innerHeight;
        const viewportWidth = window.innerWidth;

        // 计算最佳位置
        let submenuLeft = mainMenuRect.right + 5;
        let submenuTop = parentRect.top;
        const submenuWidth = 250; // 预估子菜单宽度
        const maxSubmenuHeight = Math.min(350, viewportHeight - parentRect.top - 20);

        // 检查是否超出右边界，如果超出则显示在左侧
        if (submenuLeft + submenuWidth > viewportWidth) {
            submenuLeft = mainMenuRect.left - submenuWidth - 5;
            // 如果左侧也不够，则调整到视窗内
            if (submenuLeft < 10) {
                submenuLeft = 10;
            }
        }

        // 检查是否超出下边界
        if (submenuTop + maxSubmenuHeight > viewportHeight) {
            submenuTop = viewportHeight - maxSubmenuHeight - 10;
            if (submenuTop < 10) {
                submenuTop = 10;
            }
        }

        submenu.style.cssText = `
            position: fixed;
            left: ${submenuLeft}px;
            top: ${submenuTop}px;
            min-width: 200px;
            max-width: 300px;
            max-height: ${maxSubmenuHeight}px;
            overflow-y: auto;
            overflow-x: hidden;
            background: #ffffff;
            border: 1px solid #bdc3c7;
            border-radius: 6px;
            box-shadow: 0 6px 16px rgba(0,0,0,0.2);
            z-index: 1002;
            padding: 6px 0;
            font-size: 14px;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        `;

        // 添加子菜单选项
        options.forEach((option) => {
            const item = document.createElement("div");
            item.className = "dsl-option";
            item.style.cssText = `
                padding: 10px 16px;
                cursor: pointer;
                border-bottom: none;
                background: #ffffff;
                transition: background-color 0.15s ease;
                border-radius: 3px;
                margin: 2px 4px;
            `;

            const label = document.createElement("span");
            label.className = "dsl-option-label";
            label.textContent = option.label;
            label.style.cssText = `
                font-weight: 600;
                color: #1a1a1a;
                font-size: 14px;
                display: block;
            `;
            item.appendChild(label);

            if (option.description) {
                const description = document.createElement("div");
                description.className = "dsl-option-description";
                description.textContent = option.description;
                description.style.cssText = `
                    font-size: 12px;
                    color: #4a5568;
                    margin-top: 3px;
                    line-height: 1.3;
                `;
                item.appendChild(description);
            }

            // 悬停效果
            item.addEventListener("mouseenter", () => {
                item.style.background = "#2196f3";
                item.style.borderRadius = "4px";
                // 悬停时改变文字颜色为白色
                label.style.color = "#ffffff";
                if (option.description) {
                    const desc = item.querySelector('.dsl-option-description');
                    if (desc) desc.style.color = "#e3f2fd";
                }
            });

            item.addEventListener("mouseleave", () => {
                item.style.background = "#ffffff";
                // 恢复原始文字颜色
                label.style.color = "#1a1a1a";
                if (option.description) {
                    const desc = item.querySelector('.dsl-option-description');
                    if (desc) desc.style.color = "#4a5568";
                }
            });

            item.addEventListener("click", () => {
                console.log("DslEditor: Option selected:", option);
                this.addNode(option);
                // 关闭所有菜单
                mainMenu.remove();
                submenu.remove();
            });

            submenu.appendChild(item);
        });

        // 添加自定义滚动条样式
        this.addScrollbarStyles(submenu);

        return submenu;
    }

    /**
     * 添加自定义滚动条样式
     */
    addScrollbarStyles(element) {
        // 为元素添加自定义滚动条样式
        const style = document.createElement('style');
        const uniqueClass = `dsl-menu-${Date.now()}-${Math.random().toString(36).substring(2, 11)}`;
        element.classList.add(uniqueClass);

        style.textContent = `
            .${uniqueClass}::-webkit-scrollbar {
                width: 8px;
            }

            .${uniqueClass}::-webkit-scrollbar-track {
                background: #f1f1f1;
                border-radius: 4px;
            }

            .${uniqueClass}::-webkit-scrollbar-thumb {
                background: #c1c1c1;
                border-radius: 4px;
                transition: background 0.2s ease;
            }

            .${uniqueClass}::-webkit-scrollbar-thumb:hover {
                background: #a1a1a1;
            }

            /* Firefox滚动条样式 */
            .${uniqueClass} {
                scrollbar-width: thin;
                scrollbar-color: #c1c1c1 #f1f1f1;
            }
        `;

        document.head.appendChild(style);

        // 当菜单被移除时，也移除样式
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.removedNodes.forEach((node) => {
                    if (node === element) {
                        style.remove();
                        observer.disconnect();
                    }
                });
            });
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    /**
     * 按类型分组选项
     */
    groupOptionsByType(options) {
        const groups = {
            '数据源': [],
            '属性': [],
            '函数': [],
            '操作符': [],
            '筛选': []
        };

        options.forEach(option => {
            switch (option.type) {
                case 'context':
                case 'staticValue': // 静态值也是数据源
                    groups['数据源'].push(option);
                    break;
                case 'property':
                    groups['属性'].push(option);
                    break;
                case 'function':
                    groups['函数'].push(option);
                    break;
                case 'operator':
                    groups['操作符'].push(option);
                    break;
                case 'listQuery':
                case 'listSort':
                case 'listTake':
                case 'listPipeline':
                    groups['筛选'].push(option);
                    break;
                default:
                    groups['其他'] = groups['其他'] || [];
                    groups['其他'].push(option);
                    break;
            }
        });

        // 只返回非空的分组
        const nonEmptyGroups = {};
        Object.entries(groups).forEach(([key, value]) => {
            if (value.length > 0) {
                nonEmptyGroups[key] = value;
            }
        });

        return nonEmptyGroups;
    }

    /**
     * 检查函数是否与特定类型兼容 (V2.0 精确匹配版)
     */
    isFunctionCompatibleWithType(func, typeName) {
        if (!func.parameters || func.parameters.length === 0) {
            return false;
        }

        const firstParam = func.parameters[0];

        // 1. 精确类型匹配
        if (firstParam.typeName === typeName) {
            return true;
        }

        // 2. 通用Object类型匹配
        if (firstParam.typeName === "Object") {
            return true;
        }

        // 3. 基础类型的层次匹配
        if (this.isBaseType(typeName) && this.isBaseType(firstParam.typeName)) {
            return this.areBaseTypesCompatible(typeName, firstParam.typeName);
        }

        // 4. 集合类型的匹配
        if (this.isCollectionType(typeName) && this.isCollectionType(firstParam.typeName)) {
            const sourceInner = this.getCollectionInnerType(typeName);
            const targetInner = this.getCollectionInnerType(firstParam.typeName);
            return sourceInner === targetInner || targetInner === "Object";
        }

        // 5. 继承关系匹配 (简化版)
        return this.isTypeInheritanceCompatible(typeName, firstParam.typeName);
    }


    /**
     * 添加节点 (AST重构版)
     */
    addNode(option) {
        console.log("DslEditor: Adding node (AST):", option);
    
        this.selectedNode = null;
    
        if (option.type === "complete") {
            this.handleCompleteExpression();
            return;
        }
    
        // 统一逻辑入口
        if (!this.astRoot || (this.astRoot.type === 'value' && this.astRoot.metadata?.type === 'empty')) {
            this.createRootNode(option);
        } else {
            this.extendAST(option);
        }
    
        // 关键修复：统一在 addNode 的末尾触发状态更新和重新渲染
        this.triggerStateUpdate();
    }

    /**
     * 处理静态值输入
     */
    handleStaticValueInput(value) {
        console.log("DslEditor: Handling static value input:", value);

        // 查找当前等待输入的静态值节点
        let targetNode = null;

        // 首先检查当前工作区的astRoot
        if (this.astRoot && this.astRoot.metadata && this.astRoot.metadata.type === 'staticValue' && this.astRoot.metadata.isAwaitingInput) {
            targetNode = this.astRoot;
        }

        // 如果没找到，检查编辑路径中的右操作数
        if (!targetNode && this.editingPath && this.editingPath.length > 0) {
            const top = this.editingPath[this.editingPath.length - 1];
            if (top.slot === 'right' && top.parentNode && top.parentNode.rightOperand) {
                const rightNode = top.parentNode.rightOperand;
                if (rightNode.metadata && rightNode.metadata.type === 'staticValue' && rightNode.metadata.isAwaitingInput) {
                    targetNode = rightNode;
                }
            }
        }

        if (targetNode) {
            // 更新节点值
            targetNode.value = value;
            targetNode.metadata.staticValue = value;
            targetNode.metadata.isAwaitingInput = false;
            targetNode.updateCompleteness();

            // 如果有父节点，更新父节点的完整性
            if (targetNode.parent) {
                targetNode.parent.updateCompleteness();
            }

            // 更新当前工作区指向
            this.workspaceAST = targetNode;
            this.astRoot = targetNode;

            // 更新表达式
            this.updateExpressionFromAST();

            console.log("DslEditor: Static value updated:", targetNode);
        } else {
            console.warn("DslEditor: No awaiting static value node found");
        }
    }

    /**
     * 处理"完成表达式"选项
     */
    handleCompleteExpression() {

        if (!this.astRoot) {
            this.showExpressionError("没有表达式可以完成");
            return;
        }

        // 验证表达式完整性
        const validationResult = this.validateExpressionForCompletion();
        if (!validationResult.isValid) {
            this.showExpressionError(validationResult.error);
            return;
        }

        // 将当前表达式封装为一个完成的子表达式
        this.encapsulateCurrentExpression();

        // 重新渲染编辑器，这会触发选项重新计算和视觉提示
        this.renderEditor();

        console.log("DslEditor: Expression encapsulated as completed sub-expression");
    }


    /**
     * 保存工作区（新架构）：将工作区内容写回父槽位或设置为主表达式
     */
    saveWorkspace() {
        console.log("DslEditor: Saving workspace content.");

        if (!this.workspaceAST) {
            console.warn("DslEditor: saveWorkspace called with empty workspaceAST.");
            return;
        }

        // 统一保存逻辑：只存在槽位编辑模式
        if (this.editingPath && this.editingPath.length > 0) {
            this.saveCurrentLayer();
        } else {
            console.warn("DslEditor: saveWorkspace called with no editing path.");
        }
    }

    /**
     * 保存当前层：封装 workspaceAST -> 写回父槽位 -> 出栈 -> 切换工作区到上一级
     */
    saveCurrentLayer() {
        const top = this.peekEditingPath();
        if (!top) {
            console.warn('DslEditor: saveCurrentLayer called with empty editingPath');
            return;
        }
        if (!this.workspaceAST || findIncompleteNode(this.workspaceAST)) {
            this.showExpressionError("工作区表达式不完整，无法保存。");
            console.warn('DslEditor: saveCurrentLayer failed: workspaceAST is null or incomplete');
            return;
        }

        // 关键修复：在调用 ascend() 之前捕获要保存的 AST，因为 ascend() 会将 this.workspaceAST 设置为 null
        const astToSave = this.workspaceAST;

        const { parentNode, slot, index } = top;

        if (!parentNode) {
            console.error('DslEditor: saveCurrentLayer failed: parentNode from editingPath is null.');
            this.showExpressionError("保存失败：编辑上下文丢失。");
            return;
        }

        console.log('DslEditor: saveCurrentLayer - Found target parent node from editing path:', parentNode);

        // 保存门槛：空表达式不可保存
        if (astToSave?.type === 'value' && astToSave?.metadata?.type === 'empty') {
            this.showExpressionError("表达式不能为空。");
            return;
        }

        // 保存门槛：AST 语义校验
        const astValidation = validateAST(astToSave);
        if (!astValidation?.isValid) {
            const msg = (astValidation?.errors || []).map(e => String(e)).join('\n');
            this.showExpressionError(msg || "表达式不合法，无法保存。");
            return;
        }

        // 预期类型由胶囊提示展示，最终由完整表达式校验把关

        // 关键修复：重构保存逻辑以正确处理虚拟根节点
        // 遵循规则：保存时总是对工作区内容进行封装
        const valueToWrite = this.createEncapsulatedFrom(astToSave);

        let ok = false;
        if (parentNode.type === 'functionCall' && slot === 'arg') {
            const argIndex = typeof index === 'number' ? index : 0;
            if (typeof parentNode.setArg === 'function') {
                parentNode.setArg(argIndex, valueToWrite);
                ok = true;
            }
        } else {
            ok = this.writeBackToParentSlot(parentNode, slot, valueToWrite);
        }

        if (!ok) {
            console.warn('DslEditor: writeBackToParentSlot failed unexpectedly.');
            return;
        }

        this.popEditingPath();

        // 关键修复：保存完成后，应该上升到刚刚被修改的父节点进行预览
        this.ascend(parentNode);
    }

    /**
     * 上升：在子表达式保存后，将父表达式置于工作区
     */
    ascend(parentNode) {
        console.log("DslEditor: Ascending to parent node:", parentNode);
        
        // 决定性修复：ascend 的职责是加载指定节点到工作区进行“预览”
        if (parentNode.type === 'virtualRoot') {
            // 如果上升到虚拟根，说明编辑会话结束，应预览最终的主表达式
            console.log("DslEditor: Ascended to virtual root. Previewing main expression.");
            this.workspaceAST = this.cloneAST(this.virtualRoot.main);
            this.astRoot = this.workspaceAST;
            this.clearEditingPath(); // 清除路径，表示当前不在任何槽位的编辑模式下
        } else {
            // 如果上升到普通节点，则在工作区预览该节点
            this.workspaceAST = this.cloneAST(parentNode);
            this.astRoot = this.workspaceAST;
        }
    
        this.updateExpressionFromAST();
        this.renderEditor();
    }

    // completeToTop 已移除 - 封装完最上层表达式即视为完成

    /**
     * 工具：从任意 AST 创建封装 ValueNode
     */
    createEncapsulatedFrom(ast) {
        const originalExpression = ast && ast.toExpression ? ast.toExpression() : '';
        const encapsulatedExpression = `(${originalExpression})`;
        return createValueNode(
            encapsulatedExpression,
            ast?.returnType || 'Object',
            {
                type: 'encapsulated',
                innerAST: this.cloneAST(ast),
                isCompleted: ast && ast.metadata?.type !== 'empty' ? validateAST(ast).isValid : false,
                displayValue: encapsulatedExpression,
                originalExpression: originalExpression
            }
        );
    }





    /**
     * 获取当前正在编辑的表达式
     */
    getCurrentEditingExpression() {
        if (!this.astRoot) return null;

        // 如果根节点是不完整的，那么它就是当前编辑的表达式
        const incompleteNode = findIncompleteNode(this.astRoot);
        if (incompleteNode) {
            // 对于操作符节点，找到需要编辑的操作数
            if (incompleteNode.type === 'operator') {
                if (!incompleteNode.rightOperand) {
                    // 右操作数缺失，当前没有编辑的表达式
                    return null;
                }
                // 右操作数存在但可能不完整，检查右操作数
                return incompleteNode.rightOperand;
            }
            return incompleteNode;
        }

        // 如果没有不完整节点，找到最右侧的叶子节点作为当前编辑位置
        const rightmostNode = findRightmostLeafNode(this.astRoot);

        // 如果最右侧节点已经封装，则整个表达式是当前编辑对象
        if (rightmostNode && rightmostNode.metadata && rightmostNode.metadata.type === "encapsulated") {
            return this.astRoot;
        }

        return rightmostNode;
    }

    /**
     * 封装指定的表达式节点
     */
    encapsulateExpressionNode(node) {
        if (!node) return;

        const originalExpression = this.getNodeExpression(node);
        const encapsulatedExpression = `(${originalExpression})`;

        // 创建封装节点
        const encapsulatedNode = createValueNode(
            encapsulatedExpression,
            node.returnType,
            {
                type: "encapsulated",
                innerAST: node,
                isCompleted: true,
                displayValue: encapsulatedExpression,
                originalExpression: originalExpression
            }
        );

        // 替换原节点
        this.replaceNodeInAST(node, encapsulatedNode);
    }

    /**
     * 获取节点的表达式字符串
     */
    getNodeExpression(node) {
        if (!node) return "";
        return node.toExpression ? node.toExpression() : node.value || "";
    }

    /**
     * 在AST中替换节点
     */
    replaceNodeInAST(oldNode, newNode, rootNode = null) {
        const targetRoot = rootNode || this.astRoot;

        if (targetRoot === oldNode) {
            if (rootNode) {
                // 如果指定了根节点，需要更新引用
                this.fullExpressionAST = newNode;
            } else {
                this.astRoot = newNode;
                this.workspaceAST = newNode; // 关键修复：保持 workspaceAST 与 astRoot 同步
            }
            return;
        }

        // 递归查找并替换节点
        this.replaceNodeRecursive(targetRoot, oldNode, newNode);
    }

    /**
     * 递归替换节点
     */
    replaceNodeRecursive(currentNode, oldNode, newNode) {
        if (!currentNode) return false;

        if (currentNode.type === 'operator') {
            if (currentNode.leftOperand === oldNode) {
                currentNode.setLeftOperand(newNode);
                return true;
            }
            if (currentNode.rightOperand === oldNode) {
                currentNode.setRightOperand(newNode);
                return true;
            }

            // 递归检查子节点
            if (this.replaceNodeRecursive(currentNode.leftOperand, oldNode, newNode)) {
                return true;
            }
            if (this.replaceNodeRecursive(currentNode.rightOperand, oldNode, newNode)) {
                return true;
            }
        }

        return false;
    }

    /**
     * 验证表达式是否可以完成
     */
    validateExpressionForCompletion() {
        // 检查是否有不完整的节点
        const incompleteNode = findIncompleteNode(this.astRoot);
        if (incompleteNode) {
            return {
                isValid: false,
                error: "表达式不完整，请先完成所有必需的部分"
            };
        }

        // 验证AST结构
        const astValidation = validateAST(this.astRoot);
        if (!astValidation.isValid) {
            return {
                isValid: false,
                error: `表达式验证失败：${astValidation.errors.join(', ')}`
            };
        }

        return { isValid: true };
    }



    /**
     * 显示表达式错误信息
     */
    showExpressionError(message) {
        // 移除现有的错误提示
        const existingError = this.container.querySelector('.dsl-expression-error');
        if (existingError) {
            existingError.remove();
        }

        // 创建错误提示
        const errorDiv = document.createElement('div');
        errorDiv.className = 'dsl-expression-error';
        errorDiv.style.cssText = `
            background: #ffebee;
            border: 1px solid #f44336;
            border-radius: 4px;
            padding: 8px 12px;
            margin: 8px 0;
            color: #c62828;
            font-size: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
        `;

        const errorIcon = document.createElement('span');
        errorIcon.textContent = '⚠️';
        errorDiv.appendChild(errorIcon);

        const errorText = document.createElement('span');
        errorText.textContent = message;
        errorDiv.appendChild(errorText);

        // 添加关闭按钮
        const closeBtn = document.createElement('button');
        closeBtn.textContent = '×';
        closeBtn.style.cssText = `
            background: none;
            border: none;
            color: #c62828;
            font-size: 16px;
            cursor: pointer;
            margin-left: auto;
        `;
        closeBtn.addEventListener('click', () => errorDiv.remove());
        errorDiv.appendChild(closeBtn);

        // 插入到编辑器容器中
        const editorContainer = this.container.querySelector('.dsl-editor');
        if (editorContainer) {
            editorContainer.insertBefore(errorDiv, editorContainer.firstChild);
        }

        // 3秒后自动消失
        setTimeout(() => {
            if (errorDiv.parentNode) {
                errorDiv.remove();
            }
        }, 3000);
    }

    /**
     * 标记AST节点为已完成状态
     */
    markASTAsCompleted(node) {
        if (node) {
            node.isCompletedExpression = true;
            if (node.leftOperand) {
                this.markASTAsCompleted(node.leftOperand);
            }
            if (node.rightOperand) {
                this.markASTAsCompleted(node.rightOperand);
            }
        }
    }

    /**
     * 创建根节点
     */
    createRootNode(option) {
        if (option.type === "context") {
            this.astRoot = createValueNode(option.value, option.returnType, {
                type: "context",
                name: option.value,
                displayName: option.label
            });
        } else if (option.type === "staticValue") {
            // 创建静态值节点，等待用户输入
            this.astRoot = createValueNode("[输入值]", "Object", {
                type: "staticValue",
                isAwaitingInput: true,
                staticValue: ""
            });
            this.currentNode = this.astRoot;
        } else if (option.type === "function") {
            const signature = option.meta || {};
            const parameters = signature.parameters || signature.Parameters || [];
            this.astRoot = createFunctionCallNode(option.value, option.returnType || signature.returnType || signature.returnTypeName || 'Object', parameters);
        } else if (option.type === "eventField") {
            this.astRoot = createValueNode(option.value, option.returnType, {
                type: "eventField",
                displayValue: option.meta?.displayValue || option.label,
                eventType: option.meta?.eventType,
                propertyName: option.meta?.propertyName,
                scope: option.meta?.scope
            });
        }

        // 关键修复：同步workspaceAST
        this.workspaceAST = this.astRoot;
        console.log("DslEditor: Created root node, synced workspaceAST:", {
            astRoot: this.astRoot?.toExpression?.() || null,
            workspaceAST: this.workspaceAST?.toExpression?.() || null
        });

        // 触发状态更新
        this.triggerStateUpdate();
    }

    /**
     * 扩展现有AST
     */
    extendAST(option) {
        // 新逻辑：优先处理基于选区的操作
        // 统一逻辑：所有操作符的添加都基于当前工作区内容

        // 关键修复：如果当前不在编辑模式（没有路径），但工作区有内容，则自动进入扩展主表达式模式
        if (this.editingPath.length === 0 && this.workspaceAST && validateAST(this.workspaceAST).isValid) {
            console.log("DslEditor: Auto-expanding main expression before extending.");
            this.expandMainExpression();
        }

        const incompleteNode = findIncompleteNode(this.astRoot);

        // 关键修复：如果当前不在编辑模式（没有路径），但工作区有内容，则自动进入扩展主表达式模式
        if (this.editingPath.length === 0 && this.workspaceAST && validateAST(this.workspaceAST).isValid) {
            console.log("DslEditor: Auto-expanding main expression before extending.");
            this.expandMainExpression();
            return; // 关键：准备好扩展上下文后，等待用户下一步操作
        }

        if (incompleteNode && incompleteNode.type === 'operator') {
            // 为操作符添加右操作数
            this.addRightOperandToOperator(incompleteNode, option);
        } else if (incompleteNode && incompleteNode.type === 'functionCall') {
            this.addArgToFunctionCall(incompleteNode, option);
        } else if (option.type === "operator") {
            // 创建新的操作符节点 (基于工作区)
            this.createOperatorFromCurrent(option);
        } else if (option.type === "property") {
            // 扩展属性访问
            this.extendPropertyAccess(option);
        } else if (option.type === "listPipeline") {
            this.openPipelineFromCurrent();
        } else if (option.type === "listQuery") {
            // 创建查询节点
            this.createQueryNodeFromCurrent(option);
        } else if (option.type === "listSort") {
            this.createSortNodeFromCurrent(option);
        } else if (option.type === "listTake") {
            this.createTakeNodeFromCurrent(option);
        } else if (option.type === "function") {
            this.createFunctionCallFromCurrent(option);
        }
    }

    createFunctionCallFromCurrent(option) {
        if (!this.workspaceAST) return;

        const signature = option.meta || {};
        const parameters = signature.parameters || signature.Parameters || [];
        const returnType = option.returnType || signature.returnType || signature.returnTypeName || 'Object';

        const functionNode = createFunctionCallNode(option.value, returnType, parameters);

        // Use current workspace as first argument when the function expects at least one parameter.
        if (parameters.length > 0) {
            functionNode.setArg(0, this.cloneAST(this.workspaceAST));
        }

        this.workspaceAST = functionNode;
        this.astRoot = functionNode;

        // If there are more required arguments, guide the user into editing the next slot.
        const nextIndex = parameters.findIndex((p, i) => !p?.isOptional && !functionNode.args[i]);
        if (nextIndex >= 0) {
            this.handleEnterEditModeForSlot(functionNode, 'arg', nextIndex);
        }
    }

    addArgToFunctionCall(functionNode, option) {
        const params = functionNode.parameters || [];
        const args = functionNode.args || [];
        const argIndex = params.findIndex((p, i) => !p?.isOptional && !args[i]);
        if (argIndex < 0) {
            this.showExpressionError('函数参数已齐全或不可编辑');
            return;
        }

        const makeNodeFromOption = (opt) => {
            if (opt.type === 'context') {
                return createValueNode(opt.value, opt.returnType, { type: 'context', name: opt.value, displayName: opt.label });
            }
            if (opt.type === 'property') {
                return createValueNode(opt.value, opt.returnType, {
                    type: 'property',
                    displayName: opt.label,
                    fullPath: opt.value
                });
            }
            if (opt.type === 'staticValue') {
                return createValueNode("[输入值]", "Object", { type: "staticValue", isAwaitingInput: true, staticValue: "" });
            }
            if (opt.type === 'function') {
                const signature = opt.meta || {};
                const parameters = signature.parameters || signature.Parameters || [];
                return createFunctionCallNode(opt.value, opt.returnType || signature.returnType || signature.returnTypeName || 'Object', parameters);
            }
            if (opt.type === 'eventField') {
                return createValueNode(opt.value, opt.returnType, {
                    type: "eventField",
                    displayValue: opt.meta?.displayValue || opt.label,
                    eventType: opt.meta?.eventType,
                    propertyName: opt.meta?.propertyName,
                    scope: opt.meta?.scope
                });
            }
            return null;
        };

        const argNode = makeNodeFromOption(option);
        if (!argNode) return;

        functionNode.setArg(argIndex, argNode);
        this.astRoot = this.astRoot; // no-op; keep structure
        this.workspaceAST = this.astRoot;
    }

    /**
     * 从工作区添加右操作数（新设计）
     */
    addRightOperandFromWorkspace(option) {
        console.log("DslEditor: Adding right operand from workspace:", option);

        // 在工作区创建右操作数节点
        if (option.type === "context") {
            this.astRoot = createValueNode(option.value, option.returnType, {
                type: "context",
                name: option.value,
                displayName: option.label
            });
        } else if (option.type === "staticValue") {
            this.astRoot = createValueNode("[输入值]", "Object", {
                type: "staticValue",
                isAwaitingInput: true,
                staticValue: ""
            });
            this.currentNode = this.astRoot;
        } else if (option.type === "function") {
            const signature = option.meta || {};
            const parameters = signature.parameters || signature.Parameters || [];
            this.astRoot = createFunctionCallNode(option.value, option.returnType || signature.returnType || signature.returnTypeName || 'Object', parameters);
        } else if (option.type === "eventField") {
            this.astRoot = createValueNode(option.value, option.returnType, {
                type: "eventField",
                displayValue: option.meta?.displayValue || option.label,
                eventType: option.meta?.eventType,
                propertyName: option.meta?.propertyName,
                scope: option.meta?.scope
            });
        }

        this.workspaceAST = this.astRoot;
        console.log("DslEditor: Right operand created in workspace, awaiting completion");
    }

    /**
     * 为操作符添加右操作数
     */
    addRightOperandToOperator(operatorNode, option) {
        let rightOperand;

        if (option.type === "context") {
            rightOperand = createValueNode(option.value, option.returnType, {
                type: "context",
                name: option.value,
                displayName: option.label
            });
        } else if (option.type === "staticValue") {
            rightOperand = createValueNode("[输入值]", "Object", {
                type: "staticValue",
                isAwaitingInput: true,
                staticValue: ""
            });
            this.currentNode = rightOperand;
        } else if (option.type === "function") {
            rightOperand = createValueNode(`${option.value}()`, option.returnType, {
                type: "function",
                functionName: option.value,
                arguments: []
            });
        }

        if (rightOperand) {
            operatorNode.setRightOperand(rightOperand);

            // 简化：操作符作为普通AST节点在工作区正常处理
            // 工作区继续指向整个操作符节点，而不是切换到右操作数
            this.workspaceAST = operatorNode;
            this.astRoot = operatorNode;

            console.log('DslEditor: addRightOperandToOperator -> right operand set, operator remains in workspace');
        }
    }

    /**
     * 从当前工作区创建一个操作符节点（新架构：交互驱动封装）
     * @param {string} operator - 操作符，例如 ">", "=="
     */
    createOperatorFromCurrent(option) {
        const operator = option.operator || option.value;
        if (!this.workspaceAST) {
            console.error("DslEditor: Cannot create operator, workspace is empty.");
            return;
        }
    
        // 步骤 A: 自动保存当前工作区内容
        // 获取当前编辑上下文，但不弹出
        const top = this.peekEditingPath();
        if (!top || !top.parentNode) {
            console.error("DslEditor: Cannot create operator, no editing context found for auto-save.");
            this.showExpressionError("无法添加操作符：编辑上下文丢失。");
            return;
        }
    
        // 直接将当前工作区内容（例如，手动封装后的节点）写回主AST
        const leftOperandNode = this.cloneAST(this.workspaceAST);
        this.writeBackToParentSlot(top.parentNode, top.slot, leftOperandNode);
        console.log("DslEditor: Step A - Auto-saved workspace to main AST.", leftOperandNode);
    
        // 步骤 B: 生成操作符和相应的占位符
        const rightOperandPlaceholder = createValueNode("", "Void", { type: 'empty' });
        const newOperatorNode = createOperatorNode(operator);
        newOperatorNode.setLeftOperand(leftOperandNode); // 使用刚刚保存的节点作为左操作数
        newOperatorNode.setRightOperand(rightOperandPlaceholder);
        console.log("DslEditor: Step B - Created new operator node.", newOperatorNode);
    
        // 步骤 C: 检测并创建外层封装
        const newEncapsulatedNode = this.createEncapsulatedFrom(newOperatorNode);
        console.log("DslEditor: Step C - Encapsulated the new operator node.", newEncapsulatedNode);
    
        // 步骤 D: 更新主表达式，并将工作区切换到新加载的空白表达式
        // 再次写入同一个槽位，用包含操作符的新封装节点覆盖掉旧的
        this.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);
        console.log("DslEditor: Step D - Wrote back the final encapsulated operator node to main AST.");
    
        // 从新的主AST节点中获取内部操作符节点的正确引用
        const operatorNodeInMainAST = newEncapsulatedNode.metadata.innerAST;
    
        // 进入新操作符右侧占位符的编辑模式
        this.handleEnterEditModeForSlot(operatorNodeInMainAST, 'right');
        console.log("DslEditor: Step D - Switched to edit mode for the new placeholder.");
    }

    /**
     * 从当前工作区创建一个查询节点
     */
    createQueryNodeFromCurrent(option) {
        if (!this.workspaceAST) return;

        const top = this.peekEditingPath();
        if (!top || !top.parentNode) {
            this.showExpressionError("无法添加筛选：编辑上下文丢失。");
            return;
        }

        // 步骤 A: 自动保存当前工作区内容
        const sourceNode = this.cloneAST(this.workspaceAST);
        this.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

        // 步骤 B: 创建查询节点和占位符
        const queryNode = createListQueryNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        queryNode.setFilterExpression(placeholder);

        // 步骤 C: 检测并创建外层封装
        const newEncapsulatedNode = this.createEncapsulatedFrom(queryNode);

        // 步骤 D: 更新主表达式，并将工作区切换到新加载的空白表达式
        this.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);
        
        const queryNodeInMainAST = newEncapsulatedNode.metadata.innerAST;

        // 进入新操作符右侧占位符的编辑模式
        this.handleEnterEditModeForSlot(queryNodeInMainAST, 'filterExpression');
    }

    createSortNodeFromCurrent(option) {
        if (!this.workspaceAST) return;
        
        const top = this.peekEditingPath();
        if (!top || !top.parentNode) {
            this.showExpressionError("无法添加排序：编辑上下文丢失。");
            return;
        }

        const sourceNode = this.cloneAST(this.workspaceAST);
        this.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

        const sortNode = createListSortNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        sortNode.setSortExpression(placeholder);

        const newEncapsulatedNode = this.createEncapsulatedFrom(sortNode);
        this.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);
        
        const sortNodeInMainAST = newEncapsulatedNode.metadata.innerAST;
        this.handleEnterEditModeForSlot(sortNodeInMainAST, 'sortExpression');
    }

    createTakeNodeFromCurrent(option) {
        if (!this.workspaceAST) return;

        const top = this.peekEditingPath();
        if (!top || !top.parentNode) {
            this.showExpressionError("无法添加获取：编辑上下文丢失。");
            return;
        }

        const sourceNode = this.cloneAST(this.workspaceAST);
        this.writeBackToParentSlot(top.parentNode, top.slot, sourceNode);

        const takeNode = createListTakeNode(sourceNode);
        const placeholder = createValueNode("", "Void", { type: 'empty' });
        takeNode.setCountExpression(placeholder);

        const newEncapsulatedNode = this.createEncapsulatedFrom(takeNode);
        this.writeBackToParentSlot(top.parentNode, top.slot, newEncapsulatedNode);

        const takeNodeInMainAST = newEncapsulatedNode.metadata.innerAST;
        this.handleEnterEditModeForSlot(takeNodeInMainAST, 'countExpression');
    }

    /**
     * 基于严格AST结构添加操作符
     */
    addOperatorToASTStructure(operator) {
        console.log("DslEditor: Adding operator to AST structure:", {
            operator: operator,
            currentWorkspaceAST: this.astRoot,
            mainExpressionAST: this.mainExpressionAST
        });

        // 分析当前AST结构
        const astAnalysis = this.analyzeCurrentASTStructure();
        console.log("DslEditor: Current AST structure analysis:", astAnalysis);

        // 基于AST结构确定操作符添加策略
        const strategy = this.determineOperatorAdditionStrategy(astAnalysis, operator);
        console.log("DslEditor: Operator addition strategy:", strategy);

        // 执行操作符添加策略
        return this.executeOperatorAdditionStrategy(astAnalysis, operator, strategy);
    }

    /**
     * 分析当前AST结构
     */
    analyzeCurrentASTStructure() {
        console.log("DslEditor: Analyzing AST structure:", {
            mainExpressionAST: this.virtualRoot.main,
            astRoot: this.astRoot
        });

        // 关键修复：正确识别当前的编辑状态
        const currentExpression = this.getCurrentCompleteExpression();
        console.log("DslEditor: Current complete expression:", currentExpression);

        const analysis = {
            hasMainExpression: !!this.virtualRoot.main,
            hasWorkspaceExpression: !!this.astRoot,
            currentCompleteExpression: currentExpression,
            currentExpressionType: currentExpression?.type || null,
            isEditingComplete: this.isCurrentExpressionComplete(),
            canAddOperator: this.canAddOperatorToCurrentExpression()
        };

        // 分析当前完整表达式的结构
        if (currentExpression) {
            if (currentExpression.type === 'operator') {
                analysis.currentOperator = currentExpression.operator;
                analysis.operatorComplete = !!(currentExpression.leftOperand && currentExpression.rightOperand);
            } else if (currentExpression.metadata?.type === "encapsulated") {
                analysis.isEncapsulated = true;
                analysis.innerAST = currentExpression.metadata.innerAST;
                if (analysis.innerAST?.type === 'operator') {
                    analysis.innerOperator = analysis.innerAST.operator;
                    analysis.innerOperatorComplete = !!(analysis.innerAST.leftOperand && analysis.innerAST.rightOperand);
                }
            }
        }

        console.log("DslEditor: AST structure analysis result:", analysis);
        return analysis;
    }

    /**
     * 获取当前完整的表达式
     */
    getCurrentCompleteExpression() {
        // 如果工作区有完整的表达式，返回工作区表达式
        if (this.astRoot && this.isASTComplete(this.astRoot)) {
            return this.astRoot;
        }

        // 如果主表达式存在，返回主表达式
        if (this.virtualRoot.main) {
            return this.virtualRoot.main;
        }

        return null;
    }

    /**
     * 检查当前表达式是否完整
     */
    isCurrentExpressionComplete() {
        const currentExpression = this.getCurrentCompleteExpression();
        return currentExpression ? this.isASTComplete(currentExpression) : false;
    }

    /**
     * 检查是否可以为当前表达式添加操作符
     */
    canAddOperatorToCurrentExpression() {
        const currentExpression = this.getCurrentCompleteExpression();

        if (!currentExpression) return false;

        // 如果是完整的表达式，可以添加操作符
        if (this.isASTComplete(currentExpression)) {
            return true;
        }

        // 如果是不完整的操作符，不能添加新操作符
        if (currentExpression.type === 'operator' && (!currentExpression.leftOperand || !currentExpression.rightOperand)) {
            return false;
        }

        return true;
    }

    /**
     * 检查AST是否完整
     */
    isASTComplete(ast) {
        if (!ast) return false;

        if (ast.type === 'value') {
            return true; // 值节点总是完整的
        }

        if (ast.type === 'operator') {
            return !!(ast.leftOperand && ast.rightOperand);
        }

        return false;
    }

    /**
     * 确定操作符添加策略
     */
    determineOperatorAdditionStrategy(astAnalysis, operator) {
        console.log("DslEditor: Determining strategy for operator:", operator, "with analysis:", astAnalysis);

        // 如果当前表达式不完整，不能添加操作符
        if (!astAnalysis.canAddOperator) {
            console.error("DslEditor: Cannot add operator to incomplete expression");
            return 'error_incomplete_expression';
        }

        // 如果没有当前表达式，创建新的主表达式
        if (!astAnalysis.currentCompleteExpression) {
            return 'create_new_main_expression';
        }

        // 关键修复：基于当前完整表达式的类型确定策略
        const currentExpression = astAnalysis.currentCompleteExpression;

        if (currentExpression.type === 'operator') {
            // 当前是操作符节点，检查优先级
            const needsNesting = this.needsNestedStructure(currentExpression.operator, operator);
            console.log("DslEditor: Current operator:", currentExpression.operator, "new operator:", operator, "needs nesting:", needsNesting);

            if (needsNesting) {
                return 'encapsulate_current_and_extend';
            } else {
                return 'extend_operator_chain';
            }
        } else if (currentExpression.metadata?.type === "encapsulated") {
            // 当前是封装表达式，可以直接扩展
            return 'extend_encapsulated_expression';
        } else {
            // 当前是值节点，创建新操作符
            return 'create_operator_from_value';
        }
    }

    /**
     * 执行操作符添加策略
     */
    executeOperatorAdditionStrategy(astAnalysis, operator, strategy) {
        console.log("DslEditor: Executing operator addition strategy:", strategy);

        switch (strategy) {
            case 'create_new_main_expression':
                return this.createNewMainExpression(operator);

            case 'extend_encapsulated_expression':
                return this.extendEncapsulatedExpression(astAnalysis.currentCompleteExpression, operator);

            case 'extend_operator_chain':
                return this.extendOperatorChain(astAnalysis.currentCompleteExpression, operator);

            case 'encapsulate_current_and_extend':
                return this.encapsulateCurrentAndExtend(astAnalysis.currentCompleteExpression, operator);

            case 'create_operator_from_value':
                return this.createOperatorFromValue(astAnalysis.currentCompleteExpression, operator);

            case 'error_incomplete_expression':
                return { success: false, error: 'Cannot add operator to incomplete expression' };

            default:
                return { success: false, error: `Unknown strategy: ${strategy}` };
        }
    }

    /**
     * 扩展封装表达式
     */
    extendEncapsulatedExpression(currentExpression, operator) {
        console.log("DslEditor: Extending encapsulated expression:", currentExpression.value, "with operator:", operator);

        const operatorNode = createOperatorNode(operator);
        operatorNode.setLeftOperand(currentExpression);

        // 关键：不进行额外封装，直接返回操作符节点
        return {
            success: true,
            newMainAST: operatorNode,
            operatorNode: operatorNode
        };
    }

    /**
     * 扩展操作符链
     */
    extendOperatorChain(currentExpression, operator) {
        console.log("DslEditor: Extending operator chain:", currentExpression.operator, "with operator:", operator);

        const operatorNode = createOperatorNode(operator);

        // 将当前操作符表达式作为左操作数
        operatorNode.setLeftOperand(currentExpression);

        return {
            success: true,
            newMainAST: operatorNode,
            operatorNode: operatorNode
        };
    }

    /**
     * 封装当前表达式并扩展
     */
    encapsulateCurrentAndExtend(currentExpression, operator) {
        console.log("DslEditor: Encapsulating current expression and extending with operator:", operator);

        const operatorNode = createOperatorNode(operator);

        // 封装当前表达式
        const encapsulatedExpression = this.createEncapsulatedFrom(currentExpression);
        operatorNode.setLeftOperand(encapsulatedExpression);

        // 封装新的操作符
        const newMainAST = this.createEncapsulatedFrom(operatorNode);

        return {
            success: true,
            newMainAST: newMainAST,
            operatorNode: operatorNode
        };
    }

    /**
     * 从值节点创建操作符
     */
    createOperatorFromValue(currentExpression, operator) {
        console.log("DslEditor: Creating operator from value:", currentExpression.value, "with operator:", operator);

        const operatorNode = createOperatorNode(operator);
        operatorNode.setLeftOperand(currentExpression);

        // 封装操作符
        const newMainAST = this.createEncapsulatedFrom(operatorNode);

        return {
            success: true,
            newMainAST: newMainAST,
            operatorNode: operatorNode
        };
    }

    /**
     * 创建新的主表达式
     */
    createNewMainExpression(operator) {
        console.log("DslEditor: Creating new main expression with operator:", operator);

        const operatorNode = createOperatorNode(operator);

        // 工作区表达式作为左操作数
        if (this.astRoot) {
            // 确保左操作数是封装的
            const leftOperand = this.astRoot.metadata?.type === "encapsulated"
                ? this.astRoot
                : this.createEncapsulatedFrom(this.astRoot);

            operatorNode.setLeftOperand(leftOperand);
        }

        // 封装操作符作为新的主表达式
        const newMainAST = this.createEncapsulatedFrom(operatorNode);

        return {
            success: true,
            newMainAST: newMainAST,
            operatorNode: operatorNode
        };
    }





    /**
     * 检查是否需要嵌套结构（基于操作符优先级）
     */
    needsNestedStructure(currentOperator, newOperator) {
        // 定义操作符优先级
        const operatorPriority = {
            '||': 1,
            '&&': 2,
            '==': 3,
            '!=': 3,
            '>': 4,
            '<': 4,
            '>=': 4,
            '<=': 4,
            '+': 5,
            '-': 5,
            '*': 6,
            '/': 6
        };

        const currentPriority = operatorPriority[currentOperator] || 0;
        const newPriority = operatorPriority[newOperator] || 0;

        // 如果新操作符优先级更低，需要嵌套结构
        // 例如：(a > b) && c 需要嵌套，因为 && 优先级低于 >
        return newPriority < currentPriority;
    }

    /**
     * 清除AST节点的已完成标记
     */

    /**
     * 扩展属性访问
     */
    extendPropertyAccess(option) {
        // 找到最右侧的叶子节点进行扩展
        const rightmostNode = findRightmostLeafNode(this.astRoot);

        if (rightmostNode && rightmostNode.type === 'value' && rightmostNode.metadata.type === 'context') {
            // 从上下文对象访问属性
            const newPath = `${rightmostNode.value}.${option.value}`;
            const newNode = createValueNode(newPath, option.returnType, {
                type: "property",
                contextObject: rightmostNode.value,
                property: option.value,
                fullPath: newPath,
                displayName: option.label
            });

            this.replaceNodeInAST(rightmostNode, newNode);
        } else if (rightmostNode && rightmostNode.type === 'value' && rightmostNode.metadata.type === 'property') {
            // 继续扩展属性链
            const newPath = `${rightmostNode.value}.${option.value}`;
            const newNode = createValueNode(newPath, option.returnType, {
                type: "property",
                fullPath: newPath,
                isNested: true,
                displayName: option.label
            });

            this.replaceNodeInAST(rightmostNode, newNode);

            // 关键修复：如果被替换的节点就是根节点，则必须更新根节点引用
            if (this.astRoot === rightmostNode) {
                this.astRoot = newNode;
                this.workspaceAST = newNode; // 确保 workspaceAST 也同步更新
            }
        }
    }



    /**
     * 维护AST节点关系和类型信息
     */
    maintainASTRelationships(node, parent = null) {
        if (!node) return;

        // 设置父节点关系
        node.parent = parent;

        // 计算并缓存类型匹配信息
        // 关键：为主表达式根节点传入外部预期类型
        let externalExpectedType = null;
        if (node === this.virtualRoot.main) {
            externalExpectedType = this.expectedType;
        }
        node._typeMatchInfo = this.calculateNodeTypeMatch(node, externalExpectedType);

        console.log("DslEditor: Maintaining AST relationships for node:", {
            type: node.type,
            value: node.value,
            hasParent: !!node.parent,
            parentOperator: node.parent?.operator,
            typeMatchInfo: node._typeMatchInfo
        });

        // 递归处理子节点
        if (node.type === 'operator') {
            if (node.leftOperand) {
                this.maintainASTRelationships(node.leftOperand, node);
            }
            if (node.rightOperand) {
                this.maintainASTRelationships(node.rightOperand, node);
            }
        }

        if (node.type === 'functionCall') {
            const args = node.args || [];
            args.forEach((arg) => {
                if (arg) {
                    this.maintainASTRelationships(arg, node);
                }
            });
        }

        if (node.type === 'listQuery') {
            if (node.source) {
                this.maintainASTRelationships(node.source, node);
            }
            if (node.filterExpression) {
                this.maintainASTRelationships(node.filterExpression, node);
            }
        }

        if (node.type === 'listSort') {
            if (node.source) {
                this.maintainASTRelationships(node.source, node);
            }
            if (node.sortExpression) {
                this.maintainASTRelationships(node.sortExpression, node);
            }
        }

        if (node.type === 'listTake') {
            if (node.source) {
                this.maintainASTRelationships(node.source, node);
            }
            if (node.countExpression) {
                this.maintainASTRelationships(node.countExpression, node);
            }
        }

        // 处理封装节点的内部AST
        if (node.metadata && node.metadata.innerAST) {
            // 关键修复：封装节点的内部AST应该保持其独立的父子关系结构
            // 内部AST的根节点没有父节点（因为它被封装了）
            this.maintainASTRelationships(node.metadata.innerAST, null);
        }
    }

    /**
     * 从AST更新表达式
     */
    updateExpressionFromAST() {
        console.log("DslEditor: updateExpressionFromAST called");

        // 关键修复：在更新表达式时维护AST关系
        if (this.astRoot) {
            console.log("DslEditor: Maintaining AST relationships for astRoot");
            try {
                this.maintainASTRelationships(this.astRoot);
            } catch (error) {
                console.error("DslEditor: Error maintaining AST relationships for astRoot:", error);
            }
        }
        if (this.virtualRoot.main) {
            console.log("DslEditor: Maintaining AST relationships for mainExpressionAST");
            try {
                this.maintainASTRelationships(this.virtualRoot.main);
            } catch (error) {
                console.error("DslEditor: Error maintaining AST relationships for mainExpressionAST:", error);
            }
        }

        this.expression = this.astRoot ? this.astRoot.toExpression() : "";
        console.log("DslEditor: Expression updated from AST:", this.expression);

        // 触发状态更新事件（用于实时监控）
        this.triggerStateUpdate();
        // 对外同步表达式（去重）
        this.emitExpressionChange(this.expression);
    }

    /**
     * 触发状态更新事件
     */
    triggerStateUpdate() {
        // 详细状态日志（仅在需要时输出）
        if (window.DEBUG_DSL) {
            console.log("DslEditor: Current state:", {
                expression: this.expression,
                astRoot: this.astRoot?.toExpression?.() || null,
                workspaceAST: this.workspaceAST?.toExpression?.() || null,
                mainExpressionAST: this.virtualRoot.main?.toExpression?.() || null,
                editingPathDepth: this.editingPath?.length || 0,
                isExpressionCompleted: this.isExpressionCompleted
            });
        }

        // 关键修复：状态更新时重新渲染编辑器
        this.renderEditor();

        // 触发全局状态更新（如果存在）
        if (typeof window.forceUpdateDebug === 'function') {
            window.forceUpdateDebug();
        }
    }

    /**
     * 派发表达式变更（防止重复触发）
     */
    emitExpressionChange(expr) {
        if (!this.onChange) return;
        if (this.lastEmittedExpression === expr) return;
        this.lastEmittedExpression = expr;
        try {
            this.onChange(expr);
        } catch (e) {
            console.error("DslEditor: onChange handler threw an error:", e);
        }
    }


    /**
     * 判断操作符是否为逻辑操作符
     */
    isLogicalOperator(operator) {
        return ['&&', '||'].includes(operator);
    }

    /**
     * 判断操作符是否为比较操作符
     */















    // 已迁移到 ui-workspace.js 的 renderExpressionPreviewUI

    /**
     * 获取DSL上下文对象
     */
    getDslContextObject(name) {
        // 检查是否有上下文覆盖 (用于子编辑器)
        if (this.contextOverride && this.contextOverride.name === name) {
            console.log(
                `DslEditor: Using context override for "${name}":`,
                this.contextOverride
            );
            return {
                name: this.contextOverride.name,
                displayName: this.contextOverride.displayName,
                typeName: this.contextOverride.typeName,
                properties: this.getTypeMetadata(this.contextOverride.typeName)?.properties || []
            };
        }

        const contextObjects = this.metadata?.dsl?.contextObjects || [];
        const result = contextObjects.find((obj) => obj.name === name);
        console.log(
            `DslEditor: Looking for context object "${name}", found:`,
            result
        );
        return result;
    }

    /**
     * 获取DSL属性
     */


    /**
     * 获取类型元数据
     */
    getTypeMetadata(typeName) {
        if (!typeName || !this.metadata?.dsl?.typeMetadata) {
            return null;
        }

        console.log(`DslEditor: Getting type metadata for: "${typeName}"`);

        // 首先尝试直接匹配类型名
        let result = this.metadata.dsl.typeMetadata[typeName];
        if (result) {
            console.log(`DslEditor: Found direct match for "${typeName}":`, result);
            return result;
        }

        const aliasTypeMap = {
            IGameEvent: 'GameEventBase'
        };
        const aliasTypeName = aliasTypeMap[typeName];
        if (aliasTypeName) {
            result = this.metadata.dsl.typeMetadata[aliasTypeName];
            if (result) {
                console.log(`DslEditor: Found alias match for "${typeName}" -> "${aliasTypeName}":`, result);
                return result;
            }
        }

        // 处理泛型类型，如 List<CardInstance> → CardInstance
        if (typeName.includes("<") && typeName.includes(">")) {
            const innerType = typeName.split("<")[1].replace(">", "");
            result = this.metadata.dsl.typeMetadata[innerType];
            console.log(
                `DslEditor: Extracted inner type "${innerType}" from "${typeName}":`,
                result
            );
            return result;
        }

        console.log(`DslEditor: No metadata found for type: "${typeName}"`);
        return null;
    }

    /**
     * 清空编辑器
     */
    clear() {
        this.clearWorkspace();
    }

    // ----------------------------------------------------------------
    // 类型检查辅助函数 (Type Checking Helpers)
    // ----------------------------------------------------------------

    /**
     * 判断一个类型名是否为基础类型 (e.g., "Int32", "Boolean")
     * @param {string} typeName - 类型名称
     * @returns {boolean}
     */
    isBaseType(typeName) {
        const baseTypes = [
            "Int32",
            "Number",
            "Boolean",
            "String",
            "Guid",
            "Single",
            "Double",
            "Int64",
        ];
        return baseTypes.includes(typeName);
    }

    /**
     * 判断一个类型名是否为集合类型 (e.g., "List<CardInstance>")
     * @param {string} typeName - 类型名称
     * @returns {boolean}
     */
    isCollectionType(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return false;
        return (
            normalized.startsWith("List<") ||
            normalized.startsWith("IEnumerable<") ||
            normalized === "List" ||
            normalized === "IEnumerable"
        );
    }

    /**
     * 从集合类型中提取内部泛型类型
     * @param {string} typeName - 集合类型名称 (e.g., "List<CardInstance>")
     * @returns {string|null} - 内部类型名称 (e.g., "CardInstance") or null
     */
    getCollectionInnerType(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!this.isCollectionType(normalized)) {
            return null;
        }
        const match = normalized.match(/<(.*)>/);
        return match ? match[1] : "Object";
    }

    // ----------------------------------------------------------------
    // 精确类型匹配辅助函数 (Type Matching Helpers)
    // ----------------------------------------------------------------

    /**
     * 检查基础类型之间的兼容性
     */
    areBaseTypesCompatible(sourceType, targetType) {
        // 数值类型兼容性
        const numericTypes = ['Int32', 'Int64', 'Single', 'Double'];
        if (numericTypes.includes(sourceType) && numericTypes.includes(targetType)) {
            return true;
        }

        // 字符串类型兼容性
        if (sourceType === 'String' && targetType === 'String') {
            return true;
        }

        // 布尔类型兼容性
        if (sourceType === 'Boolean' && targetType === 'Boolean') {
            return true;
        }

        // GUID类型兼容性
        if (sourceType === 'Guid' && targetType === 'Guid') {
            return true;
        }

        return false;
    }

    /**
     * 检查类型继承关系兼容性 (简化版)
     */
    isTypeInheritanceCompatible(sourceType, targetType) {
        // 获取类型元数据
        const sourceMetadata = this.getTypeMetadata(sourceType);
        const targetMetadata = this.getTypeMetadata(targetType);

        if (!sourceMetadata || !targetMetadata) {
            return false;
        }

        // 检查是否有共同的基类或接口
        // 这里可以根据实际的类型系统进行扩展

        // 简化实现：检查类型名称的相似性
        if (sourceType.includes(targetType) || targetType.includes(sourceType)) {
            return true;
        }

        return false;
    }

    /**
     * 检查类型是否兼容（特别是处理数字类型）
     * @param {string} actualType - 实际类型
     * @param {string} expectedType - 期望类型
     * @returns {boolean}
     */
    areTypesCompatible(actualType, expectedType) {
        const a = this.normalizeTypeName(actualType);
        const e = this.normalizeTypeName(expectedType);
        if (!a || !e) {
            return false; // 如果任一类型为空，则不兼容
        }
        if (a === e) {
            return true;
        }

        // 允许数值类型之间的兼容
        const numericTypes = ['Int32', 'Number', 'Single', 'Double', 'Int64'];
        if (numericTypes.includes(a) && numericTypes.includes(e)) {
            return true;
        }

        // 集合类型：允许 inner type 相同，或 expected 为 Object
        if (this.isCollectionType(a) && this.isCollectionType(e)) {
            const ai = this.getCollectionInnerType(a);
            const ei = this.getCollectionInnerType(e);
            return ei === 'Object' || ai === ei;
        }

        // 未来可以扩展更多兼容性规则，例如继承关系
        
        return false;
    }


    /**
     * 基于数据类型获取智能操作符推荐
     */
    // 委托 operators 模块，减少重复实现
    getSmartOperators(typeName) {
        return getSmartOperatorsFromModule(this, typeName);
    }

    /**
     * 检查是否为数值类型
     */
    isNumericType(typeName) {
        const numericTypes = ['Int32', 'Int64', 'Single', 'Double', 'Number'];
        return numericTypes.includes(typeName);
    }


    /**
     * 检查是否为业务对象类型（可以进行比较操作的对象类型）
     */
    isBusinessObjectType(typeName) {
        const businessObjectTypes = [
            'PlayerState',      // 玩家状态
            'CardInstance',     // 卡牌实例
            'CardDefinition',   // 卡牌定义
            'EffectInstance',   // 效果实例
            'EventInstance'     // 事件实例
        ];
        return businessObjectTypes.includes(typeName);
    }

    /**
     * 检查是否为文本类型（包括枚举和GUID）
     */
    isTextType(typeName) {
        const textTypes = ['String', 'Guid', 'CardType', 'Rarity', 'Craft', 'CardKeyword'];
        return textTypes.includes(typeName);
    }

    /**
     * 检查是否为对象类型
     */
    isObjectType(typeName) {
        // 如果不是基础类型、文本类型、布尔类型或集合类型，则认为是对象类型
        return !this.isBaseType(typeName) &&
               !this.isTextType(typeName) &&
               typeName !== 'Boolean' &&
               !this.isCollectionType(typeName);
    }


    /**
     * 获取枚举类型的可用值
     */
    getEnumValues(typeName) {
        const enumTypes = this.metadata?.dsl?.enumTypes || [];
        const enumType = enumTypes.find(e => e.name === typeName);
        return enumType?.values || [];
    }

    /**
     * 获取枚举类型的操作符
     */

    /**
     * 获取文本类型的操作符
     */
    // 委托 operators 模块
    getTextOperators() {
        return getTextOperatorsFromModule();
    }

    /**
     * 获取文本类型的函数
     */
    getTextFunctions() {
        const functions = this.metadata?.dsl?.functions || [];
        return functions
            .filter(f => f.name === 'contains' || f.name === 'isEmpty')
            .map(f => ({
                type: "function",
                value: f.name,
                label: f.name,
                description: f.description,
                meta: f
            }));
    }

    /**
     * 获取集合类型的函数
     */
    getCollectionFunctions() {
        const functions = this.metadata?.dsl?.functions || [];
        const collectionFunctionNames = ['count', 'sum', 'min', 'max', 'contains', 'isEmpty'];

        return functions
            .filter(f => collectionFunctionNames.includes(f.name))
            .map(f => ({
                type: "function",
                value: f.name,
                label: f.name,
                description: f.description,
                meta: f
            }));
    }

    /**
     * 获取列表查询操作
     */
    getListQueryOperations() {
        return [{
            type: "listQuery",
            value: ".query",
            label: "筛选 (.query)",
            description: "筛选集合元素"
        }];
    }


    /**
     * 获取逻辑链式操作选项 (用于布尔表达式的后续操作)
     * 主要是逻辑操作符，用于构建复合表达式
     */

    /**
     * 获取布尔表达式选项 (用于逻辑操作符的右操作数)
     * 包括：上下文对象、静态值（布尔类型）、函数调用等
     */
    getBooleanExpressionOptions() {
        let options = [];

        console.log("DslEditor: Getting boolean expression options");

        // 1. 上下文对象 (Context Object) - 可以构建布尔表达式
        const contextObjects = this.getContextObjectsForOptions();
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

        // 2.5 触发事件字段（如果提供了上下文）
        options.push(...this.getTriggeringEventFieldOptions());

        // 3. 布尔函数 (返回布尔值的函数)
        const functions = this.metadata?.dsl?.functions || [];
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



    /**
     * 获取数据源选项 (根据04-DSL节点类型与操作总览.md)
     * 数据源是抽象的起点，只包括：上下文对象、静态值
     */
    getDataSourceOptions() {
        let options = [];

        console.log("DslEditor: Getting data source options");

        // 1. 上下文对象 (Context Object)
        const contextObjects = this.getContextObjectsForOptions();
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

        // 3. 触发事件字段（如果提供了上下文）
        options.push(...this.getTriggeringEventFieldOptions());

        return options;
    }

    getContextObjectsForOptions() {
        const contextObjects = this.metadata?.dsl?.contextObjects || [];
        if (!this.contextOverride?.name) {
            return contextObjects;
        }

        const override = {
            name: this.contextOverride.name,
            displayName: this.contextOverride.displayName || this.contextOverride.name,
            description: this.contextOverride.description || this.contextOverride.displayName || this.contextOverride.name,
            typeName: this.contextOverride.typeName
        };

        const overrideName = String(override.name || '').toLowerCase();
        const filtered = contextObjects.filter(obj => String(obj.name || '').toLowerCase() !== overrideName);
        return [override, ...filtered];
    }

    getTriggeringEventFieldOptions() {
        const ctx = this.triggeringEventContext;
        const eventTypes = ctx?.eventTypes || [];
        if (!Array.isArray(eventTypes) || eventTypes.length === 0) return [];

        const timing = (ctx?.timing || 'On').toString();
        const allowOutput = timing.toLowerCase() === 'after';

        const selected = eventTypes[0];
        const ev = (this.metadata?.events || []).find(e =>
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

        const normalizeReturnType = (t) => this.normalizeTypeName(t) || 'Object';
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
                    description: `${p.description || p.Description || ''}（${scope}，${this.getDisplayTypeName(typeName)}）`,
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

    /**
     * 获取类型的显示名称
     */

    /**
     * 获取类型对应的输入控件类型
     */

    /**
     * 检查两个类型是否兼容
     */

    /**
     * 格式化静态值用于表达式 (根据智能推断的类型)
     */
    formatStaticValue(value, inferredType) {
        if (!value) return '""'; // 空值默认为空字符串

        // 数值类型直接返回
        if (this.isNumericType(inferredType)) {
            return value;
        }

        // 布尔类型直接返回
        if (inferredType === 'Boolean') {
            return value;
        }

        // 字符串类型
        if (inferredType === 'String') {
            // 如果已经有引号，直接返回
            if ((value.startsWith('"') && value.endsWith('"')) ||
                (value.startsWith("'") && value.endsWith("'"))) {
                return value;
            }
            // 否则添加引号
            return `"${value}"`;
        }

        // 默认加引号
        return `"${value}"`;
    }



    /**
     * 智能推断静态值的类型
     */
    inferStaticValueType(value) {
        if (!value) return "Object";

        // 布尔值
        if (value === "true" || value === "false") {
            return "Boolean";
        }

        // 数值 (整数)
        if (/^-?\d+$/.test(value)) {
            return "Int32";
        }

        // 数值 (浮点数)
        if (/^-?\d*\.\d+$/.test(value)) {
            return "Double";
        }

        // 字符串 (带引号)
        if ((value.startsWith('"') && value.endsWith('"')) ||
            (value.startsWith("'") && value.endsWith("'"))) {
            return "String";
        }

        // 默认为字符串
        return "String";
    }

    /**
     * 获取对象类型的属性
     */
    getObjectProperties(typeName) {
        const typeMetadata = this.getTypeMetadata(typeName);
        if (!typeMetadata || !typeMetadata.properties) {
            return [];
        }

        const displayTypeName = this.getDisplayTypeName(typeName);
        return typeMetadata.properties.map(prop => ({
            type: "property",
            value: prop.name,
            label: prop.displayName || prop.name,
            description: prop.description || `${displayTypeName}的${prop.name}属性`,
            returnType: prop.typeName
        }));
    }

    /**
     * 判断是否应该使用平级结构而不是嵌套结构
     */

    /**
     * 清空工作区（新设计）
     */
    clearWorkspace(preserveMainExpression = false) {
        console.log(`DslEditor: Clearing workspace (preserveMainExpression: ${preserveMainExpression})`);

        if (!preserveMainExpression) {
            this.virtualRoot.main = null;
        }

        // 重置工作区到初始状态
        this.initializeWorkspace();

        // 更新表达式和UI
        this.updateExpressionFromAST();
        this.renderEditor();

        console.log("DslEditor: Workspace cleared and re-initialized");
    }
    /**
     * 统一的进入编辑模式的入口
     * @param {ASTNode} parentNode - 要编辑的槽位的父节点
     * @param {'left'|'right'|'innerAST'|'filterExpression'|'sortExpression'|'countExpression'} slot - 要编辑的槽位
     */
    handleEnterEditModeForSlot(parentNode, slot, index = null) {
        console.log(`DslEditor: Entering edit mode for slot "${slot}"${index !== null ? ` (index ${index})` : ''} of node:`, parentNode);

        // 清除上一次的上下文覆盖
        this.contextOverride = null;
        this.slotExpectedType = null;

        this.clearEditingPath();
        this.pushEditingPath({ parentNode, slot, descriptor: `编辑 ${slot}`, index });
        const highlightTarget = this.getHighlightTargetNode ? this.getHighlightTargetNode() : null;
        this.selectedNode = highlightTarget?.metadata?.type === 'encapsulated' ? highlightTarget : null;

        // 统一解包逻辑
        let contentToEdit = null;
        if (parentNode.type === 'virtualRoot') {
            contentToEdit = parentNode.main;
            this.slotExpectedType = this.normalizeTypeName(this.expectedType);
        } else if (slot === 'left') {
            contentToEdit = parentNode.leftOperand;
        } else if (slot === 'right') {
            contentToEdit = parentNode.rightOperand;
        } else if (parentNode.type === 'functionCall' && slot === 'arg') {
            const argIndex = typeof index === 'number' ? index : 0;
            contentToEdit = (parentNode.args || [])[argIndex] || null;
            const p = (parentNode.parameters || [])[argIndex];
            if (p?.typeName) {
                this.slotExpectedType = this.normalizeTypeName(p.typeName);
            }
        } else if (parentNode.type === 'listQuery' && slot === 'filterExpression') {
            contentToEdit = parentNode.filterExpression;
            this.slotExpectedType = 'Boolean';
            const sourceReturnType = parentNode.source.returnType;
            const innerType = this.getCollectionInnerType(sourceReturnType);
            if (innerType) {
                this.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
                console.log(`DslEditor: Context override set for query. 'self' is now '${innerType}'`);
            }
        } else if (parentNode.type === 'listSort' && slot === 'sortExpression') {
            contentToEdit = parentNode.sortExpression;
            const sourceReturnType = parentNode.source.returnType;
            const innerType = this.getCollectionInnerType(sourceReturnType);
            if (innerType) {
                this.contextOverride = { name: 'self', displayName: '当前项 (self)', typeName: innerType };
                console.log(`DslEditor: Context override set for sort. 'self' is now '${innerType}'`);
            }
        } else if (parentNode.type === 'listTake' && slot === 'countExpression') {
            contentToEdit = parentNode.countExpression;
            this.slotExpectedType = 'Int32';
            // Take 操作的参数通常是数字，不需要上下文覆盖
        }
        
        // 如果槽位有内容，将其解包到工作区
        if (contentToEdit && contentToEdit.metadata?.type === 'encapsulated') {
            this.workspaceAST = this.cloneAST(contentToEdit.metadata.innerAST);
        } else {
            this.workspaceAST = this.cloneAST(contentToEdit);
        }

        this.astRoot = this.workspaceAST;
        this.isExpressionCompleted = false;

        this.renderEditor();
        console.log("DslEditor: Workspace is ready for editing the specified slot.");
    }

    // handleEditMainExpression 已废除，逻辑统一到 handleEnterEditModeForSlot

    /**
     * 解析空占位符所属的槽位与索引
     * @param {ASTNode} parentNode
     * @param {ASTNode} node
     */
    resolveEmptySlot(parentNode, node) {
        if (!parentNode) return { slot: null, index: null };

        if (parentNode.type === 'operator') {
            if (parentNode.leftOperand === node) return { slot: 'left', index: null };
            if (parentNode.rightOperand === node) return { slot: 'right', index: null };
        }

        if (parentNode.type === 'functionCall') {
            const args = parentNode.args || [];
            const index = args.indexOf(node);
            if (index >= 0) return { slot: 'arg', index };
        }

        if (parentNode.type === 'listQuery' && parentNode.filterExpression === node) {
            return { slot: 'filterExpression', index: null };
        }

        if (parentNode.type === 'listSort' && parentNode.sortExpression === node) {
            return { slot: 'sortExpression', index: null };
        }

        if (parentNode.type === 'listTake' && parentNode.countExpression === node) {
            return { slot: 'countExpression', index: null };
        }

        if (parentNode.type === 'virtualRoot' && parentNode.main === node) {
            return { slot: 'main', index: null };
        }

        return { slot: null, index: null };
    }

    /**
     * 渲染一个统一的、可点击的空槽位占位符
     * @param {ASTNode} parentNode - 占位符所属的父节点 (通常是操作符节点)
     * @param {'left'|'right'|'arg'|'filterExpression'|'sortExpression'|'countExpression'} slot - 占位符所属的槽位
     * @param {HTMLElement} container - 渲染的目标容器
     */
    renderEmptySlotPlaceholder(parentNode, slot, container, index = null) {
        const placeholderCapsule = document.createElement("span");
        placeholderCapsule.className = "dsl-placeholder-capsule";
        placeholderCapsule.style.cssText = `
            display: inline-flex;
            align-items: center;
            background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%);
            border: 3px solid #ffc107;
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: 700;
            color: #856404;
            box-shadow: 0 0 8px rgba(255, 193, 7, 0.6);
            cursor: pointer;
            transition: all 0.2s ease;
            position: relative;
            animation: pulse 2s infinite;
        `;

        // 确保脉冲动画的 keyframes 存在
        if (!document.getElementById('placeholder-pulse-style')) {
            const style = document.createElement('style');
            style.id = 'placeholder-pulse-style';
            style.textContent = `
                @keyframes pulse {
                    0% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                    50% { box-shadow: 0 0 16px rgba(255, 193, 7, 0.9); }
                    100% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                }
            `;
            document.head.appendChild(style);
        }

        placeholderCapsule.textContent = "?";
 
        placeholderCapsule.addEventListener('click', (e) => {
            e.stopPropagation();
            console.log(`DslEditor: Clicked empty placeholder for ${slot}${index !== null ? `[${index}]` : ''} operand of:`, parentNode);
            this.handleEnterEditModeForSlot(parentNode, slot, index);
        });

        container.appendChild(placeholderCapsule);
    }
    /**
     * 渲染属性访问链的UI
     */
    renderPropertyChainUI(node, container) {
        const chainContainer = document.createElement("div");
        const finalReturnType = this.calculateASTReturnType(node);
        const style = this.getStyleForValueType(finalReturnType);

        chainContainer.style.cssText = `
            display: inline-flex;
            align-items: center;
            background: ${style.background};
            color: ${style.color};
            padding: 4px 8px;
            border-radius: 4px;
            font-weight: 500;
            margin: 2px;
        `;

        const chainParts = this.getReadablePropertyChainParts(node.metadata?.fullPath || node.value);
        const parts = chainParts.length > 0 ? chainParts : (node.value || '').split('.');
        parts.forEach((part, index) => {
            const partElement = document.createElement("span");
            partElement.textContent = part;
            chainContainer.appendChild(partElement);

            if (index < parts.length - 1) {
                const separator = document.createElement("span");
                separator.textContent = ".";
                separator.style.cssText = "font-weight: bold; margin: 0 2px;";
                chainContainer.appendChild(separator);
            }
        });

        container.appendChild(chainContainer);
    }

    /**
     * 根据值的类型获取统一样式
     */
    getStyleForValueType(typeName) {
        // 默认样式
        let style = { background: '#f3e5f5', color: '#7b1fa2' }; // property

        if (this.isNumericType(typeName)) {
            style = { background: '#fff3e0', color: '#f57c00' }; // staticValue (orange)
        } else if (typeName === 'Boolean') {
            style = { background: '#e8f5e8', color: '#388e3c' }; // function (green)
        } else if (typeName === 'String' || this.isTextType(typeName)) {
            style = { background: '#f3e5f5', color: '#7b1fa2' }; // property (purple)
        } else if (typeName === 'context') {
            style = { background: '#e3f2fd', color: '#1976d2' }; // context (blue)
        }
        
        return style;
    }

    getReadableFunctionName(functionName) {
        const functions = this.metadata?.dsl?.functions || [];
        const match = functions.find(f =>
            String(f.name ?? f.Name ?? '').toLowerCase() === String(functionName ?? '').toLowerCase()
        );
        return match?.displayName || match?.DisplayName || match?.name || match?.Name || functionName;
    }

    getReadablePropertyChainParts(path) {
        const safePath = String(path || '').trim();
        if (!safePath) return [];

        const segments = safePath.split('.').filter(Boolean);
        if (segments.length === 0) return [];

        const contextName = segments[0];
        const ctx = this.getDslContextObject(contextName);
        let currentType = this.normalizeTypeName(ctx?.typeName);
        const displayParts = [ctx?.displayName || contextName];

        for (let i = 1; i < segments.length; i++) {
            const seg = segments[i];
            const tm = currentType ? this.getTypeMetadata(currentType) : null;
            const prop = (tm?.properties || []).find(p =>
                String(p.name || '').toLowerCase() === String(seg).toLowerCase()
            );
            displayParts.push(prop?.displayName || seg);
            currentType = this.normalizeTypeName(prop?.typeName) || null;
        }

        return displayParts;
    }

    getReadablePropertyChain(path) {
        const parts = this.getReadablePropertyChainParts(path);
        return parts.length > 0 ? parts.join('.') : path;
    }

    getReadableValueLabel(node) {
        if (!node) return '?';
        const meta = node.metadata || {};

        if (meta.type === 'eventField') {
            return meta.displayValue || node.value || '?';
        }

        if (meta.type === 'context') {
            return meta.displayName || this.getDslContextObject(node.value)?.displayName || node.value || '?';
        }

        if (meta.type === 'property') {
            const path = meta.fullPath || node.value || '';
            if (!path.includes('.') && meta.displayName) {
                return meta.displayName;
            }
            const readable = this.getReadablePropertyChain(path);
            return readable || meta.displayName || node.value || '?';
        }

        if (meta.type === 'staticValue') {
            return meta.staticValue || node.value || '?';
        }

        if (meta.type === 'rawText') {
            return node.value || '?';
        }

        return meta.displayName || node.value || '?';
    }

    getReadablePreviewText() {
        const node = this.virtualRoot?.main || null;
        if (!node) return '(空)';
        return this.toReadableText(node);
    }

    toReadableText(node) {
        if (!node) return '?';

        if (node.type === 'value') {
            if (node.metadata?.type === 'encapsulated' && node.metadata.innerAST) {
                return this.toReadableText(node.metadata.innerAST);
            }
            return this.getReadableValueLabel(node);
        }

        if (node.type === 'operator') {
            const opMap = {
                '&&': '且',
                '||': '或',
                '==': '等于',
                '!=': '不等于',
                '>': '大于',
                '>=': '大于等于',
                '<': '小于',
                '<=': '小于等于',
                '+': '加',
                '-': '减',
                '*': '乘',
                '/': '除'
            };
            const left = this.toReadableText(node.leftOperand);
            const right = this.toReadableText(node.rightOperand);
            const op = opMap[node.operator] || node.operator;
            return `(${left} ${op} ${right})`;
        }

        if (node.type === 'functionCall') {
            const args = (node.args || []).map((a) => (a ? this.toReadableText(a) : '?'));
            const fnName = this.getReadableFunctionName(node.functionName);
            return `${fnName}(${args.join(', ')})`;
        }

        if (node.type === 'listQuery') {
            const source = this.toReadableText(node.source);
            const pred = this.toReadableText(node.filterExpression);
            return `${source} 筛选(${pred})`;
        }
        if (node.type === 'listSort') {
            const source = this.toReadableText(node.source);
            const key = this.toReadableText(node.sortExpression);
            return `${source} 排序(${key})`;
        }
        if (node.type === 'listTake') {
            const source = this.toReadableText(node.source);
            const cnt = this.toReadableText(node.countExpression);
            return `${source} 取前(${cnt})`;
        }

        return '?';
    }
}
