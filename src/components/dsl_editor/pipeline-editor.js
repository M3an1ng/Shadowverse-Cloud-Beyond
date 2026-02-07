import { createValueNode, validateAST, findIncompleteNode } from "./core.js";
import { PIPELINE_STAGE_META, isListOperationNode, extractListPipeline, rebuildListChainFromStages, createStageNode } from "./pipeline.js";
import { finalizeDraftInputs } from "./ast-normalize.js";

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
        pipelineEditState: editor.pipelineEditState || createPipelineEditState()
    });
}

export function syncPipelineContext(editor) {
    if (editor.isPipelineInline) return null;
    ensurePipelineContextInfra(editor);

    const nextKey = getCurrentPipelineContextKey(editor);
    const currentKey = editor.pipelineActiveContextKey;
    const hasBootstrappedState = editor.pipelineContextStore.has(nextKey);

    if (currentKey && currentKey !== nextKey) {
        markPipelineContextDirty(editor);
    }

    if (currentKey === nextKey) {
        return nextKey;
    }

    // 首次进入上下文时，沿用当前实例上的兼容字段，避免覆盖旧调用路径预先设置的状态
    if (!currentKey && !hasBootstrappedState) {
        editor.pipelineContextStore.set(nextKey, {
            pipelineOpen: Boolean(editor.pipelineOpen),
            pipelineOpenSource: editor.pipelineOpenSource || null,
            pipelineEditState: editor.pipelineEditState || createPipelineEditState()
        });
    }

    const state = editor.pipelineContextStore.get(nextKey) || createPipelineContextState();
    editor.pipelineOpen = Boolean(state.pipelineOpen);
    editor.pipelineOpenSource = state.pipelineOpenSource || null;
    editor.pipelineEditState = state.pipelineEditState || createPipelineEditState();
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

export function getPipelineContext(editor) {
    syncPipelineContext(editor);
    const root = unwrapEncapsulatedNode(editor, editor.workspaceAST);
    if (!root) return null;
    if (!editor.pipelineOpen) return null;
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
    if (isListOperationNode(root)) {
        const pipeline = extractListPipeline(root);
        if (!pipeline) return null;
        editor.pipelineOpenSource = pipeline.source || null;
        markPipelineContextDirty(editor);
        return {
            root,
            source: pipeline.source,
            stages: pipeline.stages,
            allowEmpty: true
        };
    }

    const returnType = root.returnType || editor.calculateASTReturnType(root);
    const allowSingleton = returnType
        && !editor.isCollectionType(returnType)
        && typeof editor.isBusinessObjectType === "function"
        && editor.isBusinessObjectType(returnType);
    if (returnType && !editor.isCollectionType(returnType) && !allowSingleton) {
        editor.pipelineOpen = false;
        editor.pipelineOpenSource = null;
        markPipelineContextDirty(editor);
        return null;
    }

    editor.pipelineOpenSource = root;
    markPipelineContextDirty(editor);
    return {
        root,
        source: root,
        stages: [],
        allowEmpty: true
    };
}

export function openPipelineFromCurrent(editor) {
    syncPipelineContext(editor);
    const root = unwrapEncapsulatedNode(editor, editor.workspaceAST);
    if (!root) {
        editor.showExpressionError("当前没有可编辑的集合表达式。");
        return;
    }
    const returnType = root.returnType || editor.calculateASTReturnType(root);
    const allowSingleton = returnType
        && !editor.isCollectionType(returnType)
        && typeof editor.isBusinessObjectType === "function"
        && editor.isBusinessObjectType(returnType);
    if (!editor.isCollectionType(returnType) && !allowSingleton) {
        editor.showExpressionError("当前表达式不是集合或实体对象，无法打开筛选。");
        return;
    }
    editor.pipelineOpen = true;
    editor.pipelineOpenSource = root;
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
        return readable ? `去重键: ${readable}` : "未设置去重键";
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
    inlineEditor.enumLocalizationCache = editor.enumLocalizationCache;
    inlineEditor.enumLocalizationRequests = editor.enumLocalizationRequests;
}

export function renderPipelineStageInlineEditor(editor, stage, container) {
    syncPipelineContext(editor);
    if (!isPipelineStageEditing(editor, stage)) return;
    const state = editor.pipelineEditState;
    if (stage?.node?.type === "listTake") {
        const strategy = String(stage.node.strategy || "first").toLowerCase();
        if (strategy !== "all") {
            const inputWrap = document.createElement("div");
            inputWrap.className = "dsl-pipeline-inline-input";

            const inputId = `dsl-take-count-${Math.random().toString(36).slice(2, 10)}`;
            const label = document.createElement("label");
            label.textContent = "数量";
            label.className = "dsl-pipeline-select-label";
            label.htmlFor = inputId;
            inputWrap.appendChild(label);

            const input = document.createElement("input");
            input.type = "number";
            input.min = "0";
            input.step = "1";
            input.placeholder = "输入数量";
            input.id = inputId;
            input.value = state.draftCountValue || "";
            input.className = "dsl-pipeline-inline-field";

            input.addEventListener("input", (event) => {
                state.draftCountValue = event.target.value;
            });

            inputWrap.appendChild(input);
            container.appendChild(inputWrap);
        } else {
            const hint = document.createElement("div");
            hint.className = "dsl-pipeline-empty-state";
            hint.textContent = "当前策略无需填写数量。";
            container.appendChild(hint);
        }
        return;
    }
    const inlineEditor = state.editor || new editor.constructor();
    state.editor = inlineEditor;

    inlineEditor.isSubEditor = true;
    inlineEditor.parentEditor = editor;
    inlineEditor.pipelineOpen = false;
    inlineEditor.pipelineOpenSource = null;
    inlineEditor.isPipelineInline = true;
    inlineEditor.isLightWorkspace = true;

    const slot = state.slot;
    const expectedType = slot === "filterExpression"
        ? "Boolean"
        : slot === "countExpression"
            ? "Int32"
            : slot === "sortExpression" || slot === "keyExpression"
                ? "NonEntity"
            : slot === "selectExpression"
                ? "Object"
                : null;

    const sourceType = state.node?.source?.returnType
        || editor.calculateASTReturnType(state.node?.source)
        || editor.pipelineOpenSource?.returnType
        || editor.calculateASTReturnType(editor.pipelineOpenSource);
    let innerType = editor.getCollectionInnerType(sourceType);
    const shouldScopeToSelf = slot === "filterExpression" || slot === "sortExpression" || slot === "keyExpression" || slot === "selectExpression";
    if (!innerType && sourceType && typeof editor.isBusinessObjectType === "function"
        && editor.isBusinessObjectType(sourceType)) {
        innerType = sourceType;
    }
    if (shouldScopeToSelf && !innerType) {
        innerType = "Object";
    }
    const contextOverride = shouldScopeToSelf && innerType
        ? { name: "self", displayName: "当前项", typeName: innerType }
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
                triggeringEventContext: editor.triggeringEventContext
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
    if (state.node.type === "listTake") {
        const strategy = String(state.node.strategy || "first").toLowerCase();
        const trimmedCount = String(state.draftCountValue || "").trim();
        const previousExpression = String(state.draftExpression || "").trim();
        if (strategy === "all") {
            state.node.countExpression = null;
        } else if (trimmedCount) {
            const normalized = /^-?\d+$/.test(trimmedCount) ? trimmedCount : String(parseInt(trimmedCount, 10) || "");
            if (normalized) {
                const valueNode = createValueNode(normalized, "Int32", {
                    type: "staticValue",
                    staticValue: normalized,
                    isAwaitingInput: false
                });
                state.node.setCountExpression?.(valueNode);
            }
        } else if (!previousExpression) {
            const placeholder = createValueNode("", "Void", { type: "empty" });
            state.node.setCountExpression?.(placeholder);
        }

        editor.pipelineEditState.node = null;
        editor.pipelineEditState.slot = null;
        editor.pipelineEditState.draftExpression = "";
        editor.pipelineEditState.draftCountValue = "";
        markPipelineContextDirty(editor);

        const shouldRemove = strategy !== "all" && !trimmedCount && !previousExpression;
        if (shouldRemove) {
            const pipeline = getPipelineContext(editor);
            const index = pipeline?.stages?.findIndex(stage => stage.node === state.node);
            if (typeof index === "number" && index >= 0) {
                removePipelineStage(editor, index);
                return;
            }
        }

        editor.updateExpressionFromAST();
        return;
    }
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

    editor.pipelineEditState.node = null;
    editor.pipelineEditState.slot = null;
    editor.pipelineEditState.draftExpression = "";
    editor.pipelineEditState.draftCountValue = "";
    markPipelineContextDirty(editor);
    if (!trimmed) {
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
    let expectedType = null;
    if (stage.node.type === "listQuery") {
        exprNode = stage.node.filterExpression;
        expectedType = "Boolean";
    } else if (stage.node.type === "listTake") {
        const strategy = String(stage.node.strategy || "").toLowerCase();
        if (strategy === "all") return null;
        exprNode = stage.node.countExpression;
        expectedType = "Int32";
    } else if (stage.node.type === "listSort") {
        exprNode = stage.node.sortExpression;
        expectedType = "NonEntity";
    } else if (stage.node.type === "listDistinct") {
        exprNode = stage.node.keyExpression;
        expectedType = "NonEntity";
    } else if (stage.node.type === "listSelect") {
        exprNode = stage.node.selectExpression;
        expectedType = "Object";
    }

    if (!exprNode || !expectedType) return null;
    if (exprNode.metadata?.type === "empty") return null;
    const actualType = editor.calculateASTReturnType(exprNode);
    if (!actualType) return null;

    let hasIssue = false;
    if (expectedType === "Int32") {
        hasIssue = !editor.isNumericType(actualType);
    } else if (expectedType === "NonEntity") {
        const isEntity = typeof editor.isBusinessObjectType === "function" && editor.isBusinessObjectType(actualType);
        const isCollection = typeof editor.isCollectionType === "function" && editor.isCollectionType(actualType);
        const isDictionary = typeof editor.isDictionaryType === "function" && editor.isDictionaryType(actualType);
        hasIssue = isEntity || isCollection || isDictionary;
    } else if (!editor.areTypesCompatible(actualType, expectedType)) {
        hasIssue = true;
    }

    if (!hasIssue) return null;
    return { expectedType, actualType };
}

export function insertPipelineStage(editor, stageType, insertIndex = null) {
    syncPipelineContext(editor);
    const pipeline = getPipelineContext(editor);
    if (!pipeline || !pipeline.source) return;
    const newNode = createStageNode(stageType, pipeline.source);
    if (!newNode) return;

    const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
    const index = typeof insertIndex === "number" ? insertIndex : stages.length;
    stages.splice(index, 0, { type: stageType, node: newNode });

    const newRoot = rebuildListChainFromStages(pipeline.source, stages);
    editor.workspaceAST = newRoot;
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

    editor.workspaceAST = newRoot;
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
    editor.workspaceAST = newRoot;
    editor.maintainASTRelationships(editor.workspaceAST);
    editor.updateExpressionFromAST();
    editor.renderEditor();
}
