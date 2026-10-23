import { createValueNode, validateAST, findIncompleteNode, getSemanticNodeType as getCoreSemanticNodeType } from "./core.js";
import { PIPELINE_STAGE_META, isListOperationNode, extractListPipeline, rebuildListChainFromStages, createStageNode } from "./pipeline.js";
import { finalizeDraftInputs } from "./ast-normalize.js";
import { resolveEventTypeHintFromSource } from "./event-translation.js";

export function createPipelineEditState() {
    return {
        node: null,
        slot: null,
        editor: null,
        draftExpression: "",
        draftCountValue: ""
    };
}

function createPipelineContextState() {
    return {
        pipelineOpen: false,
        pipelineOpenSource: null,
        pipelineOpenMode: null,
        pipelineEditState: createPipelineEditState()
    };
}

function ensurePipelineContextInfra(editor) {
    if (!editor.pipelineContextStore) {
        editor.pipelineContextStore = new Map();
    }
    if (!editor.pipelineNodeIds) {
        editor.pipelineNodeIds = new WeakMap();
    }
    if (!Number.isFinite(editor.pipelineNodeIdSeed)) {
        editor.pipelineNodeIdSeed = 1;
    }
}

function getStableNodeId(editor, node) {
    if (!node || (typeof node !== "object" && typeof node !== "function")) return "none";
    let nodeId = editor.pipelineNodeIds.get(node);
    if (!nodeId) {
        nodeId = `n${editor.pipelineNodeIdSeed++}`;
        editor.pipelineNodeIds.set(node, nodeId);
    }
    return nodeId;
}

function getCurrentPipelineContextKey(editor) {
    const top = typeof editor.peekEditingPath === "function" ? editor.peekEditingPath() : null;
    if (top?.parentNode && top?.slot) {
        const parentId = getStableNodeId(editor, top.parentNode);
        const indexPart = typeof top.index === "number" ? String(top.index) : "";
        return `path:${parentId}:${top.slot}:${indexPart}`;
    }

    if (editor.isLightWorkspace && editor.lightWorkspaceFocus?.parentNode && editor.lightWorkspaceFocus?.slot) {
        const focus = editor.lightWorkspaceFocus;
        const parentId = getStableNodeId(editor, focus.parentNode);
        const indexPart = typeof focus.index === "number" ? String(focus.index) : "";
        return `light:${parentId}:${focus.slot}:${indexPart}`;
    }

    const rootNode = editor.virtualRoot?.main || editor.workspaceAST || null;
    return `root:${getStableNodeId(editor, rootNode)}`;
}

export function markPipelineContextDirty(editor) {
    if (editor.isPipelineInline) return;
    ensurePipelineContextInfra(editor);
    if (!editor.pipelineActiveContextKey) {
        syncPipelineContext(editor);
    }
    if (!editor.pipelineActiveContextKey) return;
    editor.pipelineContextStore.set(editor.pipelineActiveContextKey, {
        pipelineOpen: Boolean(editor.pipelineOpen),
        pipelineOpenSource: editor.pipelineOpenSource || null,
        pipelineOpenMode: editor.pipelineOpenMode || null,
        pipelineEditState: editor.pipelineEditState || createPipelineEditState()
    });
}

export function syncPipelineContext(editor) {
    if (editor.isPipelineInline) return null;
    ensurePipelineContextInfra(editor);

    const nextKey = getCurrentPipelineContextKey(editor);
    const currentKey = editor.pipelineActiveContextKey;

    if (currentKey && currentKey !== nextKey) {
        markPipelineContextDirty(editor);
    }

    if (currentKey === nextKey) {
        return nextKey;
    }

    // 需求：工作区加载时默认折叠管线，仅在用户点击管线入口后展开
    const collapsedState = createPipelineContextState();
    editor.pipelineOpen = false;
    editor.pipelineOpenSource = null;
    editor.pipelineOpenMode = null;
    editor.pipelineEditState = collapsedState.pipelineEditState;
    editor.pipelineContextStore.set(nextKey, collapsedState);
    editor.pipelineActiveContextKey = nextKey;
    return nextKey;
}

export function unwrapEncapsulatedNode(editor, node) {
    if (!node) return null;
    if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
        return node.metadata.innerAST;
    }
    return node;
}

function getSemanticType(editor, node) {
    if (!node) return null;
    if (typeof editor?.getSemanticNodeType === "function") {
        return editor.getSemanticNodeType(node);
    }
    return getCoreSemanticNodeType(node);
}

function getLightFocusSlotNode(editor) {
    const focus = editor?.lightWorkspaceFocus;
    if (!focus?.parentNode || !focus?.slot) return null;
    const { parentNode, slot, index } = focus;
    if (parentNode.type === "virtualRoot" && slot === "main") return parentNode.main || null;
    if (parentNode.type === "operator") {
        return slot === "left"
            ? parentNode.leftOperand || null
            : slot === "right"
                ? parentNode.rightOperand || null
                : null;
    }
    if (parentNode.type === "functionCall" && slot === "arg") {
        const argIndex = typeof index === "number" ? index : 0;
        return (parentNode.args || [])[argIndex] || null;
    }
    if (parentNode.type === "listQuery" && slot === "filterExpression") return parentNode.filterExpression || null;
    if (parentNode.type === "listSort" && slot === "sortExpression") return parentNode.sortExpression || null;
    if (parentNode.type === "listTake" && slot === "countExpression") return parentNode.countExpression || null;
    if (parentNode.type === "listDistinct" && slot === "keyExpression") return parentNode.keyExpression || null;
    if (parentNode.type === "listSelect" && slot === "selectExpression") return parentNode.selectExpression || null;
    return null;
}

function canStartPipelineFromNode(editor, node) {
    if (!node) return false;
    const nodeType = getSemanticType(editor, node);
    if (!nodeType) return false;
    if (typeof editor.canOpenPipelineOnType === "function") {
        return editor.canOpenPipelineOnType(nodeType);
    }
    if (editor.isCollectionType(nodeType)) return true;
    return typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(nodeType);
}

function findPipelineTargetLocation(editor, root, requestedSource) {
    if (!root || !requestedSource) return null;
    const visited = new Set();
    let found = null;

    const matches = (node) => {
        if (!node) return false;
        return node === requestedSource;
    };

    const visit = (node, parentNode = null, slot = null, index = null) => {
        if (!node || typeof node !== "object" || found) return;
        if (visited.has(node)) return;
        visited.add(node);

        if (matches(node) && canStartPipelineFromNode(editor, node)) {
            found = { node, parentNode, slot, index };
            return;
        }

        if (node.type === "value" && node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
            visit(node.metadata.innerAST, node, "innerAST", null);
            return;
        }

        if (node.type === "operator") {
            visit(node.leftOperand, node, "left", null);
            visit(node.rightOperand, node, "right", null);
            return;
        }

        if (node.type === "functionCall") {
            (node.args || []).forEach((arg, argIndex) => visit(arg, node, "arg", argIndex));
            return;
        }

        if (node.type === "listQuery") {
            visit(node.source, node, "source", null);
            visit(node.filterExpression, node, "filterExpression", null);
            return;
        }

        if (node.type === "listSort") {
            visit(node.source, node, "source", null);
            visit(node.sortExpression, node, "sortExpression", null);
            return;
        }

        if (node.type === "listTake") {
            visit(node.source, node, "source", null);
            visit(node.countExpression, node, "countExpression", null);
            return;
        }

        if (node.type === "listDistinct") {
            visit(node.source, node, "source", null);
            visit(node.keyExpression, node, "keyExpression", null);
            return;
        }

        if (node.type === "listSelect") {
            visit(node.source, node, "source", null);
            visit(node.selectExpression, node, "selectExpression", null);
        }
    };

    visit(root, null, null, null);
    return found;
}

function collectPipelineCandidates(root) {
    const result = [];
    const visited = new Set();

    const visit = (node, parentNode = null, slot = null, index = null) => {
        if (!node || typeof node !== "object") return;
        if (visited.has(node)) return;
        visited.add(node);

        if (node.type === "value" && node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
            visit(node.metadata.innerAST, node, "innerAST", null);
            return;
        }

        if (isListOperationNode(node) && !isListOperationNode(parentNode)) {
            const pipeline = extractListPipeline(node);
            if (pipeline?.source) {
                result.push({
                    root: node,
                    parentNode,
                    slot,
                    index,
                    source: pipeline.source,
                    stages: pipeline.stages,
                    allowEmpty: true
                });
            }
        }

        if (node.type === "operator") {
            visit(node.leftOperand, node, "left", null);
            visit(node.rightOperand, node, "right", null);
            return;
        }

        if (node.type === "functionCall") {
            (node.args || []).forEach((arg, argIndex) => visit(arg, node, "arg", argIndex));
            return;
        }

        if (node.type === "listQuery") {
            visit(node.source, node, "source", null);
            visit(node.filterExpression, node, "filterExpression", null);
            return;
        }

        if (node.type === "listSort") {
            visit(node.source, node, "source", null);
            visit(node.sortExpression, node, "sortExpression", null);
            return;
        }

        if (node.type === "listTake") {
            visit(node.source, node, "source", null);
            visit(node.countExpression, node, "countExpression", null);
            return;
        }

        if (node.type === "listDistinct") {
            visit(node.source, node, "source", null);
            visit(node.keyExpression, node, "keyExpression", null);
            return;
        }

        if (node.type === "listSelect") {
            visit(node.source, node, "source", null);
            visit(node.selectExpression, node, "selectExpression", null);
        }
    };

    visit(root, null, null, null);
    return result;
}

function clearPipelineEditState(editor) {
    editor.pipelineEditState = createPipelineEditState();
}

function alignPipelineEditState(editor, pipelineContext) {
    const state = editor.pipelineEditState;
    if (!state?.node) return;
    const stages = Array.isArray(pipelineContext?.stages) ? pipelineContext.stages : [];
    const stillExists = stages.some((stage) => stage?.node === state.node);
    if (!stillExists) {
        clearPipelineEditState(editor);
    }
}

function writeChildNode(editor, parentNode, slot, index, nextNode) {
    if (!editor || typeof editor.setSlotNode !== "function") return false;
    return editor.setSlotNode(parentNode, slot, index, nextNode);
}

function applyPipelineRootReplacement(editor, pipelineContext, newRoot) {
    if (!pipelineContext?.parentNode) {
        editor.workspaceAST = newRoot;
        return;
    }
    const updated = writeChildNode(editor, pipelineContext.parentNode, pipelineContext.slot, pipelineContext.index, newRoot);
    if (!updated) {
        console.warn("DslEditor: applyPipelineRootReplacement failed to write slot.", pipelineContext);
    }
}

export function getPipelineContext(editor) {
    syncPipelineContext(editor);
    const root = unwrapEncapsulatedNode(editor, editor.workspaceAST);
    if (!root) return null;
    if (!editor.pipelineOpen) return null;
    if (editor.pipelineOpenMode === "fresh") {
        const freshSource = editor.pipelineOpenSource || root;
        const sourceType = getSemanticType(editor, freshSource);
        const allowSingleton = sourceType
            && !editor.isCollectionType(sourceType)
            && typeof editor.canOpenPipelineOnType === "function"
            && editor.canOpenPipelineOnType(sourceType);
        if (!sourceType || (!editor.isCollectionType(sourceType) && !allowSingleton)) {
            editor.pipelineOpen = false;
            editor.pipelineOpenSource = null;
            editor.pipelineOpenMode = null;
            markPipelineContextDirty(editor);
            return null;
        }
        return {
            root: freshSource,
            parentNode: null,
            slot: null,
            index: null,
            source: freshSource,
            stages: [],
            allowEmpty: true
        };
    }
    if (root.type === "value" && root.metadata?.type === "rawText") {
        const rawText = String(root.value || root.metadata?.rawText || "");
        if (/\.(query|orderBy|take|distinct|select)\s*\(/i.test(rawText)) {
            return {
                root,
                source: null,
                stages: [{ type: "raw", node: root, rawText }],
                allowEmpty: false
            };
        }
    }

    const pipelineCandidates = collectPipelineCandidates(root);
    const requestedSource = editor.pipelineOpenSource || null;
    if (pipelineCandidates.length > 0) {
        let selected = null;

        if (requestedSource) {
            selected = pipelineCandidates.find((candidate) => (
                candidate.root === requestedSource
                || candidate.source === requestedSource
            )) || null;
            if (!selected && pipelineCandidates.length === 1) {
                selected = pipelineCandidates[0];
            }
        } else if (pipelineCandidates.length === 1) {
            selected = pipelineCandidates[0];
        }

        if (selected) {
            editor.pipelineOpenSource = selected.source || null;
            alignPipelineEditState(editor, selected);
            markPipelineContextDirty(editor);
            return selected;
        }
    }

    if (requestedSource) {
        const target = findPipelineTargetLocation(editor, root, requestedSource);
        if (target?.node) {
            const targetContext = {
                root: target.node,
                parentNode: target.parentNode,
                slot: target.slot,
                index: target.index,
                source: target.node,
                stages: [],
                allowEmpty: true
            };
            editor.pipelineOpenSource = target.node;
            alignPipelineEditState(editor, targetContext);
            markPipelineContextDirty(editor);
            return targetContext;
        }
    }

    const returnType = getSemanticType(editor, root);
    const allowSingleton = returnType
        && !editor.isCollectionType(returnType)
        && typeof editor.canOpenPipelineOnType === "function"
        && editor.canOpenPipelineOnType(returnType);
    if (returnType && !editor.isCollectionType(returnType) && !allowSingleton) {
        editor.pipelineOpen = false;
        editor.pipelineOpenSource = null;
        editor.pipelineOpenMode = null;
        markPipelineContextDirty(editor);
        return null;
    }

    editor.pipelineOpenSource = root;
    alignPipelineEditState(editor, { stages: [] });
    markPipelineContextDirty(editor);
    return {
        root,
        parentNode: null,
        slot: null,
        index: null,
        source: root,
        stages: [],
        allowEmpty: true
    };
}

export function openPipelineFromCurrent(editor, options = {}) {
    syncPipelineContext(editor);
    const root = unwrapEncapsulatedNode(editor, editor.workspaceAST);
    if (!root) {
        editor.showExpressionError("当前没有可编辑的集合表达式。");
        return;
    }

    if (!canStartPipelineFromNode(editor, root)) {
        editor.showExpressionError("当前表达式不是集合或实体对象，无法打开筛选。");
        return;
    }

    // 打开新的管线编辑区时，默认不继承旧阶段的编辑焦点。
    clearPipelineEditState(editor);
    const forceFresh = options?.forceFresh === true;
    editor.pipelineOpen = true;
    editor.pipelineOpenMode = forceFresh ? "fresh" : null;
    if (forceFresh) {
        editor.pipelineOpenSource = root;
    } else if (isListOperationNode(root)) {
        const pipeline = extractListPipeline(root);
        editor.pipelineOpenSource = pipeline?.source || root;
    } else {
        // 架构约束：筛选基于当前工作区根表达式，不向子节点回退
        editor.pipelineOpenSource = root;
    }
    markPipelineContextDirty(editor);
}

export function getPipelineStageLabel(editor, stage) {
    const meta = PIPELINE_STAGE_META[stage?.type];
    if (meta?.dslMethod) {
        return `${meta.label} (.${meta.dslMethod})`;
    }
    return meta?.label || stage?.type || "阶段";
}

export function getPipelineStageSummary(editor, stage) {
    if (!stage?.node) return "未设置";
    if (stage.type === "raw") {
        const rawText = stage.rawText || stage.node.value || stage.node.metadata?.rawText || "";
        return rawText ? `原始: ${rawText}` : "原始片段";
    }
    const typeIssue = typeof editor.getPipelineStageTypeIssue === "function"
        ? editor.getPipelineStageTypeIssue(stage)
        : null;
    if (typeIssue) {
        const expectedDisplay = editor.getDisplayTypeName?.(typeIssue.expectedType) || typeIssue.expectedType;
        const actualDisplay = editor.getDisplayTypeName?.(typeIssue.actualType) || typeIssue.actualType;
        if (stage.node.type === "listQuery") {
            return `查询条件需为 ${expectedDisplay}，当前为 ${actualDisplay}`;
        }
        if (stage.node.type === "listSort") {
            return `排序键需为 ${expectedDisplay}，当前为 ${actualDisplay}`;
        }
        if (stage.node.type === "listTake") {
            return `数量需为 ${expectedDisplay}，当前为 ${actualDisplay}`;
        }
        if (stage.node.type === "listDistinct") {
            return `去重键需为 ${expectedDisplay}，当前为 ${actualDisplay}`;
        }
        if (stage.node.type === "listSelect") {
            return `投影需为 ${expectedDisplay}，当前为 ${actualDisplay}`;
        }
        return `类型不匹配：期望 ${expectedDisplay}，实际 ${actualDisplay}`;
    }
    let exprNode = null;
    if (stage.node.type === "listQuery") exprNode = stage.node.filterExpression;
    if (stage.node.type === "listSort") exprNode = stage.node.sortExpression;
    if (stage.node.type === "listTake") exprNode = stage.node.countExpression;
    if (stage.node.type === "listDistinct") exprNode = stage.node.keyExpression;
    if (stage.node.type === "listSelect") exprNode = stage.node.selectExpression;
    const readable = exprNode ? editor.toReadableText(exprNode) : null;
    if (stage.node.type === "listQuery") {
        return readable ? `查询条件: ${readable}` : "未设置查询条件";
    }
    if (stage.node.type === "listSort") {
        const direction = String(stage.node.direction || 'asc').toLowerCase();
        const suffix = direction === 'desc' ? '（降序）' : '（升序）';
        return readable ? `排序键: ${readable}${suffix}` : "未设置排序键";
    }
    if (stage.node.type === "listTake") {
        const strategy = String(stage.node.strategy || 'first').toLowerCase();
        const label = strategy === 'all'
            ? '全部'
            : strategy === 'random'
                ? '随机取'
                : strategy === 'last'
                    ? '取后'
                    : '取前';
        if (strategy === 'all') {
            return "全部保留";
        }
        return readable ? `${label}: ${readable}` : "未设置数量";
    }
    if (stage.node.type === "listDistinct") {
        return readable
            ? `去重键: ${readable}（列表键会展开后尽可能多保留元素）`
            : "未设置去重键（支持标量或标量列表）";
    }
    if (stage.node.type === "listSelect") {
        return readable ? `投影: ${readable}` : "未设置投影";
    }
    return readable || "未设置";
}

export function getPipelineStageSlot(editor, node) {
    if (!node) return null;
    if (node.type === "listQuery") return "filterExpression";
    if (node.type === "listSort") return "sortExpression";
    if (node.type === "listTake") return "countExpression";
    if (node.type === "listDistinct") return "keyExpression";
    if (node.type === "listSelect") return "selectExpression";
    return null;
}

export function getPipelineStageExpressionNode(editor, node, slot) {
    if (!node || !slot) return null;
    if (slot === "filterExpression") return node.filterExpression;
    if (slot === "sortExpression") return node.sortExpression;
    if (slot === "countExpression") return node.countExpression;
    if (slot === "keyExpression") return node.keyExpression;
    if (slot === "selectExpression") return node.selectExpression;
    return null;
}

export function getPipelineStageExpressionText(editor, node, slot) {
    const exprNode = getPipelineStageExpressionNode(editor, node, slot);
    if (!exprNode || exprNode.metadata?.type === "empty") return "";
    if (exprNode.metadata?.type === "encapsulated" && exprNode.metadata?.innerAST?.toExpression) {
        return exprNode.metadata.innerAST.toExpression();
    }
    return exprNode.toExpression?.() || "";
}

function setPipelineStageSlot(stageNode, slot, valueNode) {
    if (!stageNode || !slot) return;
    if (slot === "filterExpression") {
        stageNode.setFilterExpression(valueNode);
    } else if (slot === "sortExpression") {
        stageNode.setSortExpression(valueNode);
    } else if (slot === "countExpression") {
        stageNode.setCountExpression(valueNode);
    } else if (slot === "keyExpression") {
        stageNode.setKeyExpression(valueNode);
    } else if (slot === "selectExpression") {
        stageNode.setSelectExpression(valueNode);
    }
}

function applyPipelineStageAst(editor, stageNode, slot, ast) {
    if (!stageNode || !slot || !ast) return;
    const cloned = typeof editor.cloneAST === "function" ? editor.cloneAST(ast) : ast;
    const encapsulated = editor.createEncapsulatedFrom(cloned);
    setPipelineStageSlot(stageNode, slot, encapsulated);
}

export function isPipelineStageEditing(editor, stage) {
    syncPipelineContext(editor);
    return Boolean(editor.pipelineEditState?.node && stage?.node === editor.pipelineEditState.node);
}

export function editPipelineStage(editor, stage) {
    syncPipelineContext(editor);
    if (!stage?.node) return;
    if (stage.type === "raw") return;
    editor.pipelineOpen = true;
    if (stage.node.type === "listTake" && !stage.node.strategy) {
        stage.node.strategy = "first";
    }
    if (stage.node.type === "listSort" && !stage.node.direction) {
        stage.node.direction = "asc";
    }
    const slot = getPipelineStageSlot(editor, stage.node);
    if (!slot) return;
    const current = editor.pipelineEditState || {};
    if (current.node === stage.node && current.slot === slot) {
        editor.pipelineEditState.node = null;
        editor.pipelineEditState.slot = null;
        editor.pipelineEditState.draftExpression = "";
        editor.pipelineEditState.draftCountValue = "";
        markPipelineContextDirty(editor);
        editor.renderEditor();
        return;
    }
    current.editor = new editor.constructor();
    current.editor.isSubEditor = true;
    current.editor.parentEditor = editor;
    editor.pipelineEditState = {
        node: stage.node,
        slot,
        editor: current.editor,
        draftExpression: getPipelineStageExpressionText(editor, stage.node, slot),
        draftCountValue: getPipelineStageCountValue(editor, stage.node)
    };
    markPipelineContextDirty(editor);
    editor.renderEditor();
}

function getPipelineStageCountValue(editor, stageNode) {
    if (!stageNode || stageNode.type !== "listTake") return "";
    const countNode = stageNode.countExpression;
    if (!countNode) return "";
    const unwrapped = unwrapEncapsulatedNode(editor, countNode);
    if (!unwrapped || unwrapped.type !== "value") return "";
    const staticValue = unwrapped.metadata?.staticValue ?? unwrapped.value;
    const raw = staticValue != null ? String(staticValue).trim() : "";
    return /^-?\d+$/.test(raw) ? raw : "";
}

function syncInlineSuggestionEditor(editor, inlineEditor, expectedType, contextOverride) {
    inlineEditor.metadata = editor.metadata;
    inlineEditor.isInitialized = Boolean(editor.metadata);
    inlineEditor.expectedType = expectedType;
    inlineEditor.slotExpectedType = expectedType;
    inlineEditor.triggeringEventContext = editor.triggeringEventContext;
    inlineEditor.contextOverride = contextOverride;
    inlineEditor.fixedRootContext = contextOverride?.name === "self"
        ? {
            name: contextOverride.name,
            displayName: contextOverride.displayName || contextOverride.name,
            typeName: contextOverride.typeName,
            eventType: contextOverride.eventType || null
        }
        : null;
    inlineEditor.hideFavoritesToolbar = false;
    inlineEditor.enumLocalizationCache = editor.enumLocalizationCache;
    inlineEditor.enumLocalizationRequests = editor.enumLocalizationRequests;
}

export function renderPipelineStageInlineEditor(editor, stage, container) {
    syncPipelineContext(editor);
    if (!isPipelineStageEditing(editor, stage)) return;
    const state = editor.pipelineEditState;
    if (stage?.node?.type === "listTake" && String(stage.node.strategy || "first").toLowerCase() === "all") {
        const hint = document.createElement("div");
        hint.className = "dsl-pipeline-empty-state";
        hint.textContent = "当前策略无需填写数量。";
        container.appendChild(hint);
        return;
    }
    const inlineEditor = state.editor || new editor.constructor();
    state.editor = inlineEditor;

    inlineEditor.isSubEditor = true;
    inlineEditor.parentEditor = editor;
    inlineEditor.pipelineOpen = false;
    inlineEditor.pipelineOpenSource = null;
    inlineEditor.isPipelineInline = false;
    inlineEditor.isLightWorkspace = false;

    const slot = state.slot;
    const fallbackExpectedType = slot === "filterExpression"
        ? "Boolean"
        : slot === "countExpression"
            ? "Int32"
            : slot === "sortExpression"
                ? "NonEntity"
                : slot === "keyExpression"
                    ? "ScalarOrScalarList"
                : slot === "selectExpression"
                    ? "Object"
                    : null;
    const expectedType = (typeof editor.getExpectedTypeForSlot === "function" && slot)
        ? editor.getExpectedTypeForSlot(state.node, slot)
        : fallbackExpectedType;

    const sourceType = getSemanticType(editor, state.node?.source)
        || getSemanticType(editor, editor.pipelineOpenSource);
    let innerType = editor.getCollectionInnerType(sourceType);
    const shouldScopeToSelf = slot === "filterExpression" || slot === "sortExpression" || slot === "keyExpression" || slot === "selectExpression";
    if (!innerType && sourceType && typeof editor.canOpenPipelineOnType === "function"
        && editor.canOpenPipelineOnType(sourceType)) {
        innerType = editor.normalizeTypeName(sourceType) || sourceType;
    }
    if (shouldScopeToSelf && !innerType) {
        innerType = "Object";
    }
    const contextOverride = shouldScopeToSelf && innerType
        ? {
            name: "self",
            displayName: "当前项",
            typeName: innerType,
            eventType: editor.normalizeTypeName(innerType) === "IGameEvent"
                ? resolveEventTypeHintFromSource(editor, state.node?.source || editor.pipelineOpenSource)
                : null
        }
        : null;

    syncInlineSuggestionEditor(editor, inlineEditor, expectedType, contextOverride);

    const initialExpression = state.draftExpression ?? getPipelineStageExpressionText(editor, state.node, state.slot);
    state.draftExpression = initialExpression;

    const workspaceHost = document.createElement("div");
    workspaceHost.className = "dsl-pipeline-inline-workspace";
    container.appendChild(workspaceHost);

    inlineEditor
        .render(
            workspaceHost,
            initialExpression,
            (expression) => {
                state.draftExpression = expression;
            },
            {
                expectedType,
                triggeringEventContext: editor.triggeringEventContext,
                fixedRootContext: inlineEditor.fixedRootContext,
                hideFavoritesToolbar: inlineEditor.hideFavoritesToolbar
            }
        )
        .catch((error) => {
            console.error("Pipeline inline DSL render failed:", error);
        });
}

export function commitPipelineStageEdit(editor) {
    syncPipelineContext(editor);
    const state = editor.pipelineEditState;
    if (!state?.node || !state.slot) return;
    const isTakeStage = state.node.type === "listTake";
    const isTakeAll = isTakeStage && String(state.node.strategy || "first").toLowerCase() === "all";
    const inlineEditor = state.editor;
    const inlineAst = inlineEditor?.workspaceAST || null;
    const isEmptyAst = inlineAst?.type === "value" && inlineAst.metadata?.type === "empty";
    let expression = state.editor?.expression ?? state.draftExpression ?? "";
    if (isEmptyAst) {
        expression = "";
    }

    let trimmed = String(expression || "").trim();

    if (inlineEditor && inlineAst && !isEmptyAst) {
        finalizeDraftInputs(inlineEditor, inlineAst);
        if (findIncompleteNode(inlineAst)) {
            inlineEditor.showExpressionError("工作区表达式不完整，无法保存。");
            return;
        }
        const astValidation = validateAST(inlineAst);
        if (!astValidation?.isValid) {
            const msg = (astValidation?.errors || []).map(e => String(e)).join('\n');
            inlineEditor.showExpressionError(msg || "表达式不合法，无法保存。");
            return;
        }
        applyPipelineStageAst(editor, state.node, state.slot, inlineAst);
        trimmed = String(inlineAst?.toExpression?.() || "").trim();
    } else {
        applyPipelineStageExpression(editor, state.node, state.slot, expression);
    }

    if (isTakeAll) {
        state.node.setCountExpression?.(null);
        trimmed = "";
    }

    editor.pipelineEditState.node = null;
    editor.pipelineEditState.slot = null;
    editor.pipelineEditState.draftExpression = "";
    editor.pipelineEditState.draftCountValue = "";
    markPipelineContextDirty(editor);
    if (!trimmed && !isTakeAll) {
        const pipeline = getPipelineContext(editor);
        const index = pipeline?.stages?.findIndex(stage => stage.node === state.node);
        if (typeof index === "number" && index >= 0) {
            removePipelineStage(editor, index);
            return;
        }
    }
    editor.updateExpressionFromAST();
}

export function cancelPipelineStageEdit(editor) {
    syncPipelineContext(editor);
    if (!editor.pipelineEditState) return;
    editor.pipelineEditState.node = null;
    editor.pipelineEditState.slot = null;
    editor.pipelineEditState.draftExpression = "";
    editor.pipelineEditState.draftCountValue = "";
    markPipelineContextDirty(editor);
    editor.renderEditor();
}

export function applyPipelineStageExpression(editor, stageNode, slot, expression) {
    if (!stageNode || !slot) return;
    const trimmed = String(expression || "").trim();
    if (!trimmed) {
        const placeholder = createValueNode("", "Void", { type: "empty" });
        setPipelineStageSlot(stageNode, slot, placeholder);
        return;
    }

    const ast = editor.buildAstFromExpression(trimmed);
    if (!ast) return;
    const encapsulated = editor.createEncapsulatedFrom(ast);
    setPipelineStageSlot(stageNode, slot, encapsulated);
}

export function getPipelineStageTypeIssue(editor, stage) {
    if (!stage?.node) return null;
    if (stage.type === "raw") return null;
    let exprNode = null;
    let slot = null;
    let fallbackExpectedType = null;
    if (stage.node.type === "listQuery") {
        slot = "filterExpression";
        exprNode = stage.node.filterExpression;
        fallbackExpectedType = "Boolean";
    } else if (stage.node.type === "listTake") {
        const strategy = String(stage.node.strategy || "").toLowerCase();
        if (strategy === "all") return null;
        slot = "countExpression";
        exprNode = stage.node.countExpression;
        fallbackExpectedType = "Int32";
    } else if (stage.node.type === "listSort") {
        slot = "sortExpression";
        exprNode = stage.node.sortExpression;
        fallbackExpectedType = "NonEntity";
    } else if (stage.node.type === "listDistinct") {
        slot = "keyExpression";
        exprNode = stage.node.keyExpression;
        fallbackExpectedType = "ScalarOrScalarList";
    } else if (stage.node.type === "listSelect") {
        slot = "selectExpression";
        exprNode = stage.node.selectExpression;
        fallbackExpectedType = "Object";
    }

    const expectedType = (typeof editor.getExpectedTypeForSlot === "function" && slot)
        ? editor.getExpectedTypeForSlot(stage.node, slot)
        : fallbackExpectedType;
    if (!exprNode || !expectedType) return null;
    if (exprNode.metadata?.type === "empty") return null;
    const actualType = getSemanticType(editor, exprNode);
    if (!actualType) return null;
    const normalizedExpectedType = typeof editor.normalizeTypeName === "function"
        ? editor.normalizeTypeName(expectedType)
        : expectedType;

    let hasIssue = false;
    if (normalizedExpectedType === "Int32") {
        hasIssue = !editor.isNumericType(actualType);
    } else if (normalizedExpectedType === "ScalarOrScalarList") {
        const isEntity = typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(actualType);
        const isDictionary = typeof editor.isDictionaryType === "function" && editor.isDictionaryType(actualType);
        const isCollection = typeof editor.isCollectionType === "function" && editor.isCollectionType(actualType);

        if (isEntity || isDictionary) {
            hasIssue = true;
        } else if (isCollection) {
            const match = String(actualType).trim().match(/^(List|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection)<(.+)>$/);
            const innerType = match?.[2] ? String(match[2]).trim() : null;
            const innerIsEntity = innerType && typeof editor.isBusinessObjectType === "function"
                ? editor.isBusinessObjectType(innerType)
                : false;
            const innerIsDictionary = innerType && typeof editor.isDictionaryType === "function"
                ? editor.isDictionaryType(innerType)
                : false;
            hasIssue = Boolean(innerIsEntity || innerIsDictionary);
        }
    } else if (normalizedExpectedType === "NonEntity") {
        const isEntity = typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(actualType);
        const isCollection = typeof editor.isCollectionType === "function" && editor.isCollectionType(actualType);
        const isDictionary = typeof editor.isDictionaryType === "function" && editor.isDictionaryType(actualType);
        hasIssue = isEntity || isCollection || isDictionary;
    } else if (!editor.areTypesCompatible(actualType, normalizedExpectedType)) {
        hasIssue = true;
    }

    if (!hasIssue) return null;
    return { expectedType: normalizedExpectedType, actualType };
}

export function insertPipelineStage(editor, stageType, insertIndex = null) {
    syncPipelineContext(editor);
    const pipeline = getPipelineContext(editor);
    if (!pipeline || !pipeline.source) return;

    const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
    const newNode = createStageNode(stageType, pipeline.source);
    if (!newNode) return;

    const index = typeof insertIndex === "number" ? insertIndex : stages.length;
    stages.splice(index, 0, { type: stageType, node: newNode });

    const newRoot = rebuildListChainFromStages(pipeline.source, stages);
    applyPipelineRootReplacement(editor, pipeline, newRoot);
    editor.pipelineOpenMode = null;
    editor.pipelineOpenSource = pipeline.source || null;
    editor.maintainASTRelationships(editor.workspaceAST);
    editor.updateExpressionFromAST();
    editor.renderEditor();
}

export function removePipelineStage(editor, index) {
    syncPipelineContext(editor);
    const pipeline = getPipelineContext(editor);
    if (!pipeline) return;
    const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
    if (index < 0 || index >= stages.length) return;
    stages.splice(index, 1);

    const newRoot = stages.length > 0
        ? rebuildListChainFromStages(pipeline.source, stages)
        : pipeline.source;

    applyPipelineRootReplacement(editor, pipeline, newRoot);
    editor.pipelineOpenMode = null;
    editor.pipelineOpenSource = pipeline.source || null;
    editor.maintainASTRelationships(editor.workspaceAST);
    editor.updateExpressionFromAST();
    editor.renderEditor();
}

export function movePipelineStage(editor, fromIndex, toIndex) {
    syncPipelineContext(editor);
    const pipeline = getPipelineContext(editor);
    if (!pipeline) return;
    const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
    if (fromIndex < 0 || fromIndex >= stages.length) return;
    if (toIndex < 0 || toIndex >= stages.length) return;
    if (fromIndex === toIndex) return;

    const [moved] = stages.splice(fromIndex, 1);
    stages.splice(toIndex, 0, moved);

    const newRoot = rebuildListChainFromStages(pipeline.source, stages);
    applyPipelineRootReplacement(editor, pipeline, newRoot);
    editor.pipelineOpenMode = null;
    editor.pipelineOpenSource = pipeline.source || null;
    editor.maintainASTRelationships(editor.workspaceAST);
    editor.updateExpressionFromAST();
    editor.renderEditor();
}
