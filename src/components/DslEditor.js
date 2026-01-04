// src/components/DslEditor.js - 可视化DSL编辑器
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md (第4节)
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md (第6节)
//

import { MetadataDrivenComponent } from "./MetadataDrivenComponent.js";

import {
    pushEditingPath,
    popEditingPath,
    peekEditingPath,
    clearEditingPath,
    getHighlightTargetNode,
    ensureAstRootSync,
    writeBackToParentSlot,
    getDslContextObject
} from "./dsl_editor/editing-session.js";
import { getSmartOperators as getSmartOperatorsFromModule, getTextOperators as getTextOperatorsFromModule } from "./dsl_editor/operators.js";

import { renderMainExpressionUI } from "./dsl_editor/ui-main.js";

import { renderWorkspaceUI, renderWorkspaceHeader } from "./dsl_editor/ui-workspace.js";
import {
    renderFunctionCallNodeAST,
    renderValueNode,
    renderEncapsulatedExpression,
    renderPlaceholderCapsule,
    renderNestedEncapsulatedExpression,
    renderOperatorNodeAST,
    renderStaticValueInput,
    renderRawTextInput,
    renderEmptySlotPlaceholder,
    renderPropertyChainUI,
    getEncapsulationStyle,
    createStatusBadge,
    removeBrackets,
    getStyleForValueType,
    getReadablePropertyChainParts,
    getReadablePropertyChain
} from "./dsl_editor/ui-nodes.js";
import { showAddNodeMenu, createSubmenu, addScrollbarStyles } from "./dsl_editor/ui-menus.js";
import {
    createPipelineEditState,
    unwrapEncapsulatedNode,
    getPipelineContext,
    openPipelineFromCurrent,
    getPipelineStageLabel,
    getPipelineStageSummary,
    getPipelineStageSlot,
    getPipelineStageExpressionNode,
    getPipelineStageExpressionText,
    isPipelineStageEditing,
    editPipelineStage,
    renderPipelineStageInlineEditor,
    commitPipelineStageEdit,
    cancelPipelineStageEdit,
    applyPipelineStageExpression,
    getPipelineStageTypeIssue,
    insertPipelineStage,
    removePipelineStage,
    movePipelineStage
} from "./dsl_editor/pipeline-editor.js";
import { buildAstFromExpression } from "./dsl_editor/ast-parse.js";
import { calculateASTReturnType, validateExpressionSemantics } from "./dsl_editor/type-check.js";
import { fetchFavoritesApi, markFavoriteUsedApi, createFavoriteApi } from "./dsl_editor/services/favorites.js";
import { requestParseAstApi } from "./dsl_editor/services/parse.js";
import { createValueNode, createOperatorNode, createFunctionCallNode, createListQueryNode, createListSortNode, createListTakeNode, validateAST, findIncompleteNode, findRightmostLeafNode } from "./dsl_editor/core.js";
import { renderPipelineBlock, renderListQueryNodeAST } from "./dsl_editor/ui-pipeline.js";
import {
    renderClickableMainExpression,
    renderASTNodeRecursive,
    renderNodeContent,
    renderClickableSubExpressions,
    renderClickableASTNode,
    renderClickableListNode
} from "./dsl_editor/ui-expression.js";
import { getReadableFunctionName, getReadableValueLabel } from "./dsl_editor/ui-labels.js";
import {
    removeFavoritesMenu,
    showFavoritesMenu,
    toggleFavoritesMenu,
    renderFavoritesToolbar,
    renderFavoritesSection
} from "./dsl_editor/ui-favorites.js";
import { getReadablePreviewText, toReadableText } from "./dsl_editor/ui-preview.js";
import {
    mapDslAstDtoToUiAst,
    mapLiteralDtoToNode,
    mapIdentifierDtoToNode,
    mapBinaryOperationDtoToNode,
    mapUnaryOperationDtoToNode,
    mapFunctionCallDtoToNode,
    mapListOperationFromMethodCall,
    tryMapEventFieldCall,
    getEventFieldMeta,
    getFunctionSignature,
    mapOperatorTokenToSymbol,
    formatLiteralValue,
    createPropertyChainNode,
    flattenMemberAccessPath
} from "./dsl_editor/ast-mapping.js";
import {
    performASTGoBack,
    saveCurrentLayer,
    createRootNode,
    extendAST,
    createFunctionCallFromCurrent,
    addArgToFunctionCall,
    addRightOperandFromWorkspace,
    addRightOperandToOperator,
    createOperatorFromCurrent,
    createQueryNodeFromCurrent,
    createSortNodeFromCurrent,
    createTakeNodeFromCurrent,
    handleEnterEditModeForSlot
} from "./dsl_editor/ast-editing.js";
import {
    setParseStatus,
    getParseStatusInfo,
    showExpressionError,
    showError as showErrorFromModule
} from "./dsl_editor/ui-feedback.js";
import {
    getCurrentEditingOptions,
    getOptionsForIncompleteNode,
    getOptionsForCompleteNode,
    getOptionsForType,
    getBooleanExpressionOptions,
    getDataSourceOptions,
    getContextObjectsForOptions,
    getTriggeringEventFieldOptions
} from "./dsl_editor/options-provider.js";
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
        this.pipelineEditState = createPipelineEditState();
        this.parseStatus = {
            state: "idle",
            message: "",
            errors: []
        };
        this.allowRawTextInput = true;
        this.isPipelineInline = false;
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
            return pushEditingPath(this, entry);
    }

        /**
         * 出栈：返回上一级编辑
         * @returns {object|undefined}
         */
    popEditingPath() {
            return popEditingPath(this);
    }

        /**
         * 计算主表达式中需要高亮的目标节点（基于编辑路径栈顶）
         */
    getHighlightTargetNode() {
            return getHighlightTargetNode(this);
    }

        /**
         * 查看当前栈顶
         */
    peekEditingPath() {
            return peekEditingPath(this);
    }

        /** 清空编辑路径 */
    clearEditingPath() {
            return clearEditingPath(this);
    }

        /**
         * 保持 astRoot 始终指向工作区 AST
         */
    ensureAstRootSync() {
            return ensureAstRootSync(this);
    }

        /**
         * 将封装后的 newValueNode 写回父节点指定槽位
         * 注意：slot='self' 需要由上一层路径项决定如何替换，此处只处理 left/right
         */
    writeBackToParentSlot(parentNode, slot, newValueNode) {
            return writeBackToParentSlot(this, parentNode, slot, newValueNode);
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
        return getCurrentEditingOptions(this);
    }

    // 已迁移到 core.js 模块

    // 已迁移到 core.js 模块

    /**
     * 为不完整节点获取选项
     */
    getOptionsForIncompleteNode(node) {
        return getOptionsForIncompleteNode(this, node);
    }

    /**
     * 为完整节点获取后续选项
     */
    getOptionsForCompleteNode(node) {
        return getOptionsForCompleteNode(this, node);
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
        return getOptionsForType(this, type);
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
            } else if (['|', '&'].includes(parentOperator)) {
                if (isRightOperand && node.parent.leftOperand) {
                    result.expectedType = this.calculateASTReturnType(node.parent.leftOperand);
                }
                console.log("DslEditor: calculateNodeTypeMatch - set operator expects:", result.expectedType);
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
            } else if (parentOperator === '-') {
                if (isRightOperand && node.parent.leftOperand) {
                    const leftType = this.calculateASTReturnType(node.parent.leftOperand);
                    if (this.isCollectionType(leftType)) {
                        result.expectedType = leftType;
                    }
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
        return validateExpressionSemantics(this, node);
    }

    /**
     * 计算AST节点的实际返回类型
     */
    calculateASTReturnType(node) {
        return calculateASTReturnType(this, node);
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
            showErrorFromModule(this, `渲染DSL编辑器失败: ${error.message}`, container);
        }
    }

    buildAstFromExpression(expression) {
        return buildAstFromExpression(this, expression);
    }

    // ---------------------------
    // Pipeline editor helpers
    // ---------------------------
    unwrapEncapsulatedNode(node) {
        return unwrapEncapsulatedNode(this, node);
    }

    getPipelineContext() {
        return getPipelineContext(this);
    }

    openPipelineFromCurrent() {
        return openPipelineFromCurrent(this);
    }

    getPipelineStageLabel(stage) {
        return getPipelineStageLabel(this, stage);
    }

    getPipelineStageSummary(stage) {
        return getPipelineStageSummary(this, stage);
    }

    getPipelineStageSlot(node) {
        return getPipelineStageSlot(this, node);
    }

    getPipelineStageExpressionNode(node, slot) {
        return getPipelineStageExpressionNode(this, node, slot);
    }

    getPipelineStageExpressionText(node, slot) {
        return getPipelineStageExpressionText(this, node, slot);
    }

    isPipelineStageEditing(stage) {
        return isPipelineStageEditing(this, stage);
    }

    editPipelineStage(stage) {
        return editPipelineStage(this, stage);
    }

    renderPipelineStageInlineEditor(stage, container) {
        return renderPipelineStageInlineEditor(this, stage, container);
    }

    commitPipelineStageEdit() {
        return commitPipelineStageEdit(this);
    }

    cancelPipelineStageEdit() {
        return cancelPipelineStageEdit(this);
    }

    applyPipelineStageExpression(stageNode, slot, expression) {
        return applyPipelineStageExpression(this, stageNode, slot, expression);
    }

    getPipelineStageTypeIssue(stage) {
        return getPipelineStageTypeIssue(this, stage);
    }

    insertPipelineStage(stageType, insertIndex = null) {
        return insertPipelineStage(this, stageType, insertIndex);
    }

    removePipelineStage(index) {
        return removePipelineStage(this, index);
    }

    movePipelineStage(fromIndex, toIndex) {
        return movePipelineStage(this, fromIndex, toIndex);
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
        return fetchFavoritesApi({
            userId: this.userId,
            query,
            expectedType,
            limit,
            page
        });
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
        return setParseStatus(this, state, message, errors);
    }

    getParseStatusInfo() {
        return getParseStatusInfo(this);
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
        return requestParseAstApi(expression);
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
        if (!this.allowRawTextInput) {
            this.setParseStatus("fallback", message, errors);
            this.showExpressionError(message);
            this.initializeWorkspace();
            return;
        }
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
        return mapDslAstDtoToUiAst(this, dto);
    }

    mapLiteralDtoToNode(dto) {
        return mapLiteralDtoToNode(this, dto);
    }

    mapIdentifierDtoToNode(dto) {
        return mapIdentifierDtoToNode(this, dto);
    }

    mapBinaryOperationDtoToNode(dto) {
        return mapBinaryOperationDtoToNode(this, dto);
    }

    mapUnaryOperationDtoToNode(dto) {
        return mapUnaryOperationDtoToNode(this, dto);
    }

    mapFunctionCallDtoToNode(dto) {
        return mapFunctionCallDtoToNode(this, dto);
    }

    mapListOperationFromMethodCall(methodName, receiverDto, dto) {
        return mapListOperationFromMethodCall(this, methodName, receiverDto, dto);
    }

    tryMapEventFieldCall(name, dto) {
        return tryMapEventFieldCall(this, name, dto);
    }

    getEventFieldMeta(propertyName, returnType) {
        return getEventFieldMeta(this, propertyName, returnType);
    }

    getFunctionSignature(name) {
        return getFunctionSignature(this, name);
    }

    mapOperatorTokenToSymbol(token) {
        return mapOperatorTokenToSymbol(this, token);
    }

    formatLiteralValue(value, returnType) {
        return formatLiteralValue(this, value, returnType);
    }

    createPropertyChainNode(segments) {
        return createPropertyChainNode(this, segments);
    }

    flattenMemberAccessPath(dto) {
        return flattenMemberAccessPath(this, dto);
    }

    async markFavoriteUsed(favoriteId) {
        if (!this.userId || !favoriteId) return;
        const result = await markFavoriteUsedApi(this.userId, favoriteId);
        if (!result?.success) {
            console.warn("DslEditor: Failed to update favorite usage.", result?.status);
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
            const created = await createFavoriteApi(payload);
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
        return removeFavoritesMenu(this);
    }

    showFavoritesMenu(anchor) {
        return showFavoritesMenu(this, anchor);
    }

    toggleFavoritesMenu(anchor) {
        return toggleFavoritesMenu(this, anchor);
    }

    renderFavoritesToolbar() {
        return renderFavoritesToolbar(this);
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
        return renderWorkspaceHeader(this, container);
    }

    /**
     * 渲染可点击的主表达式
     */
    renderClickableMainExpression(node, container) {
        return renderClickableMainExpression(this, node, container);
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
        return renderASTNodeRecursive(this, node, container);
    }

    /**
     * 渲染节点内容（不包含完成状态包装）
     */
    renderNodeContent(node, container) {
        return renderNodeContent(this, node, container);
    }

    renderPipelineBlock(node, container, renderNodeFn) {
        return renderPipelineBlock(this, node, container, renderNodeFn);
    }

    renderFunctionCallNodeAST(node, container) {
        return renderFunctionCallNodeAST(this, node, container);
    }

    /**
     * 渲染值节点
     */
    renderValueNode(node, container) {
        return renderValueNode(this, node, container);
    }

    /**
     * 渲染封装的完成表达式（胶囊样式）
     */
    renderEncapsulatedExpression(node, container) {
        return renderEncapsulatedExpression(this, node, container);
    }
/**
     * 渲染一个“待编辑”状态的占位符胶囊
     * @param {ASTNode} node - 封装了 empty 节点的父节点
     * @param {HTMLElement} container - 渲染的目标容器
     */
    renderPlaceholderCapsule(node, container) {
        return renderPlaceholderCapsule(this, node, container);
    }

    /**
     * 根据节点状态获取封装表达式的样式
     * @param {ASTNode} node - 封装节点
     * @returns {object} - 包含样式属性的对象
     */
    getEncapsulationStyle(node) {
        return getEncapsulationStyle(this, node);
    }

    /**
     * 创建状态徽章元素
     * @param {object} style - 从 getEncapsulationStyle 获取的样式对象
     * @returns {HTMLElement|null}
     */
    createStatusBadge(style) {
        return createStatusBadge(style);
    }

    /**
     * 渲染嵌套的封装表达式（内部样式框）
     */
    renderNestedEncapsulatedExpression(innerNode, container, parentEncapsulatedNode) {
        return renderNestedEncapsulatedExpression(this, innerNode, container);
    }

    /**
     * 移除文本中的外层括号
     */
    removeBrackets(text) {
        return removeBrackets(text);
    }

    /**
     * 渲染可点击的子表达式（支持嵌套样式框）
     */
    renderClickableSubExpressions(innerAST, container, encapsulatedNode, isParentSelected = false) {
        return renderClickableSubExpressions(this, innerAST, container, encapsulatedNode, isParentSelected);
    }

    /**
     * 渲染可点击的AST节点
     */
    renderClickableASTNode(node, container, encapsulatedNode, isParentSelected = false) {
        return renderClickableASTNode(this, node, container, encapsulatedNode, isParentSelected);
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
        return renderClickableListNode(this, node, container, encapsulatedNode);
    }

    renderListQueryNodeAST(node, container) {
        return renderListQueryNodeAST(this, node, container);
    }

    renderOperatorNodeAST(node, container) {
        return renderOperatorNodeAST(this, node, container);
    }

    /**
     * 渲染静态值输入框 (AST版本)
     */
    renderStaticValueInput(node, container) {
        return renderStaticValueInput(this, node, container);
    }

    /**
     * 渲染原始文本输入框（解析失败降级）
     */
    renderRawTextInput(node, container) {
        return renderRawTextInput(this, node, container);
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
        return performASTGoBack(this);
    }

    /**
     * 显示添加节点菜单
     */
    showAddNodeMenu(button) {
        return showAddNodeMenu(this, button);
    }

    renderFavoritesSection(menu, closeMenu) {
        return renderFavoritesSection(this, menu, closeMenu);
    }

    /**
     * 创建独立的子菜单
     */
    createSubmenu(options, parentItem, mainMenu) {
        return createSubmenu(this, options, parentItem, mainMenu);
    }

    /**
     * 添加自定义滚动条样式
     */
    addScrollbarStyles(element) {
        return addScrollbarStyles(this, element);
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
        return saveCurrentLayer(this);
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
        return showExpressionError(this, message);
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
        return createRootNode(this, option);
    }

    /**
     * 扩展现有AST
     */
    extendAST(option) {
        return extendAST(this, option);
    }

    createFunctionCallFromCurrent(option) {
        return createFunctionCallFromCurrent(this, option);
    }

    addArgToFunctionCall(functionNode, option) {
        return addArgToFunctionCall(this, functionNode, option);
    }

    /**
     * 从工作区添加右操作数（新设计）
     */
    addRightOperandFromWorkspace(option) {
        return addRightOperandFromWorkspace(this, option);
    }

    /**
     * 为操作符添加右操作数
     */
    addRightOperandToOperator(operatorNode, option) {
        return addRightOperandToOperator(this, operatorNode, option);
    }

    /**
     * 从当前工作区创建一个操作符节点（新架构：交互驱动封装）
     * @param {string} operator - 操作符，例如 ">", "=="
     */
    createOperatorFromCurrent(option) {
        return createOperatorFromCurrent(this, option);
    }

    /**
     * 从当前工作区创建一个查询节点
     */
    createQueryNodeFromCurrent(option) {
        return createQueryNodeFromCurrent(this, option);
    }

    createSortNodeFromCurrent(option) {
        return createSortNodeFromCurrent(this, option);
    }

    createTakeNodeFromCurrent(option) {
        return createTakeNodeFromCurrent(this, option);
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
            '|': 5,
            '&': 6,
            '+': 7,
            '-': 7,
            '*': 8,
            '/': 8
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
        return getDslContextObject(this, name);
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
        return getBooleanExpressionOptions(this);
    }



    /**
     * 获取数据源选项 (根据04-DSL节点类型与操作总览.md)
     * 数据源是抽象的起点，只包括：上下文对象、静态值
     */
    getDataSourceOptions() {
        return getDataSourceOptions(this);
    }

    getContextObjectsForOptions() {
        return getContextObjectsForOptions(this);
    }

    getTriggeringEventFieldOptions() {
        return getTriggeringEventFieldOptions(this);
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
        return handleEnterEditModeForSlot(this, parentNode, slot, index);
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
        return renderEmptySlotPlaceholder(this, parentNode, slot, container, index);
    }
    /**
     * 渲染属性访问链的UI
     */
    renderPropertyChainUI(node, container) {
        return renderPropertyChainUI(this, node, container);
    }

    /**
     * 根据值的类型获取统一样式
     */
    getStyleForValueType(typeName) {
        return getStyleForValueType(this, typeName);
    }

    getReadableFunctionName(functionName) {
        return getReadableFunctionName(this, functionName);
    }

    getReadablePropertyChainParts(path) {
        return getReadablePropertyChainParts(this, path);
    }

    getReadablePropertyChain(path) {
        return getReadablePropertyChain(this, path);
    }

    getReadableValueLabel(node) {
        return getReadableValueLabel(this, node);
    }

    getReadablePreviewText() {
        return getReadablePreviewText(this);
    }

    toReadableText(node) {
        return toReadableText(this, node);
    }
}
