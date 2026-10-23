// src/components/DslEditor.js - 可视化DSL编辑器
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md (第4节)
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md (第6节)
//

import { MetadataDrivenComponent } from "./MetadataDrivenComponent.js";
import { getBlockingLoader } from "./blockingLoader.js";
import { ensureEnumLocalization as ensureSharedEnumLocalization } from "../enumLocalizationCache.js";

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
    renderPropertyInput,
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
    movePipelineStage,
    syncPipelineContext,
    markPipelineContextDirty
} from "./dsl_editor/pipeline-editor.js";
import { buildAstFromExpression } from "./dsl_editor/ast-parse.js";
import { validateExpressionSemantics } from "./dsl_editor/type-check.js";
import { fetchFavoritesApi, markFavoriteUsedApi, createFavoriteApi, deleteFavoriteApi } from "./dsl_editor/services/favorites.js";
import { requestParseAstApi } from "./dsl_editor/services/parse.js";
import { createValueNode, createOperatorNode, createFunctionCallNode, createListQueryNode, createListSortNode, createListTakeNode, createListDistinctNode, createListSelectNode, validateAST, findIncompleteNode, findRightmostLeafNode, getSemanticNodeType as getCoreSemanticNodeType, canSemanticOperateAsSetOperand } from "./dsl_editor/core.js";
import { renderPipelineBlock, renderListQueryNodeAST } from "./dsl_editor/ui-pipeline.js";
import { finalizeDraftInputs, refreshCompleteness, refreshValueNodeReturnTypes } from "./dsl_editor/ast-normalize.js";
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
import { resolveScopedSelfContextFromSource } from "./dsl_editor/event-translation.js";
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
            page: 1,
            limit: 6,
            total: 0,
            totalPages: 1,
            loading: false,
            error: null
        };
        this.favoriteSearchTimer = null;
        this.favoriteMenuAnchor = null;
        this.favoriteMenuOutsideHandler = null;
        this.pipelineOpen = false;
        this.pipelineOpenSource = null;
        this.pipelineOpenMode = null;
        this.pipelineEditState = createPipelineEditState();
        this.pipelineContextStore = new Map();
        this.workspaceEditContext = null;
        this.pipelineNodeIds = new WeakMap();
        this.pipelineNodeIdSeed = 1;
        this.pipelineActiveContextKey = null;
        this.parseStatus = {
            state: "idle",
            message: "",
            errors: []
        };
        this.allowRawTextInput = true;
        this.isPipelineInline = false;
        this.isLightWorkspace = false;
        this.lightWorkspaceFocus = null;
        this.fixedRootContext = null;
        this.hideFavoritesToolbar = false;
        this.enumLocalizationCache = new Map();
        this.enumLocalizationRequests = new Map();
    }

    get astRoot() {
        return this.workspaceAST;
    }

    set astRoot(value) {
        this.workspaceAST = value;
    }

    assertAstAlias() {
        if (Object.prototype.hasOwnProperty.call(this, "astRoot")) {
            console.warn("DslEditor: astRoot should be derived from workspaceAST; instance property detected.");
        }
    }

    async initialize() {
        if (this.isInitialized) {
            return;
        }

        await super.initialize();
        await this.preloadEnumLocalizations();
    }

    splitGenericTypeArguments(raw) {
        const text = String(raw || "").trim();
        if (!text) return [];
        const result = [];
        let depth = 0;
        let current = "";
        for (let i = 0; i < text.length; i++) {
            const ch = text[i];
            if (ch === "<") {
                depth++;
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

    normalizeTypeName(typeName) {
        if (!typeName) return null;
        const trimmed = String(typeName).trim().replace(/^global::/i, "");
        if (!trimmed) return null;

        const simpleAliasMap = {
            "bool": "Boolean",
            "boolean": "Boolean",
            "system.boolean": "Boolean",
            "string": "String",
            "system.string": "String",
            "object": "Object",
            "system.object": "Object",
            "guid": "Guid",
            "system.guid": "Guid",
            "int": "Int32",
            "int32": "Int32",
            "system.int32": "Int32",
            "long": "Int64",
            "int64": "Int64",
            "system.int64": "Int64",
            "float": "Single",
            "single": "Single",
            "system.single": "Single",
            "double": "Double",
            "system.double": "Double",
            "decimal": "Decimal",
            "system.decimal": "Decimal",
            "number": "Number"
        };

        const lower = trimmed.toLowerCase();
        if (Object.prototype.hasOwnProperty.call(simpleAliasMap, lower)) {
            return simpleAliasMap[lower];
        }

        const nullable = trimmed.match(/^Nullable<(.+)>$/i);
        if (nullable) {
            return this.normalizeTypeName(nullable[1]) || nullable[1].trim();
        }

        const genericMatch = trimmed.match(/^([^<]+)<(.+)>$/);
        if (genericMatch) {
            const containerRaw = genericMatch[1].trim();
            const containerLower = containerRaw.toLowerCase();
            const rawArgs = this.splitGenericTypeArguments(genericMatch[2]);
            const normalizedArgs = rawArgs.map(arg => this.normalizeTypeName(arg) || arg.trim());

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

    resolveTypeAlias(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return null;

        const aliasMap = {
            CardInstanceSnapshot: 'CardInstance',
            CardInstanceResetSnapshot: 'CardInstance',
            CardDefinitionSnapshot: 'CardDefinition'
        };

        const listMatch = normalized.match(/^List<(.+)>$/);
        if (listMatch) {
            const innerAlias = this.resolveTypeAlias(listMatch[1]) || listMatch[1];
            return `List<${innerAlias}>`;
        }

        return aliasMap[normalized] || normalized;
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
         * 轻工作区：设置当前聚焦槽位（不进入编辑路径）
         */
    setLightWorkspaceFocus(parentNode, slot, index = null) {
            this.lightWorkspaceFocus = { parentNode, slot, index };
            if (this.isLightWorkspace) {
                this.clearEditingPath();
            }
            this.syncLightWorkspaceFocus();
    }

        /**
         * 轻工作区：清除聚焦槽位
         */
    clearLightWorkspaceFocus() {
            this.lightWorkspaceFocus = null;
            this.slotExpectedType = null;
            if (!this.isPipelineInline) {
                this.contextOverride = null;
            }
    }

        /**
         * 轻工作区：同步槽位期望类型与上下文覆盖
         */
    syncLightWorkspaceFocus() {
            if (!this.isLightWorkspace) return;
            const focus = this.lightWorkspaceFocus;
            const preservedContext = this.isPipelineInline && this.contextOverride?.name
                ? this.contextOverride
                : null;
            const restoreContext = () => {
                if (!this.contextOverride && preservedContext) {
                    this.contextOverride = preservedContext;
                }
            };
            if (!focus?.parentNode) {
                this.slotExpectedType = this.normalizeTypeName(this.expectedType);
                if (!this.isPipelineInline) {
                    this.contextOverride = null;
                }
                return;
            }

            const { parentNode, slot, index } = focus;
            this.slotExpectedType = null;
            this.contextOverride = null;

            if (parentNode.type === 'virtualRoot' && slot === 'main') {
                this.slotExpectedType = this.normalizeTypeName(this.expectedType);
                restoreContext();
                return;
            }

            if (parentNode.type === 'operator' && (slot === 'left' || slot === 'right')) {
                const expected = this.getExpectedTypeForSlot(parentNode, slot, index);
                if (expected) {
                    this.slotExpectedType = this.normalizeTypeName(expected);
                }
                restoreContext();
                return;
            }

            if (parentNode.type === 'functionCall' && slot === 'arg') {
                const p = (parentNode.parameters || [])[typeof index === 'number' ? index : 0];
                if (p?.typeName) {
                    this.slotExpectedType = this.normalizeTypeName(p.typeName);
                }
                restoreContext();
                return;
            }

            const setSelfContext = (sourceType, sourceNode) => {
                const scopedContext = resolveScopedSelfContextFromSource(this, sourceNode, sourceType);
                if (scopedContext) {
                    this.contextOverride = scopedContext;
                }
            };

            if (parentNode.type === 'listQuery' && slot === 'filterExpression') {
                this.slotExpectedType = 'Boolean';
                const sourceReturnType = this.getSemanticNodeType(parentNode.source);
                setSelfContext(sourceReturnType, parentNode.source);
                restoreContext();
                return;
            }

            if (parentNode.type === 'listSort' && slot === 'sortExpression') {
                this.slotExpectedType = 'NonEntity';
                const sourceReturnType = this.getSemanticNodeType(parentNode.source);
                setSelfContext(sourceReturnType, parentNode.source);
                restoreContext();
                return;
            }

            if (parentNode.type === 'listTake' && slot === 'countExpression') {
                this.slotExpectedType = 'Int32';
                restoreContext();
                return;
            }

            if (parentNode.type === 'listDistinct' && slot === 'keyExpression') {
                this.slotExpectedType = 'ScalarOrScalarList';
                const sourceReturnType = this.getSemanticNodeType(parentNode.source);
                setSelfContext(sourceReturnType, parentNode.source);
                restoreContext();
                return;
            }
            if (parentNode.type === 'listSelect' && slot === 'selectExpression') {
                this.slotExpectedType = 'Object';
                const sourceReturnType = this.getSemanticNodeType(parentNode.source);
                setSelfContext(sourceReturnType, parentNode.source);
                restoreContext();
                return;
            }
            restoreContext();
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
    writeBackToParentSlot(parentNode, slot, newValueNode, index = null) {
            return writeBackToParentSlot(this, parentNode, slot, newValueNode, index);
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


        // 3. 统一入口：立即进入对 main 槽位的编辑
        this.handleEnterEditModeForSlot(this.virtualRoot, 'main');
    }

    // 已迁移到 core.js 模块

    // 已迁移到 core.js 模块

    /**
     * 获取当前编辑位置的可用选项
     */
    getCurrentEditingOptions() {
        this.syncLightWorkspaceFocus();
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


        // 关键修复：如果是封装表达式且没有父节点，说明它是完整的表达式，不应该查找内部
        if (node.metadata && node.metadata.type === "encapsulated" && !node.parent) {
            return node;
        }

        // 如果是操作符节点，找到最右边的操作数
        if (node.type === 'operator') {
            if (node.rightOperand) {
                // 递归查找右操作数中的最后一个节点
                return this.findLastNodeInExpression(node.rightOperand);
            } else if (node.leftOperand) {
                return this.findLastNodeInExpression(node.leftOperand);
            }
        }

        // 如果是封装表达式且有父节点，检查其内部AST
        if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
            return this.findLastNodeInExpression(node.metadata.innerAST);
        }

        // 如果是值节点，就是最后一个节点
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
        return this.getSemanticNodeType(node) || 'Object';
    }

    /**
     * 语义类型统一入口：业务判定只依赖 AST 推导结果
     */
    getSemanticNodeType(node) {
        if (!node) return null;
        const computedType = getCoreSemanticNodeType(node);
        const normalized = this.normalizeTypeName(computedType);
        if (!normalized || normalized === 'Void') {
            return null;
        }
        return normalized;
    }

    normalizeSetOperandExpectedType(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return null;
        if (this.isCollectionType(normalized)) return normalized;
        if (normalized === 'String' || normalized === 'Void' || normalized === 'Object') return normalized;
        return `List<${normalized}>`;
    }

    canOperateAsSetOperand(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        return canSemanticOperateAsSetOperand(normalized);
    }

    canPromoteToCollection(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized || this.isCollectionType(normalized)) return false;
        return this.canOperateAsSetOperand(normalized);
    }

    canOpenPipelineOnType(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized || normalized === 'Void' || normalized === 'Object') return false;
        if (this.isCollectionType(normalized)) return true;
        if (this.isDictionaryType(normalized)) return false;
        if (this.isNumericType(normalized)) return false;
        if (normalized === 'Boolean') return false;
        if (this.isTextType(normalized)) return false;
        return this.isObjectType(normalized) || this.isBusinessObjectType(normalized);
    }

    /**
     * 获取指定槽位的期望类型（统一规则）
     */
    getExpectedTypeForSlot(parentNode, slot, index) {
        if (!parentNode) {
            return this.normalizeTypeName(this.expectedType);
        }

        const isEmptyValueNode = (node) => {
            if (!node || node.type !== 'value') return false;
            if (node.metadata?.type === 'empty') return true;
            if (node.metadata?.type === 'encapsulated' && node.metadata?.innerAST?.metadata?.type === 'empty') {
                return true;
            }
            return false;
        };

        const resolveUsableNodeType = (node) => {
            if (!node || isEmptyValueNode(node)) return null;
            return this.getSemanticNodeType(node);
        };

        if (parentNode.type === 'virtualRoot' && slot === 'main') {
            return this.normalizeTypeName(this.expectedType);
        }

        if (parentNode.type === 'functionCall' && slot === 'arg') {
            const argIndex = typeof index === 'number' ? index : 0;
            const param = (parentNode.parameters || [])[argIndex];
            const paramType = this.normalizeTypeName(param?.typeName);
            if (paramType && paramType !== 'Object') {
                return paramType;
            }
            if (argIndex > 0) {
                const receiver = (parentNode.args || [])[0];
                const receiverType = receiver ? this.getSemanticNodeType(receiver) : null;
                if (receiverType) {
                    if (this.isCollectionType(receiverType)) {
                        return this.normalizeTypeName(this.getCollectionInnerType(receiverType)) || 'Object';
                    }
                    if (this.isTextType(receiverType)) {
                        return 'String';
                    }
                }
            }
            return paramType;
        }

        if (parentNode.type === 'listQuery' && slot === 'filterExpression') {
            return 'Boolean';
        }

        if (parentNode.type === 'listSort' && slot === 'sortExpression') {
            return 'NonEntity';
        }

        if (parentNode.type === 'listTake' && slot === 'countExpression') {
            return 'Int32';
        }

        if (parentNode.type === 'listDistinct' && slot === 'keyExpression') {
            return 'ScalarOrScalarList';
        }
        if (parentNode.type === 'listSelect' && slot === 'selectExpression') {
            return 'Object';
        }

        if (parentNode.type === 'operator') {
            const op = parentNode.operator;
            if (['&&', '||'].includes(op)) {
                return 'Boolean';
            }
            if (['|', '&', '~'].includes(op)) {
                const sibling = slot === 'left' ? parentNode.rightOperand : parentNode.leftOperand;
                const siblingType = resolveUsableNodeType(sibling);
                if (siblingType) return this.normalizeSetOperandExpectedType(siblingType);
                const currentNode = slot === 'left' ? parentNode.leftOperand : parentNode.rightOperand;
                return this.normalizeSetOperandExpectedType(resolveUsableNodeType(currentNode));
            }
            if (['==', '!=', '>', '<', '>=', '<='].includes(op)) {
                const sibling = slot === 'left' ? parentNode.rightOperand : parentNode.leftOperand;
                const siblingType = resolveUsableNodeType(sibling);
                if (siblingType) return siblingType;
                const currentNode = slot === 'left' ? parentNode.leftOperand : parentNode.rightOperand;
                return resolveUsableNodeType(currentNode);
            }
            if (['contains', 'startsWith', 'endsWith'].includes(op)) {
                const sibling = slot === 'left' ? parentNode.rightOperand : parentNode.leftOperand;
                const normalizedSibling = resolveUsableNodeType(sibling);
                if (normalizedSibling) {
                    if (this.isCollectionType(normalizedSibling)) {
                        const inner = this.getCollectionInnerType(normalizedSibling);
                        return slot === 'right'
                            ? (this.normalizeTypeName(inner) || normalizedSibling)
                            : normalizedSibling;
                    }
                    if (this.isTextType(normalizedSibling)) {
                        return 'String';
                    }
                }
                const currentNode = slot === 'left' ? parentNode.leftOperand : parentNode.rightOperand;
                const currentType = resolveUsableNodeType(currentNode);
                return currentType || 'String';
            }
            if (op === '-') {
                return 'Number';
            }
            if (['+', '*', '/', '\\', '%'].includes(op)) {
                return 'Number';
            }
        }

        // 不再回退到 slotExpectedType，避免工作区状态污染主表达式推断
        return this.normalizeTypeName(this.expectedType);
    }

    /**
     * 将期望类型转为占位提示文案
     */
    getPlaceholderLabel(expectedType) {
        if (!expectedType) return '待填写';
        if (String(expectedType).toLowerCase() === 'object') return '任意类型';
        const display = this.getDisplayTypeName(expectedType);
        return display || '待填写';
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
            return result;
        }


        // 计算实际类型
        result.actualType = this.getSemanticNodeType(node) || 'Object';

        const hasEffectiveParent = node.parent && node.parent.type !== 'virtualRoot';

        // 计算预期类型（基于父节点上下文）
        if (node.parent && node.parent.type === 'operator') {
            const parentOperator = node.parent.operator;
            const isRightOperand = node.parent.rightOperand === node;


            // 根据操作符确定预期类型
            if (['&&', '||'].includes(parentOperator)) {
                result.expectedType = 'Boolean';
            } else if (['|', '&', '~'].includes(parentOperator)) {
                if (isRightOperand && node.parent.leftOperand) {
                    const leftType = this.getSemanticNodeType(node.parent.leftOperand);
                    result.expectedType = this.normalizeSetOperandExpectedType(leftType);
                }
            } else if (['>', '<', '>=', '<='].includes(parentOperator)) {
                // 比较操作符：如果左操作数是数值，右操作数也应该是数值
                if (isRightOperand && node.parent.leftOperand) {
                    const leftType = this.normalizeTypeName(this.getSemanticNodeType(node.parent.leftOperand));
                    if (leftType && this.isNumericType(leftType)) {
                        result.expectedType = leftType;
                    }
                }
            } else if (['==', '!='].includes(parentOperator)) {
                // 相等操作符：两个操作数类型应该匹配
                if (isRightOperand && node.parent.leftOperand) {
                    result.expectedType = this.getSemanticNodeType(node.parent.leftOperand);
                }
            } else if (['contains', 'startsWith', 'endsWith'].includes(parentOperator)) {
                if (isRightOperand && node.parent.leftOperand) {
                    const leftType = this.getSemanticNodeType(node.parent.leftOperand);
                    const normalizedLeft = this.normalizeTypeName(leftType);
                    if (normalizedLeft) {
                        if (this.isCollectionType(normalizedLeft)) {
                            const inner = this.getCollectionInnerType(normalizedLeft);
                            result.expectedType = this.normalizeTypeName(inner) || normalizedLeft;
                        } else if (this.isTextType(normalizedLeft)) {
                            result.expectedType = 'String';
                        } else {
                            result.expectedType = normalizedLeft;
                        }
                    }
                }
            } else if (parentOperator === '-') {
                if (isRightOperand && node.parent.leftOperand) {
                    const leftType = this.getSemanticNodeType(node.parent.leftOperand);
                    if (['Int32', 'Int64', 'Single', 'Double', 'Number'].includes(leftType)) {
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
                let expectedType = this.normalizeTypeName(p.typeName);
                if (expectedType === 'Object' && index > 0) {
                    const receiver = args[0];
                    const receiverType = receiver ? this.getSemanticNodeType(receiver) : null;
                    if (receiverType) {
                        if (this.isCollectionType(receiverType)) {
                            expectedType = this.normalizeTypeName(this.getCollectionInnerType(receiverType)) || expectedType;
                        } else if (this.isTextType(receiverType)) {
                            expectedType = 'String';
                        }
                    }
                }
                result.expectedType = expectedType;
            }
        } else if (node.parent && node.parent.type === 'listQuery') {
            if (node.parent.filterExpression === node) {
                result.expectedType = 'Boolean';
            }
        } else if (node.parent && node.parent.type === 'listTake') {
            if (node.parent.countExpression === node) {
                result.expectedType = 'Int32';
            }
        } else if (node.parent && node.parent.type === 'listSort') {
            if (node.parent.sortExpression === node) {
                const isEntity = this.isBusinessObjectType(result.actualType);
                const isCollection = this.isCollectionType(result.actualType);
                const isDictionary = typeof this.isDictionaryType === "function" && this.isDictionaryType(result.actualType);
                if (isEntity || isCollection || isDictionary) {
                    result.expectedType = 'NonEntity';
                }
            }
        } else if (node.parent && node.parent.type === 'listDistinct') {
            if (node.parent.keyExpression === node) {
                const isEntity = this.isBusinessObjectType(result.actualType);
                const isCollection = this.isCollectionType(result.actualType);
                const isDictionary = typeof this.isDictionaryType === "function" && this.isDictionaryType(result.actualType);
                if (isEntity || isCollection || isDictionary) {
                    result.expectedType = 'ScalarOrScalarList';
                }
            }
        } else if (node.parent && node.parent.type === 'listSelect') {
            if (node.parent.selectExpression === node) {
                result.expectedType = 'Object';
            }
        } else {
        }

        // 关键：如果存在外部预期类型且当前节点是根节点，则使用外部类型
        if (externalExpectedType && !hasEffectiveParent) {
            result.expectedType = externalExpectedType;
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
        return this.getSemanticNodeType(node) || "Object";
    }

    /**
     * 渲染DSL编辑器
     * @param {HTMLElement} container - 容器元素
     * @param {string} expression - 当前DSL表达式
     * @param {Function} onChange - 表达式变化回调
     */
    async render(container, expression = "", onChange = () => { }, options = {}) {
        const loader = container ? getBlockingLoader(container) : null;
        if (loader) {
            loader.show("正在加载DSL编辑器...");
        }
        try {
            await this.initialize();

            this.container = container;
            this.expression = expression;
            this.onChange = onChange;
            this.expectedType = options.expectedType || null;
            this.triggeringEventContext = options.triggeringEventContext || null;
            this.fixedRootContext = options.fixedRootContext || null;
            this.hideFavoritesToolbar = Boolean(options.hideFavoritesToolbar);
            const session = options.userSession || getUserSession();
            this.userId = options.userId || session?.userId || null;

            this.clearContainer(container);

            
            // 关键修复：如果表达式为空，则始终执行初始化流程
            if (!expression) {
                this.setParseStatus("empty", "");
                this.initializeWorkspace();
            } else {
                await this.loadExpressionWithParseApi(expression);
                this.renderEditor();
            }

            // 初次渲染后对外通知当前表达式
            this.emitExpressionChange(this.expression || "");
        } catch (error) {
            console.error("DslEditor: Render failed:", error);
            showErrorFromModule(this, `渲染DSL编辑器失败: ${error.message}`, container);
        } finally {
            if (loader) {
                loader.hide();
            }
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

    openPipelineFromCurrent(options = undefined) {
        return openPipelineFromCurrent(this, options);
    }

    syncPipelineContext() {
        return syncPipelineContext(this);
    }

    markPipelineContextDirty() {
        return markPipelineContextDirty(this);
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
        if (!raw) {
            this.initializeWorkspace();
            return;
        }

        const rootNode = this.buildAstFromExpression(raw);
        if (!rootNode) {
            this.initializeWorkspace();
            return;
        }

        this.applyParsedAst(rootNode, raw);
    }



    /**
     * 渲染编辑器界面
     */
    renderEditor() {
        this.ensureEditingPathConsistency();

        if (!this.container) {
            return;
        }

        if (!this.isPipelineInline) {
            this.syncPipelineContext();
        }

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
        if (!this.isPipelineInline) {
            renderMainExpressionUI(this, editorContainer);
        }

        // 渲染工作区（模块化）
        renderWorkspaceUI(this, editorContainer);
    }

    getFavoriteExpectedType() {
        return null;
    }

    async fetchFavorites(query = "", limit = this.favoriteState.limit || 6, page = 1) {
        return fetchFavoritesApi({
            userId: this.userId,
            query,
            limit,
            page
        });
    }

    async refreshFavorites(query = this.favoriteState.query, page = this.favoriteState.page || 1) {
        const normalizedQuery = String(query || "").trim();
        this.favoriteState.loading = true;
        this.favoriteState.error = null;
        this.favoriteState.query = normalizedQuery;
        this.favoriteState.page = Math.max(1, Number(page) || 1);

        try {
            const limit = this.favoriteState.limit || 6;
            let activePage = this.favoriteState.page;

            let result = await this.fetchFavorites(normalizedQuery, limit, activePage);
            const total = Math.max(0, Number(result.total) || 0);
            const totalPages = Math.max(1, Math.ceil(total / limit));

            if (total > 0 && activePage > totalPages) {
                activePage = totalPages;
                result = await this.fetchFavorites(normalizedQuery, limit, activePage);
            }

            this.favoriteState.items = result.items || [];
            this.favoriteState.total = Math.max(0, Number(result.total) || 0);
            this.favoriteState.totalPages = Math.max(1, Math.ceil(this.favoriteState.total / limit));
            this.favoriteState.page = activePage;

            if (normalizedQuery) {
                this.favoriteState.recentItems = [];
            } else {
                const recentResult = await this.fetchFavorites("", 24, 1);
                this.favoriteState.recentItems = (recentResult.items || [])
                    .filter(item => item.lastUsedAt || item.usageCount > 0)
                    .slice(0, 1);
            }
        } catch (error) {
            console.error("DslEditor: Failed to fetch favorites", error);
            this.favoriteState.error = error?.message || "收藏加载失败";
            this.favoriteState.items = [];
            this.favoriteState.recentItems = [];
            this.favoriteState.total = 0;
            this.favoriteState.totalPages = 1;
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

    replaceMainExpression(rootNode, options = {}) {
        if (!rootNode) {
            this.setParseStatus("empty", "");
            this.initializeWorkspace();
            return false;
        }

        const {
            expression = rootNode?.toExpression?.() || "",
            parseState = null,
            parseMessage = "",
            parseErrors = []
        } = options;

        this.selectedNode = null;
        this.virtualRoot.main = this.createEncapsulatedFrom(rootNode);
        this.workspaceAST = this.cloneAST(rootNode);
        this.astRoot = this.workspaceAST;
        this.expression = this.workspaceAST?.toExpression?.() || expression;
        this.normalizeLoadedAst();

        if (parseState) {
            this.setParseStatus(parseState, parseMessage, parseErrors);
        }

        this.enterMainEditMode();
        return true;
    }

    applyParsedAst(rootNode, expression) {
        this.replaceMainExpression(rootNode, {
            expression,
            parseState: "parsed",
            parseMessage: "解析成功"
        });
    }

    normalizeLoadedAst() {
        const normalize = (node) => {
            if (!node) return;
            this.maintainASTRelationships(node);
            refreshValueNodeReturnTypes(this, node);
            refreshCompleteness(node);
            this.maintainASTRelationships(node);
        };
        normalize(this.virtualRoot.main);
        normalize(this.workspaceAST);
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
        this.replaceMainExpression(rawNode, {
            expression,
            parseState: "fallback",
            parseMessage: message,
            parseErrors: errors
        });
    }

    syncMainWorkspaceContext() {
        this.contextOverride = null;
        this.slotExpectedType = typeof this.getExpectedTypeForSlot === "function"
            ? this.getExpectedTypeForSlot(this.virtualRoot, "main")
            : this.normalizeTypeName(this.expectedType);

        this.clearEditingPath();
        this.pushEditingPath({
            parentNode: this.virtualRoot,
            slot: "main",
            descriptor: "编辑 main"
        });

        const mainNode = this.virtualRoot?.main || null;
        const mainContent = mainNode?.metadata?.type === "encapsulated"
            ? mainNode.metadata?.innerAST
            : mainNode;
        const workspaceExpr = this.workspaceAST?.toExpression?.() || "";
        const mainExpr = mainContent?.toExpression?.() || "";
        const isWorkspaceSynced = (!mainContent && !this.workspaceAST)
            || (workspaceExpr && mainExpr && workspaceExpr === mainExpr);
        this.workspaceEditContext = isWorkspaceSynced
            ? { parentNode: this.virtualRoot, slot: "main", index: null }
            : null;
        this.selectedNode = mainNode?.metadata?.type === "encapsulated" ? mainNode : null;
    }

    collectAstChildren(node) {
        if (!node) return [];
        const children = [];
        if (node.type === "operator") {
            if (node.leftOperand) children.push(node.leftOperand);
            if (node.rightOperand) children.push(node.rightOperand);
        } else if (node.type === "functionCall") {
            (node.args || []).forEach((arg) => {
                if (arg) children.push(arg);
            });
        } else if (node.type === "listQuery") {
            if (node.source) children.push(node.source);
            if (node.filterExpression) children.push(node.filterExpression);
        } else if (node.type === "listSort") {
            if (node.source) children.push(node.source);
            if (node.sortExpression) children.push(node.sortExpression);
        } else if (node.type === "listTake") {
            if (node.source) children.push(node.source);
            if (node.countExpression) children.push(node.countExpression);
        } else if (node.type === "listDistinct") {
            if (node.source) children.push(node.source);
            if (node.keyExpression) children.push(node.keyExpression);
        } else if (node.type === "listSelect") {
            if (node.source) children.push(node.source);
            if (node.selectExpression) children.push(node.selectExpression);
        }
        if (node.metadata?.innerAST) {
            children.push(node.metadata.innerAST);
        }
        return children;
    }

    isNodeAttachedToMainAst(targetNode) {
        if (!targetNode) return false;
        if (targetNode === this.virtualRoot) return true;
        const mainNode = this.virtualRoot?.main || null;
        if (!mainNode) return false;
        if (targetNode === mainNode) return true;

        const stack = [mainNode];
        const visited = new Set();
        while (stack.length > 0) {
            const current = stack.pop();
            if (!current || visited.has(current)) continue;
            visited.add(current);
            if (current === targetNode) return true;
            const children = this.collectAstChildren(current);
            for (const child of children) {
                if (child && !visited.has(child)) {
                    stack.push(child);
                }
            }
        }
        return false;
    }

    isEditingPathEntryValid(entry) {
        if (!entry?.parentNode || !entry?.slot) return false;
        if (entry.parentNode === this.virtualRoot) {
            return entry.slot === "main";
        }
        return this.isNodeAttachedToMainAst(entry.parentNode);
    }

    ensureEditingPathConsistency() {
        if (this.isLightWorkspace) return;
        if (!this.virtualRoot?.main) return;
        const top = this.peekEditingPath ? this.peekEditingPath() : null;
        if (!this.isEditingPathEntryValid(top)) {
            this.syncMainWorkspaceContext();
        }
    }

    rebuildWorkspaceFromPath() {
        this.ensureEditingPathConsistency();
        const top = this.peekEditingPath ? this.peekEditingPath() : null;
        if (this.isEditingPathEntryValid(top)) {
            this.handleEnterEditModeForSlot(top.parentNode, top.slot, top.index, { preservePath: false });
            return;
        }
        if (this.virtualRoot?.main) {
            this.enterMainEditMode();
            return;
        }
        this.workspaceAST = null;
        this.astRoot = null;
        this.renderEditor();
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

    async deleteFavorite(favoriteId) {
        if (!this.userId || !favoriteId) return { success: false };
        const result = await deleteFavoriteApi(this.userId, favoriteId);
        if (!result?.success) {
            console.warn("DslEditor: Failed to delete favorite.", result?.status);
        }
        return result;
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
            expression
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

    async insertFavoriteExpression(expression) {
        const raw = (expression ?? "").trim();
        if (!raw) {
            this.showExpressionError("收藏表达式为空，无法插入。");
            return false;
        }

        this.setParseStatus("loading", "正在加载收藏表达式...");

        try {
            await this.loadExpressionWithParseApi(raw);
        } catch (error) {
            console.error("DslEditor: Failed to load favorite expression", error);
            this.showExpressionError(error?.message || "收藏表达式加载失败");
            return false;
        }

        const validation = this.validateExpressionForCompletion();
        if (!validation.isValid) {
            this.showExpressionError(validation.error);
        }
        return true;
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
        this.enterMainEditMode();
    }

    /**
     * 统一入口：进入主表达式编辑状态（合并预览/编辑）
     */
    enterMainEditMode() {
        this.contextOverride = null;
        this.slotExpectedType = typeof this.getExpectedTypeForSlot === 'function'
            ? this.getExpectedTypeForSlot(this.virtualRoot, 'main')
            : null;

        this.clearEditingPath();
        this.pushEditingPath({
            parentNode: this.virtualRoot,
            slot: 'main',
            descriptor: '编辑 main'
        });
        const highlightTarget = this.getHighlightTargetNode ? this.getHighlightTargetNode() : null;
        this.selectedNode = highlightTarget?.metadata?.type === 'encapsulated' ? highlightTarget : null;

        const mainNode = this.virtualRoot?.main || null;
        const contentToEdit = mainNode?.metadata?.type === 'encapsulated'
            ? mainNode.metadata.innerAST
            : mainNode;

        this.workspaceAST = this.cloneAST(contentToEdit);
        this.astRoot = this.workspaceAST;
        this.isExpressionCompleted = false;
        this.workspaceEditContext = {
            parentNode: this.virtualRoot,
            slot: 'main',
            index: null
        };

        this.updateExpressionFromAST({ skipRender: true });
        this.renderEditor();
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

    renderPipelineBlock(node, container, renderNodeFn, options = undefined) {
        return renderPipelineBlock(this, node, container, renderNodeFn, options);
    }

    renderFunctionCallNodeAST(node, container, options = undefined) {
        return renderFunctionCallNodeAST(this, node, container, options);
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
    renderEncapsulatedExpression(node, container, options = {}) {
        return renderEncapsulatedExpression(this, node, container, options);
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
        if (parent.type === 'listDistinct' && parent.keyExpression === subNode) {
            this.handleEnterEditModeForSlot(parent, 'keyExpression');
            return;
        }
        if (parent.type === 'listSelect' && parent.selectExpression === subNode) {
            this.handleEnterEditModeForSlot(parent, 'selectExpression');
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
            if (node.direction) {
                cloned.direction = node.direction;
                cloned.setDirection?.(node.direction);
            }
            return cloned;
        } else if (node.type === 'listTake') {
            const cloned = createListTakeNode(this.cloneAST(node.source));
            if (node.countExpression) {
                cloned.setCountExpression(this.cloneAST(node.countExpression));
            }
            if (node.strategy) {
                cloned.strategy = String(node.strategy || '').toLowerCase();
                cloned.setStrategy?.(node.strategy);
            }
            return cloned;
        } else if (node.type === 'listDistinct') {
            const cloned = createListDistinctNode(this.cloneAST(node.source));
            if (node.keyExpression) {
                cloned.setKeyExpression(this.cloneAST(node.keyExpression));
            }
            return cloned;
        } else if (node.type === 'listSelect') {
            const cloned = createListSelectNode(this.cloneAST(node.source));
            if (node.selectExpression) {
                cloned.setSelectExpression(this.cloneAST(node.selectExpression));
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

    renderPropertyInput(node, container) {
        return renderPropertyInput(this, node, container);
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
            // 清空子编辑器的表达式
            this.expression = "";
            this.emitExpressionChange("");
            // 重新渲染父编辑器以移除子编辑器焦点
            this.parentEditor.renderEditor();
        }
    }

    canGoBackFromWorkspace() {
        if (this.isSubEditor) return true;
        if (this.workspaceAST) return true;
        return Array.isArray(this.editingPath) && this.editingPath.length > 1;
    }

    /**
     * 退回上级节点 (AST重构版)
     */
    goBack() {
        const hasPathBack = Array.isArray(this.editingPath) && this.editingPath.length > 1;

        // 子编辑器且无可回退路径时，退回父编辑器
        if (this.isSubEditor && !this.workspaceAST && !hasPathBack) {
            this.exitSubEditor();
            return;
        }

        // 工作区为空但路径仍有上级：按路径回退
        if (!this.workspaceAST) {
            if (hasPathBack) {
                this.popEditingPath();
                this.rebuildWorkspaceFromPath();
                this.updateExpressionFromAST({ skipRender: true });
            }
            return;
        }


        // 重置表达式完成状态
        this.isExpressionCompleted = false;

        // 实现AST的撤回逻辑
        const backResult = this.performASTGoBack();

        if (backResult.success) {
            this.workspaceAST = backResult.newRoot;
            this.astRoot = this.workspaceAST;

            // 当前层已清空时，优先沿编辑路径回退到父层，避免卡死在空工作区
            if (!this.workspaceAST) {
                const canAscend = Array.isArray(this.editingPath) && this.editingPath.length > 1;
                if (canAscend) {
                    this.popEditingPath();
                    this.rebuildWorkspaceFromPath();
                    this.updateExpressionFromAST({ skipRender: true });
                    return;
                }
            }
        }


        this.updateExpressionFromAST({ skipRender: true });
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
            '枚举值': [],
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
                case 'enumValue':
                    groups['枚举值'].push(option);
                    break;
                case 'property':
                case 'propertyInput':
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
                case 'listDistinct':
                case 'listSelect':
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
        const actualType = this.resolveTypeAlias(typeName) || typeName;
        const expectedType = this.resolveTypeAlias(firstParam.typeName) || firstParam.typeName;

        // 1. 精确类型匹配
        if (expectedType === actualType) {
            return true;
        }

        // 2. 通用Object类型匹配
        if (expectedType === "Object") {
            return true;
        }

        // 3. 基础类型的层次匹配
        if (this.isBaseType(actualType) && this.isBaseType(expectedType)) {
            return this.areBaseTypesCompatible(actualType, expectedType);
        }

        // 4. 集合类型的匹配
        if (this.isCollectionType(actualType) && this.isCollectionType(expectedType)) {
            const sourceInner = this.getCollectionInnerType(actualType);
            const targetInner = this.getCollectionInnerType(expectedType);
            if (targetInner === "Object") {
                return true;
            }
            if (sourceInner === targetInner) {
                return true;
            }
            if (this.isNumericType(sourceInner) && this.isNumericType(targetInner)) {
                return true;
            }
            return false;
        }

        // 5. 继承关系匹配 (简化版)
        return this.isTypeInheritanceCompatible(actualType, expectedType);
    }


    /**
     * 添加节点 (AST重构版)
     */
    addNode(option) {
        if (option.type === "complete") {
            this.handleCompleteExpression();
            return;
        }

        const previousSelectedNode = this.selectedNode;

        // 统一逻辑入口
        if (!this.astRoot || (this.astRoot.type === 'value' && this.astRoot.metadata?.type === 'empty')) {
            this.selectedNode = null;
            this.createRootNode(option);
            return;
        }

        const changed = this.extendAST(option);
        if (!changed) {
            this.selectedNode = previousSelectedNode;
            return;
        }

        this.selectedNode = null;
        // 关键修复：统一在 addNode 的末尾触发状态更新和重新渲染
        this.triggerStateUpdate();
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

    }


    /**
     * 保存工作区（新架构）：将工作区内容写回父槽位或设置为主表达式
     */
    saveWorkspace() {

        if (!this.workspaceAST) {
            console.warn("DslEditor: saveWorkspace called with empty workspaceAST.");
            return;
        }

        finalizeDraftInputs(this, this.workspaceAST);

        if (findIncompleteNode(this.workspaceAST)) {
            this.showExpressionError("工作区表达式不完整，无法保存。");
            console.warn("DslEditor: saveWorkspace failed: workspaceAST is null or incomplete");
            return;
        }

        const astValidation = validateAST(this.workspaceAST);
        if (!astValidation?.isValid) {
            const msg = (astValidation?.errors || []).map(e => String(e)).join('\n');
            this.showExpressionError(msg || "表达式不合法，无法保存。");
            return;
        }

        // 统一保存逻辑：只存在槽位编辑模式
        if (this.editingPath && this.editingPath.length > 0) {
            this.saveCurrentLayer();
            return;
        }

        // 兼容：当尚未建立主表达式且路径为空时，允许直接提交为 main。
        if (!this.virtualRoot?.main) {
            const mainNode = this.workspaceAST.metadata?.type === 'encapsulated'
                ? this.cloneAST(this.workspaceAST)
                : this.createEncapsulatedFrom(this.workspaceAST);
            this.virtualRoot.main = mainNode;
            this.enterMainEditMode();
            this.updateExpressionFromAST({ skipRender: true });
            return;
        }

        console.warn("DslEditor: saveWorkspace aborted because editingPath is empty while main exists.");
        if (typeof this.rebuildWorkspaceFromPath === "function") {
            this.rebuildWorkspaceFromPath();
        } else {
            this.enterMainEditMode();
        }
        this.showExpressionError("编辑上下文已丢失，已回到主表达式，请确认后重新提交。");
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
        // 路径是权威。ascend 只负责触发基于路径的重建，避免工作区与路径脱节。
        if (typeof this.rebuildWorkspaceFromPath === "function") {
            this.rebuildWorkspaceFromPath();
            return;
        }

        if (parentNode?.type === "virtualRoot") {
            this.enterMainEditMode();
            return;
        }
        if (parentNode) {
            this.workspaceAST = this.cloneAST(parentNode);
            this.astRoot = this.workspaceAST;
            this.updateExpressionFromAST();
            this.renderEditor();
        }
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

        // 分析当前AST结构
        const astAnalysis = this.analyzeCurrentASTStructure();

        // 基于AST结构确定操作符添加策略
        const strategy = this.determineOperatorAdditionStrategy(astAnalysis, operator);

        // 执行操作符添加策略
        return this.executeOperatorAdditionStrategy(astAnalysis, operator, strategy);
    }

    /**
     * 分析当前AST结构
     */
    analyzeCurrentASTStructure() {

        // 关键修复：正确识别当前的编辑状态
        const currentExpression = this.getCurrentCompleteExpression();

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
            '~': 6,
            '+': 7,
            '-': 7,
            '*': 8,
            '/': 8,
            '\\': 8,
            '%': 8
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
        const targetNode = this.resolvePropertyExtensionTarget();
        if (!targetNode) {
            this.showExpressionError("无法添加属性：缺少有效的扩展目标。");
            return false;
        }

        const actualTarget = targetNode?.metadata?.type === 'encapsulated' && targetNode?.metadata?.innerAST
            ? targetNode.metadata.innerAST
            : targetNode;

        if (actualTarget && actualTarget.type === 'value' && actualTarget.metadata?.type === 'context') {
            const newPath = `${actualTarget.value}.${option.value}`;
            const newNode = createValueNode(newPath, option.returnType, {
                type: "property",
                contextObject: actualTarget.value,
                property: option.value,
                fullPath: newPath,
                displayName: option.label
            });

            this.replaceNodeInAST(actualTarget, newNode);
            if (this.astRoot === actualTarget) {
                this.astRoot = newNode;
                this.workspaceAST = newNode;
            }
            return true;
        } else if (actualTarget && actualTarget.type === 'value' && actualTarget.metadata?.type === 'property' && !actualTarget.metadata?.isDynamicMemberAccess) {
            const newPath = `${actualTarget.value}.${option.value}`;
            const newNode = createValueNode(newPath, option.returnType, {
                type: "property",
                fullPath: newPath,
                isNested: true,
                displayName: option.label
            });

            this.replaceNodeInAST(actualTarget, newNode);

            if (this.astRoot === actualTarget) {
                this.astRoot = newNode;
                this.workspaceAST = newNode;
            }
            return true;
        }

        const memberAccessNode = this.createDynamicMemberAccessNode(actualTarget, option);
        if (!memberAccessNode) {
            this.showExpressionError("无法添加属性：当前表达式不支持继续扩展。");
            return false;
        }

        this.replaceNodeInAST(targetNode, memberAccessNode);
        if (this.astRoot === targetNode || this.astRoot === actualTarget) {
            this.astRoot = memberAccessNode;
            this.workspaceAST = memberAccessNode;
        }
        return true;
    }

    resolvePropertyExtensionTarget() {
        if (!this.astRoot) return null;

        if (this.isLightWorkspace && this.lightWorkspaceFocus?.parentNode && this.lightWorkspaceFocus?.slot) {
            return this.getSlotNode(
                this.lightWorkspaceFocus.parentNode,
                this.lightWorkspaceFocus.slot,
                this.lightWorkspaceFocus.index
            ) || this.workspaceAST || this.astRoot;
        }

        const incompleteNode = findIncompleteNode(this.astRoot);
        if (incompleteNode) {
            const rightmostNode = findRightmostLeafNode(this.astRoot);
            return rightmostNode || this.astRoot;
        }

        return this.workspaceAST || this.astRoot;
    }

    formatExpressionForMemberAccess(node) {
        if (!node) return "";
        const rawExpression = node.toExpression?.() || node.value || "";
        if (!rawExpression) return "";
        if (node.type === "operator") {
            return `(${rawExpression})`;
        }
        return rawExpression;
    }

    createDynamicMemberAccessNode(baseNode, option) {
        if (!baseNode || !option?.value) {
            return null;
        }

        const baseExpression = this.formatExpressionForMemberAccess(baseNode);
        if (!baseExpression) {
            return null;
        }

        const value = `${baseExpression}.${option.value}`;
        return createValueNode(value, option.returnType || "Object", {
            type: "property",
            fullPath: value,
            baseExpression,
            property: option.value,
            displayName: option.label || option.value,
            isDynamicMemberAccess: true
        });
    }



    /**
     * 维护AST节点关系和类型信息
     */
    maintainASTRelationships(node, parent = null) {
        if (!node) return;

        const assignParents = (current, currentParent) => {
            if (!current) return;
            current.parent = currentParent;

            if (current.type === 'operator') {
                if (current.leftOperand) assignParents(current.leftOperand, current);
                if (current.rightOperand) assignParents(current.rightOperand, current);
            }

            if (current.type === 'functionCall') {
                (current.args || []).forEach((arg) => {
                    if (arg) assignParents(arg, current);
                });
            }

            if (current.type === 'listQuery') {
                if (current.source) assignParents(current.source, current);
                if (current.filterExpression) assignParents(current.filterExpression, current);
            }

            if (current.type === 'listSort') {
                if (current.source) assignParents(current.source, current);
                if (current.sortExpression) assignParents(current.sortExpression, current);
            }

            if (current.type === 'listTake') {
                if (current.source) assignParents(current.source, current);
                if (current.countExpression) assignParents(current.countExpression, current);
            }

            if (current.type === 'listDistinct') {
                if (current.source) assignParents(current.source, current);
                if (current.keyExpression) assignParents(current.keyExpression, current);
            }
            if (current.type === 'listSelect') {
                if (current.source) assignParents(current.source, current);
                if (current.selectExpression) assignParents(current.selectExpression, current);
            }

            if (current.metadata && current.metadata.innerAST) {
                // Keep innerAST connected to its encapsulated wrapper so clicks can resolve to the correct slot.
                assignParents(current.metadata.innerAST, current);
            }
        };

        const assignTypeMatch = (current, currentParent) => {
            if (!current) return;
            current.parent = currentParent;

            let externalExpectedType = null;
            if (current === this.virtualRoot.main) {
                externalExpectedType = this.expectedType;
            }
            current._typeMatchInfo = this.calculateNodeTypeMatch(current, externalExpectedType);


            if (current.type === 'operator') {
                if (current.leftOperand) assignTypeMatch(current.leftOperand, current);
                if (current.rightOperand) assignTypeMatch(current.rightOperand, current);
            }

            if (current.type === 'functionCall') {
                (current.args || []).forEach((arg) => {
                    if (arg) assignTypeMatch(arg, current);
                });
            }

            if (current.type === 'listQuery') {
                if (current.source) assignTypeMatch(current.source, current);
                if (current.filterExpression) assignTypeMatch(current.filterExpression, current);
            }

            if (current.type === 'listSort') {
                if (current.source) assignTypeMatch(current.source, current);
                if (current.sortExpression) assignTypeMatch(current.sortExpression, current);
            }

            if (current.type === 'listTake') {
                if (current.source) assignTypeMatch(current.source, current);
                if (current.countExpression) assignTypeMatch(current.countExpression, current);
            }

            if (current.type === 'listDistinct') {
                if (current.source) assignTypeMatch(current.source, current);
                if (current.keyExpression) assignTypeMatch(current.keyExpression, current);
            }
            if (current.type === 'listSelect') {
                if (current.source) assignTypeMatch(current.source, current);
                if (current.selectExpression) assignTypeMatch(current.selectExpression, current);
            }

            if (current.metadata && current.metadata.innerAST) {
                assignTypeMatch(current.metadata.innerAST, current);
            }
        };

        if (!parent) {
            assignParents(node, null);
            refreshValueNodeReturnTypes(this, node);
            refreshCompleteness(node);
            assignTypeMatch(node, null);
            return;
        }

        assignParents(node, parent);
        refreshValueNodeReturnTypes(this, node);
        refreshCompleteness(node);
        assignTypeMatch(node, parent);
    }

    /**
     * 从AST更新表达式
     */
    updateExpressionFromAST(options = {}) {
        this.assertAstAlias();
        const { skipRender = false } = options;

        // 关键修复：在更新表达式时维护AST关系
        if (this.workspaceAST) {
            try {
                this.maintainASTRelationships(this.workspaceAST);
            } catch (error) {
                console.error("DslEditor: Error maintaining AST relationships for workspaceAST:", error);
            }
        }
        const top = this.peekEditingPath ? this.peekEditingPath() : null;
        const isEditingMain = top?.parentNode === this.virtualRoot && top?.slot === "main";
        if (isEditingMain && this.workspaceAST) {
            this.virtualRoot.main = this.createEncapsulatedFrom(this.workspaceAST);
        }
        if (this.virtualRoot.main) {
            try {
                this.maintainASTRelationships(this.virtualRoot.main);
            } catch (error) {
                console.error("DslEditor: Error maintaining AST relationships for mainExpressionAST:", error);
            }
        }

        this.expression = this.computeCurrentExpression() || "";

        // 触发状态更新事件（用于实时监控）
        this.triggerStateUpdate({ skipRender });
        // 对外同步表达式（去重）
        this.emitExpressionChange(this.expression);
    }

    /**
     * 触发状态更新事件
     */
    triggerStateUpdate(options = {}) {
        const { skipRender = false } = options;
        // 详细状态日志（仅在需要时输出）
        if (window.DEBUG_DSL) {
        }

        // Avoid re-render while inline inputs are typing to keep focus stable.
        if (!skipRender) {
            // 关键修复：状态更新时重新渲染编辑器
            this.renderEditor();
        }

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

    computeCurrentExpression() {
        const mainNode = this.virtualRoot?.main ?? null;
        if (!mainNode && this.workspaceAST) {
            return this.workspaceAST.toExpression?.() || "";
        }

        const top = this.peekEditingPath ? this.peekEditingPath() : null;
        const isEditingMain = top?.parentNode?.type === 'virtualRoot' && top?.slot === 'main';
        if (!top?.parentNode) {
            const fallbackNode = mainNode?.metadata?.innerAST ?? mainNode;
            return fallbackNode?.toExpression?.() || "";
        }

        if (isEditingMain) {
            const fallbackNode = mainNode?.metadata?.innerAST ?? mainNode;
            const expressionNode = this.workspaceAST ?? fallbackNode;
            return expressionNode?.toExpression?.() || "";
        }

        if (!this.workspaceAST || !mainNode) {
            const fallbackNode = mainNode?.metadata?.innerAST ?? mainNode;
            return fallbackNode?.toExpression?.() || "";
        }

        const parentNode = top.parentNode;
        const slot = top.slot;
        const index = top.index;
        const originalNode = this.getSlotNode(parentNode, slot, index);
        const injectedNode = this.workspaceAST.metadata?.type === 'encapsulated'
            ? this.workspaceAST
            : this.createEncapsulatedFrom(this.workspaceAST);

        const updated = this.setSlotNode(parentNode, slot, index, injectedNode);
        try {
            const expressionSource = mainNode.metadata?.innerAST ?? mainNode;
            return expressionSource?.toExpression?.() || "";
        } finally {
            if (updated) {
                this.setSlotNode(parentNode, slot, index, originalNode);
            }
        }
    }

    getSlotNode(parentNode, slot, index = null) {
        if (!parentNode || !slot) return null;
        if (parentNode.type === 'virtualRoot' && slot === 'main') return parentNode.main || null;
        if (parentNode.type === 'value' && slot === 'innerAST') return parentNode.metadata?.innerAST || null;
        if (parentNode.type === 'operator') {
            if (slot === 'left') return parentNode.leftOperand || null;
            if (slot === 'right') return parentNode.rightOperand || null;
        }
        if (parentNode.type === 'functionCall' && slot === 'arg') {
            const argIndex = typeof index === 'number' ? index : 0;
            return (parentNode.args || [])[argIndex] || null;
        }
        if (
            (parentNode.type === 'listQuery'
                || parentNode.type === 'listSort'
                || parentNode.type === 'listTake'
                || parentNode.type === 'listDistinct'
                || parentNode.type === 'listSelect')
            && slot === 'source'
        ) {
            return parentNode.source || null;
        }
        if (parentNode.type === 'listQuery' && slot === 'filterExpression') return parentNode.filterExpression || null;
        if (parentNode.type === 'listSort' && slot === 'sortExpression') return parentNode.sortExpression || null;
        if (parentNode.type === 'listTake' && slot === 'countExpression') return parentNode.countExpression || null;
        if (parentNode.type === 'listDistinct' && slot === 'keyExpression') return parentNode.keyExpression || null;
        if (parentNode.type === 'listSelect' && slot === 'selectExpression') return parentNode.selectExpression || null;
        return null;
    }

    setSlotNode(parentNode, slot, index, node) {
        if (!parentNode || !slot) return false;
        if (parentNode.type === 'virtualRoot' && slot === 'main') {
            parentNode.main = node;
            return true;
        }
        if (parentNode.type === 'value' && slot === 'innerAST') {
            if (!parentNode.metadata || typeof parentNode.metadata !== 'object') {
                parentNode.metadata = {};
            }
            parentNode.metadata.innerAST = node || null;
            if (node) node.parent = parentNode;
            parentNode.updateCompleteness?.();
            return true;
        }
        if (parentNode.type === 'operator') {
            if (slot === 'left') {
                if (typeof parentNode.setLeftOperand === 'function') {
                    parentNode.setLeftOperand(node);
                } else {
                    parentNode.leftOperand = node;
                    if (node) node.parent = parentNode;
                    parentNode.updateCompleteness?.();
                }
                return true;
            }
            if (slot === 'right') {
                if (typeof parentNode.setRightOperand === 'function') {
                    parentNode.setRightOperand(node);
                } else {
                    parentNode.rightOperand = node;
                    if (node) node.parent = parentNode;
                    parentNode.updateCompleteness?.();
                }
                return true;
            }
        }
        if (parentNode.type === 'functionCall' && slot === 'arg') {
            const argIndex = typeof index === 'number' ? index : 0;
            if (typeof parentNode.setArg === 'function') {
                parentNode.setArg(argIndex, node);
                return true;
            }
            return false;
        }
        if (
            (parentNode.type === 'listQuery'
                || parentNode.type === 'listSort'
                || parentNode.type === 'listTake'
                || parentNode.type === 'listDistinct'
                || parentNode.type === 'listSelect')
            && slot === 'source'
        ) {
            parentNode.source = node || null;
            if (node) node.parent = parentNode;
            parentNode.updateCompleteness?.();
            return true;
        }
        if (parentNode.type === 'listQuery' && slot === 'filterExpression') {
            if (typeof parentNode.setFilterExpression === 'function') {
                parentNode.setFilterExpression(node);
            } else {
                parentNode.filterExpression = node || null;
                if (node) node.parent = parentNode;
                parentNode.updateCompleteness?.();
            }
            return true;
        }
        if (parentNode.type === 'listSort' && slot === 'sortExpression') {
            if (typeof parentNode.setSortExpression === 'function') {
                parentNode.setSortExpression(node);
            } else {
                parentNode.sortExpression = node || null;
                if (node) node.parent = parentNode;
                parentNode.updateCompleteness?.();
            }
            return true;
        }
        if (parentNode.type === 'listTake' && slot === 'countExpression') {
            if (typeof parentNode.setCountExpression === 'function') {
                parentNode.setCountExpression(node);
            } else {
                parentNode.countExpression = node || null;
                if (node) node.parent = parentNode;
                parentNode.updateCompleteness?.();
            }
            return true;
        }
        if (parentNode.type === 'listDistinct' && slot === 'keyExpression') {
            if (typeof parentNode.setKeyExpression === 'function') {
                parentNode.setKeyExpression(node);
            } else {
                parentNode.keyExpression = node || null;
                if (node) node.parent = parentNode;
                parentNode.updateCompleteness?.();
            }
            return true;
        }
        if (parentNode.type === 'listSelect' && slot === 'selectExpression') {
            if (typeof parentNode.setSelectExpression === 'function') {
                parentNode.setSelectExpression(node);
            } else {
                parentNode.selectExpression = node || null;
                if (node) node.parent = parentNode;
                parentNode.updateCompleteness?.();
            }
            return true;
        }
        return false;
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


        // 首先尝试直接匹配类型名
        let result = this.metadata.dsl.typeMetadata[typeName];
        if (result) {
            return result;
        }

        const aliasTypeMap = {
            IGameEvent: 'GameEventBase'
        };
        const aliasTypeName = aliasTypeMap[typeName];
        if (aliasTypeName) {
            result = this.metadata.dsl.typeMetadata[aliasTypeName];
            if (result) {
                return result;
            }
        }

        // 处理泛型类型，如 List<CardInstance> → CardInstance
        if (typeName.includes("<") && typeName.includes(">")) {
            const innerType = typeName.split("<")[1].replace(">", "");
            result = this.metadata.dsl.typeMetadata[innerType];
            return result;
        }

        return null;
    }

    getEnumOptionSources() {
        const sources = new Set();
        const metaSources = this.metadata?.enumOptionSources || this.metadata?.EnumOptionSources || [];
        (metaSources || []).forEach((name) => {
            if (name) sources.add(name);
        });

        const enumTypes = this.metadata?.dsl?.enumTypes || this.metadata?.dsl?.EnumTypes || [];
        (enumTypes || []).forEach((entry) => {
            const name = entry?.name ?? entry?.Name;
            if (name) sources.add(name);
        });

        const relationTypes = this.metadata?.relationTypes || this.metadata?.RelationTypes || [];
        if (Array.isArray(relationTypes) && relationTypes.length > 0) {
            sources.add("TriggerRelationType");
        }

        return Array.from(sources);
    }

    resolveEnumSourceName(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return null;

        const sources = this.getEnumOptionSources();
        const directMatch = sources.find(name =>
            String(name).toLowerCase() === normalized.toLowerCase()
        );
        if (directMatch) return directMatch;

        const aliasMap = {
            Rarity: "CardRarity",
            Craft: "CardClass"
        };
        const alias = aliasMap[normalized];
        if (!alias) return normalized;

        const aliasMatch = sources.find(name =>
            String(name).toLowerCase() === String(alias).toLowerCase()
        );
        return aliasMatch || alias;
    }

    seedEnumLocalizationFromRelationTypes() {
        const relationTypes = this.metadata?.relationTypes || this.metadata?.RelationTypes || [];
        if (!Array.isArray(relationTypes) || relationTypes.length === 0) return;
        if (this.enumLocalizationCache.has("TriggerRelationType")) return;

        const map = new Map();
        relationTypes.forEach((item) => {
            const value = item?.value ?? item?.Value;
            if (value === undefined || value === null) return;
            const label = item?.displayName ?? item?.DisplayName ?? String(value);
            map.set(String(value), label);
        });
        this.enumLocalizationCache.set("TriggerRelationType", map);
    }

    async ensureEnumLocalization(enumName) {
        if (!enumName) return null;
        if (this.enumLocalizationCache.has(enumName)) {
            return this.enumLocalizationCache.get(enumName);
        }
        if (this.enumLocalizationRequests.has(enumName)) {
            return this.enumLocalizationRequests.get(enumName);
        }

        const request = ensureSharedEnumLocalization(enumName)
            .then(({ raw }) => {
                const entries = raw ? Object.entries(raw) : [];
                const map = new Map(entries.map(([value, label]) => [value, label]));
                this.enumLocalizationCache.set(enumName, map);
                return map;
            })
            .catch((error) => {
                console.warn(`DslEditor: Failed to load enum localization for ${enumName}:`, error);
                const empty = new Map();
                this.enumLocalizationCache.set(enumName, empty);
                return empty;
            })
            .finally(() => {
                this.enumLocalizationRequests.delete(enumName);
            });

        this.enumLocalizationRequests.set(enumName, request);
        return request;
    }

    async preloadEnumLocalizations() {
        this.seedEnumLocalizationFromRelationTypes();

        const sources = this.getEnumOptionSources();
        if (!sources.length) return;
        await Promise.all(sources.map((name) => this.ensureEnumLocalization(name)));
    }

    getEnumValueOptions(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized || !this.isEnumType(normalized)) return [];

        const enumSource = this.resolveEnumSourceName(normalized);
        const labels = enumSource ? this.enumLocalizationCache.get(enumSource) : null;
        const values = this.getEnumValues(normalized);
        if (!values.length) return [];

        return values.map((value) => {
            const label = labels?.get(value) || value;
            return {
                type: "enumValue",
                value,
                label,
                description: label !== value ? value : "",
                returnType: normalized,
                enumType: normalized,
                displayValue: label
            };
        });
    }

    normalizeEnumToken(value) {
        if (value === null || value === undefined) return "";
        const raw = String(value).trim();
        if (!raw) return "";
        return raw.replace(/^['"]|['"]$/g, "");
    }

    getEnumLabelForValue(typeName, value) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return null;
        const enumSource = this.resolveEnumSourceName(normalized);
        const labels = enumSource ? this.enumLocalizationCache.get(enumSource) : null;
        if (!labels) return null;
        return labels.get(value) || null;
    }

    getEnumLabelReverseMap(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return null;
        const enumSource = this.resolveEnumSourceName(normalized);
        const labels = enumSource ? this.enumLocalizationCache.get(enumSource) : null;
        if (!labels) return null;

        const reverse = new Map();
        const duplicates = new Set();
        labels.forEach((label, value) => {
            const key = this.normalizeEnumToken(label).toLowerCase();
            if (!key) return;
            if (reverse.has(key)) {
                duplicates.add(key);
                return;
            }
            reverse.set(key, value);
        });
        duplicates.forEach(key => reverse.delete(key));
        return reverse;
    }

    resolveEnumValueFromInput(typeName, input) {
        const normalized = this.normalizeTypeName(typeName);
        const token = this.normalizeEnumToken(input);
        if (!normalized || !token) return token;

        const values = this.getEnumValues(normalized);
        const direct = values.find(v => String(v).toLowerCase() === token.toLowerCase());
        if (direct) return direct;

        const reverse = this.getEnumLabelReverseMap(normalized);
        const mapped = reverse?.get(token.toLowerCase());
        if (mapped) return mapped;

        return token;
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
            normalized.startsWith("IReadOnlyList<") ||
            normalized.startsWith("IReadOnlyCollection<") ||
            normalized.startsWith("ICollection<") ||
            normalized === "List" ||
            normalized === "IEnumerable" ||
            normalized === "IReadOnlyList" ||
            normalized === "IReadOnlyCollection" ||
            normalized === "ICollection"
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

    isDictionaryType(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return false;
        return (
            normalized.startsWith("Dictionary<") ||
            normalized.startsWith("IDictionary<") ||
            normalized.startsWith("IReadOnlyDictionary<")
        );
    }

    getDictionaryValueType(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!this.isDictionaryType(normalized)) {
            return null;
        }
        const match = normalized.match(/<(.*)>/);
        if (!match) return "Object";
        const parts = match[1].split(",").map(part => part.trim());
        return parts[1] || "Object";
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
        const a = this.resolveTypeAlias(actualType);
        const e = this.resolveTypeAlias(expectedType);
        if (!a || !e) {
            return false; // 如果任一类型为空，则不兼容
        }
        if (e === 'ScalarOrScalarList') {
            if (this.isDictionaryType(a) || this.isBusinessObjectType(a)) {
                return false;
            }
            if (this.isCollectionType(a)) {
                const innerType = this.getCollectionInnerType(a);
                if (!innerType) {
                    return false;
                }
                return !this.isDictionaryType(innerType) && !this.isBusinessObjectType(innerType) && !this.isCollectionType(innerType);
            }
            return true;
        }
        if (e === 'Object') {
            return true; // Object 作为通配类型
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
            if (ei === 'Object' || ai === ei) {
                return true;
            }
            return this.isNumericType(ai) && this.isNumericType(ei);
        }

        // 允许单个元素匹配列表期望类型（后端会将单个对象包装为列表）
        if (!this.isCollectionType(a) && this.isCollectionType(e)) {
            if (a.toLowerCase() === 'string') {
                return false;
            }
            const ei = this.getCollectionInnerType(e);
            return ei === 'Object' || a === ei;
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
        const normalized = this.normalizeTypeName(typeName);
        const numericTypes = ['Int32', 'Int64', 'Single', 'Double', 'Decimal', 'Number'];
        return numericTypes.includes(normalized);
    }


    /**
     * 检查是否为业务对象类型（可以进行比较操作的对象类型）
     */
    isBusinessObjectType(typeName) {
        const normalized = this.resolveTypeAlias(typeName) || typeName;
        const businessObjectTypes = [
            'PlayerState',      // 玩家状态
            'CardInstance',     // 卡牌实例
            'CardDefinition',   // 卡牌定义
            'Ability',          // 能力实例
            'IAbility',         // 能力接口
            'EffectInstance',   // 效果实例
            'EventInstance'     // 事件实例
        ];
        return businessObjectTypes.includes(normalized);
    }

    isEnumType(typeName) {
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return false;

        const enumTypes = this.metadata?.dsl?.enumTypes || this.metadata?.dsl?.EnumTypes || [];
        if (Array.isArray(enumTypes) && enumTypes.some(e =>
            String(e.name ?? e.Name ?? '').toLowerCase() === normalized.toLowerCase()
        )) {
            return true;
        }

        const enumSource = this.resolveEnumSourceName(normalized);
        if (!enumSource) return false;

        const sources = this.getEnumOptionSources();
        if (sources.some(name => String(name).toLowerCase() === enumSource.toLowerCase())) {
            return true;
        }

        return this.enumLocalizationCache.has(enumSource);
    }

    /**
     * 检查是否为文本类型（包括枚举和GUID）
     */
    isTextType(typeName) {
        const textTypes = ['String', 'Guid', 'CardType', 'Rarity', 'Craft', 'CardKeyword', 'CardRarity', 'CardClass', 'AbilityKeyword'];
        if (textTypes.includes(typeName)) return true;
        return this.isEnumType(typeName);
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
        const normalized = this.normalizeTypeName(typeName);
        if (!normalized) return [];

        const enumTypes = this.metadata?.dsl?.enumTypes || this.metadata?.dsl?.EnumTypes || [];
        const enumType = (enumTypes || []).find(e =>
            String(e.name ?? e.Name ?? '').toLowerCase() === normalized.toLowerCase()
        );
        if (enumType?.values?.length) {
            return enumType.values;
        }

        const enumSource = this.resolveEnumSourceName(normalized);
        const cached = enumSource ? this.enumLocalizationCache.get(enumSource) : null;
        if (cached) {
            return Array.from(cached.keys());
        }

        return [];
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
            .filter(f => {
                const functionName = String(f?.name ?? f?.Name ?? "").toLowerCase();
                return functionName === "contains" || functionName === "isempty";
            })
            .map(f => ({
                type: "function",
                value: f.name ?? f.Name,
                label: this.getReadableFunctionName(f.name ?? f.Name),
                description: f.description,
                meta: f
            }));
    }

    /**
     * 获取集合类型的函数
     */
    getCollectionFunctions(typeName = null) {
        const functions = this.metadata?.dsl?.functions || [];
        const collectionFunctionNames = new Set(['count', 'sum', 'min', 'max', 'contains', 'isempty', 'bytype', 'frequency', 'find']);

        return functions
            .filter(f => collectionFunctionNames.has(String(f?.name ?? f?.Name ?? "").toLowerCase()))
            .filter(f => !typeName || this.isFunctionCompatibleWithType(f, typeName))
            .map(f => ({
                type: "function",
                value: f.name ?? f.Name,
                label: this.getReadableFunctionName(f.name ?? f.Name),
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
            label: "查询 (.query)",
            description: "查询集合元素"
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
     * 格式化枚举值用于表达式
     */
    formatEnumLiteralValue(value) {
        if (value === null || value === undefined) return "\"\"";
        const raw = String(value);
        if ((raw.startsWith('"') && raw.endsWith('"')) ||
            (raw.startsWith("'") && raw.endsWith("'"))) {
            return raw;
        }
        return `"${raw}"`;
    }

    /**
     * 格式化静态值用于表达式 (根据智能推断的类型)
     */
    formatStaticValue(value, inferredType) {
        if (!value) return '""'; // 空值默认为空字符串

        const trimmedValue = String(value).trim();
        if (this.isListLiteral(trimmedValue)) {
            return trimmedValue;
        }

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

        const trimmedValue = String(value).trim();
        if (this.isListLiteral(trimmedValue)) {
            const items = this.parseListLiteralItems(trimmedValue);
            if (items.length > 0 && items.every(item => /^-?\d+$/.test(item))) {
                return "List<Int32>";
            }
            return "List<String>";
        }

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

    isListLiteral(value) {
        const trimmed = String(value ?? "").trim();
        if (!trimmed.startsWith('[') || !trimmed.endsWith(']')) return false;
        // Minimal shape check: [] or [a,b]
        return trimmed.length >= 2;
    }

    parseListLiteralItems(value) {
        const trimmed = String(value ?? "").trim();
        if (!this.isListLiteral(trimmed)) return [];
        const inner = trimmed.slice(1, -1).trim();
        if (!inner) return [];
        return inner.split(",").map(part => {
            const token = part.trim();
            if ((token.startsWith('"') && token.endsWith('"')) || (token.startsWith("'") && token.endsWith("'"))) {
                return token.slice(1, -1).trim();
            }
            return token;
        }).filter(part => part.length > 0);
    }

    /**
     * 获取对象类型的属性
     */
    getObjectProperties(typeName) {
        if (this.isDictionaryType(typeName)) {
            return [{
                type: "propertyInput",
                value: "input",
                label: "输入键名",
                description: "输入字典键以读取对应值",
                returnType: this.getDictionaryValueType(typeName) || "Object"
            }];
        }

        const normalizedType = this.normalizeTypeName(typeName) || typeName;
        const typeMetadata = this.getTypeMetadata(normalizedType);
        const baseProperties = (typeMetadata?.properties || []).map(prop => ({
            type: "property",
            value: prop.name,
            label: prop.displayName || prop.name,
            description: prop.description || `${this.getDisplayTypeName(normalizedType)}的${prop.name}属性`,
            returnType: prop.typeName
        }));

        const isEventBaseType = normalizedType === "IGameEvent" || normalizedType === "GameEventBase";
        const eventTypeHint = this.contextOverride?.name === "self"
            ? this.contextOverride?.eventType
            : (this.fixedRootContext?.name === "self" ? this.fixedRootContext?.eventType : null);

        if (!isEventBaseType || !eventTypeHint) {
            return baseProperties;
        }

        const eventMeta = (this.metadata?.events || []).find(ev =>
            String(ev.eventType ?? ev.EventType ?? "").toLowerCase() === String(eventTypeHint).toLowerCase()
        );
        if (!eventMeta) {
            return baseProperties;
        }

        const params = eventMeta.parameters || eventMeta.Parameters || [];
        const eventProperties = params.map(param => {
            const propertyName = param.propertyName || param.PropertyName || param.name;
            const typeNameFromMeta = this.normalizeTypeName(param.typeName || param.TypeName || param.type) || "Object";
            const scope = String(param.scope || param.Scope || "both").toLowerCase();
            const scopeLabel = scope === "input" ? "输入" : scope === "output" ? "输出" : "输入/输出";
            return {
                type: "property",
                value: propertyName,
                label: param.displayName || param.DisplayName || propertyName,
                description: `${param.description || param.Description || "事件参数"}（${scopeLabel}）`,
                returnType: typeNameFromMeta
            };
        });

        const dedupe = new Set();
        const merged = [...eventProperties, ...baseProperties].filter(option => {
            const key = String(option.value || "").toLowerCase();
            if (!key || dedupe.has(key)) {
                return false;
            }
            dedupe.add(key);
            return true;
        });

        return merged;
    }

    /**
     * 判断是否应该使用平级结构而不是嵌套结构
     */

    /**
     * 清空工作区（新设计）
     */
    clearWorkspace(preserveMainExpression = false) {

        if (!preserveMainExpression) {
            this.virtualRoot.main = null;
        }

        if (this.isLightWorkspace) {
            this.clearLightWorkspaceFocus();
        }

        // 重置工作区到初始状态
        this.initializeWorkspace();

        // 更新表达式和UI
        this.updateExpressionFromAST();
        this.renderEditor();

    }
    /**
     * 统一的进入编辑模式的入口
     * @param {ASTNode} parentNode - 要编辑的槽位的父节点
     * @param {'left'|'right'|'innerAST'|'filterExpression'|'sortExpression'|'countExpression'} slot - 要编辑的槽位
     */
    handleEnterEditModeForSlot(parentNode, slot, index = null, options = undefined) {
        return handleEnterEditModeForSlot(this, parentNode, slot, index, options);
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

        if (parentNode.type === 'listDistinct' && parentNode.keyExpression === node) {
            return { slot: 'keyExpression', index: null };
        }
        if (parentNode.type === 'listSelect' && parentNode.selectExpression === node) {
            return { slot: 'selectExpression', index: null };
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
