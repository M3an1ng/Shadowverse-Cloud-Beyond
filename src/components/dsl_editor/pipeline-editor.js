import { createValueNode } from "./core.js";
import { PIPELINE_STAGE_META, isListOperationNode, extractListPipeline, rebuildListChainFromStages, createStageNode } from "./pipeline.js";

export function createPipelineEditState() {
    return {
        node: null,
        slot: null,
        editor: null,
        draftExpression: ""
    };
}

export function unwrapEncapsulatedNode(editor, node) {
    if (!node) return null;
    if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
        return node.metadata.innerAST;
    }
    return node;
}

export function getPipelineContext(editor) {
    const root = unwrapEncapsulatedNode(editor, editor.astRoot);
    if (!root) return null;
    if (!editor.pipelineOpen) return null;
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
        editor.pipelineOpenSource = pipeline.source || null;
        return {
            root,
            source: pipeline.source,
            stages: pipeline.stages,
            allowEmpty: true
        };
    }

    const returnType = root.returnType || editor.calculateASTReturnType(root);
    if (returnType && !editor.isCollectionType(returnType)) {
        editor.pipelineOpen = false;
        editor.pipelineOpenSource = null;
        return null;
    }

    editor.pipelineOpenSource = root;
    return {
        root,
        source: root,
        stages: [],
        allowEmpty: true
    };
}

export function openPipelineFromCurrent(editor) {
    const root = unwrapEncapsulatedNode(editor, editor.astRoot);
    if (!root) {
        editor.showExpressionError("当前没有可编辑的集合表达式。");
        return;
    }
    const returnType = root.returnType || editor.calculateASTReturnType(root);
    if (!editor.isCollectionType(returnType)) {
        editor.showExpressionError("当前表达式不是集合，无法打开管线。");
        return;
    }
    editor.pipelineOpen = true;
    editor.pipelineOpenSource = root;
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
    let exprNode = null;
    if (stage.node.type === "listQuery") exprNode = stage.node.filterExpression;
    if (stage.node.type === "listSort") exprNode = stage.node.sortExpression;
    if (stage.node.type === "listTake") exprNode = stage.node.countExpression;
    const readable = exprNode ? editor.toReadableText(exprNode) : null;
    if (stage.node.type === "listQuery") {
        return readable ? `条件: ${readable}` : "未设置筛选条件";
    }
    if (stage.node.type === "listSort") {
        return readable ? `排序键: ${readable}` : "未设置排序键";
    }
    if (stage.node.type === "listTake") {
        const strategy = String(stage.node.strategy || 'first').toLowerCase();
        const label = strategy === 'random'
            ? '随机取'
            : strategy === 'last'
                ? '取后'
                : '取前';
        return readable ? `${label}: ${readable}` : "未设置数量";
    }
    return readable || "未设置";
}

export function getPipelineStageSlot(editor, node) {
    if (!node) return null;
    if (node.type === "listQuery") return "filterExpression";
    if (node.type === "listSort") return "sortExpression";
    if (node.type === "listTake") return "countExpression";
    return null;
}

export function getPipelineStageExpressionNode(editor, node, slot) {
    if (!node || !slot) return null;
    if (slot === "filterExpression") return node.filterExpression;
    if (slot === "sortExpression") return node.sortExpression;
    if (slot === "countExpression") return node.countExpression;
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

export function isPipelineStageEditing(editor, stage) {
    return Boolean(editor.pipelineEditState?.node && stage?.node === editor.pipelineEditState.node);
}

export function editPipelineStage(editor, stage) {
    if (!stage?.node) return;
    if (stage.type === "raw") return;
    editor.pipelineOpen = true;
    if (stage.node.type === "listTake" && !stage.node.strategy) {
        stage.node.strategy = "first";
    }
    const slot = getPipelineStageSlot(editor, stage.node);
    if (!slot) return;
    const current = editor.pipelineEditState || {};
    if (current.node === stage.node && current.slot === slot) {
        editor.pipelineEditState.node = null;
        editor.pipelineEditState.slot = null;
        editor.pipelineEditState.draftExpression = "";
        editor.renderEditor();
        return;
    }
    if (!current.editor) {
        current.editor = new editor.constructor();
        current.editor.isSubEditor = true;
        current.editor.parentEditor = editor;
    }
    editor.pipelineEditState = {
        node: stage.node,
        slot,
        editor: current.editor,
        draftExpression: getPipelineStageExpressionText(editor, stage.node, slot)
    };
    editor.renderEditor();
}

function syncInlineSuggestionEditor(editor, inlineEditor, expectedType, contextOverride) {
    inlineEditor.metadata = editor.metadata;
    inlineEditor.isInitialized = Boolean(editor.metadata);
    inlineEditor.expectedType = expectedType;
    inlineEditor.slotExpectedType = expectedType;
    inlineEditor.triggeringEventContext = editor.triggeringEventContext;
    inlineEditor.contextOverride = contextOverride;
}

function getPropertyChainSuggestionContext(inlineEditor, expression, caret) {
    const prefix = expression.slice(0, caret);
    const match = prefix.match(/([A-Za-z_][A-Za-z0-9_.]*)$/);
    if (!match) return null;
    const chainText = match[1];
    if (!chainText.includes(".")) return null;

    const endsWithDot = chainText.endsWith(".");
    const segments = chainText.split(".");
    const partial = endsWithDot ? "" : segments.pop();
    const chainSegments = segments.filter(Boolean);
    if (chainSegments.length === 0) return null;

    const ctx = inlineEditor.getDslContextObject(chainSegments[0]);
    if (!ctx) return null;

    let currentType = inlineEditor.normalizeTypeName(ctx.typeName) || "Object";
    for (let i = 1; i < chainSegments.length; i++) {
        const seg = chainSegments[i];
        const meta = inlineEditor.getTypeMetadata(currentType);
        const prop = (meta?.properties || []).find(p =>
            String(p.name || "").toLowerCase() === String(seg || "").toLowerCase()
        );
        if (!prop) return null;
        currentType = inlineEditor.normalizeTypeName(prop.typeName) || "Object";
    }

    const options = inlineEditor.getObjectProperties(currentType) || [];
    const partialLower = String(partial || "").toLowerCase();
    const filtered = partialLower
        ? options.filter(option =>
            String(option.value || "").toLowerCase().startsWith(partialLower) ||
            String(option.label || "").toLowerCase().startsWith(partialLower)
        )
        : options;

    const replaceStart = match.index + chainText.length - String(partial || "").length;
    return {
        mode: "property",
        options: filtered,
        replaceStart,
        replaceEnd: caret
    };
}

function getGeneralSuggestionContext(inlineEditor, expression, caret) {
    const trimmed = String(expression || "").trim();
    inlineEditor.astRoot = trimmed ? inlineEditor.buildAstFromExpression(trimmed) : null;
    inlineEditor.isExpressionCompleted = false;
    const options = inlineEditor.getCurrentEditingOptions?.() || [];

    const prefix = expression.slice(0, caret);
    const tokenMatch = prefix.match(/([A-Za-z_][A-Za-z0-9_]*)$/);
    const token = tokenMatch ? tokenMatch[1] : "";
    const tokenLower = token.length >= 2 ? token.toLowerCase() : "";
    const filtered = tokenLower
        ? options.filter(option =>
            String(option.label || "").toLowerCase().includes(tokenLower) ||
            String(option.value || "").toLowerCase().includes(tokenLower)
        )
        : options;

    return {
        mode: "general",
        options: filtered,
        replaceStart: caret,
        replaceEnd: caret
    };
}

function buildSuggestionInsert(option, expectedType) {
    if (!option) return { text: "", caretOffset: 0 };
    if (option.type === "operator") {
        const token = option.operator || option.value || "";
        return { text: ` ${token} `, caretOffset: 0 };
    }
    if (option.type === "function") {
        const name = option.value || option.name || option.label || "";
        return { text: `${name}()`, caretOffset: -1 };
    }
    if (option.type === "eventField") {
        return { text: option.value || "", caretOffset: 0 };
    }
    if (option.type === "staticValue") {
        if (expectedType === "Boolean") return { text: "true", caretOffset: 0 };
        if (expectedType === "Int32" || expectedType === "Number") return { text: "0", caretOffset: 0 };
        return { text: "\"\"", caretOffset: -1 };
    }
    return { text: option.value || option.label || "", caretOffset: 0 };
}

export function renderPipelineStageInlineEditor(editor, stage, container) {
    if (!isPipelineStageEditing(editor, stage)) return;
    const state = editor.pipelineEditState;
    const inlineEditor = state.editor || new editor.constructor();
    state.editor = inlineEditor;

    inlineEditor.isSubEditor = true;
    inlineEditor.parentEditor = editor;
    inlineEditor.pipelineOpen = false;
    inlineEditor.pipelineOpenSource = null;
    inlineEditor.isPipelineInline = true;

    const slot = state.slot;
    const expectedType = slot === "filterExpression"
        ? "Boolean"
        : slot === "countExpression"
            ? "Int32"
            : null;

    const sourceType = state.node?.source?.returnType || editor.calculateASTReturnType(state.node?.source);
    const innerType = editor.getCollectionInnerType(sourceType);
    const shouldScopeToSelf = slot === "filterExpression" || slot === "sortExpression";
    const contextOverride = shouldScopeToSelf && innerType
        ? { name: "self", displayName: "当前项 (self)", typeName: innerType }
        : null;

    syncInlineSuggestionEditor(editor, inlineEditor, expectedType, contextOverride);

    const inputRow = document.createElement("div");
    inputRow.className = "dsl-pipeline-inline-input";

    const input = document.createElement("input");
    input.className = "dsl-pipeline-inline-field";
    input.placeholder = expectedType
        ? `输入${editor.getDisplayTypeName(expectedType)}表达式`
        : "输入公式";
    input.value = state.draftExpression ?? getPipelineStageExpressionText(editor, state.node, state.slot);
    state.draftExpression = input.value;
    inputRow.appendChild(input);

    const tooltip = document.createElement("div");
    tooltip.className = "dsl-options-dropdown dsl-inline-tooltip";
    tooltip.style.display = "none";
    inputRow.appendChild(tooltip);

    container.appendChild(inputRow);

    let suggestionContext = null;
    let flatOptions = [];
    let activeIndex = 0;
    let hideTimer = null;

    const applySuggestion = (option) => {
        const value = input.value || "";
        const caret = input.selectionStart ?? value.length;
        const context = suggestionContext || { replaceStart: caret, replaceEnd: caret };
        const insert = buildSuggestionInsert(option, expectedType);
        const before = value.slice(0, context.replaceStart);
        const after = value.slice(context.replaceEnd);
        const next = `${before}${insert.text}${after}`;

        input.value = next;
        state.draftExpression = next;
        const caretOffset = insert.caretOffset || 0;
        const caretPos = before.length + insert.text.length + caretOffset;
        input.setSelectionRange(caretPos, caretPos);
        input.focus();
        updateSuggestions();
    };

    const renderSuggestions = (context) => {
        tooltip.innerHTML = "";
        flatOptions = [];
        activeIndex = 0;

        const options = context?.options || [];
        if (options.length === 0) {
            tooltip.style.display = "none";
            return;
        }

        const grouped = inlineEditor.groupOptionsByType(options);
        Object.entries(grouped).forEach(([groupName, groupOptions]) => {
            const groupEl = document.createElement("div");
            groupEl.className = "dsl-inline-tooltip-group";
            groupEl.textContent = groupName;
            tooltip.appendChild(groupEl);

            groupOptions.forEach((option) => {
                if (option.type === "listPipeline") return;
                const item = document.createElement("div");
                item.className = "dsl-option";
                item.dataset.index = String(flatOptions.length);

                const label = document.createElement("span");
                label.className = "dsl-option-label";
                label.textContent = option.label || option.value || "未命名";
                item.appendChild(label);

                if (option.description) {
                    const description = document.createElement("div");
                    description.className = "dsl-option-description";
                    description.textContent = option.description;
                    item.appendChild(description);
                }

                item.addEventListener("mouseenter", () => {
                    const idx = Number(item.dataset.index);
                    if (!Number.isNaN(idx)) {
                        activeIndex = idx;
                        updateActiveItem();
                    }
                });

                item.addEventListener("mousedown", (event) => {
                    event.preventDefault();
                });

                item.addEventListener("click", () => {
                    applySuggestion(option);
                });

                tooltip.appendChild(item);
                flatOptions.push(option);
            });
        });

        updateActiveItem();
        tooltip.style.display = flatOptions.length > 0 ? "block" : "none";
    };

    const updateActiveItem = () => {
        const items = tooltip.querySelectorAll(".dsl-option");
        items.forEach((item) => item.classList.remove("is-active"));
        const activeItem = items[activeIndex];
        if (activeItem) {
            activeItem.classList.add("is-active");
            activeItem.scrollIntoView({ block: "nearest" });
        }
    };

    const updateSuggestions = () => {
        const value = input.value || "";
        const caret = input.selectionStart ?? value.length;
        const propertyContext = getPropertyChainSuggestionContext(inlineEditor, value, caret);
        suggestionContext = propertyContext || getGeneralSuggestionContext(inlineEditor, value, caret);
        renderSuggestions(suggestionContext);
    };

    const scheduleHide = () => {
        if (hideTimer) clearTimeout(hideTimer);
        hideTimer = setTimeout(() => {
            tooltip.style.display = "none";
        }, 120);
    };

    input.addEventListener("input", (event) => {
        state.draftExpression = event.target.value;
        updateSuggestions();
    });

    input.addEventListener("focus", () => {
        if (hideTimer) clearTimeout(hideTimer);
        updateSuggestions();
    });

    input.addEventListener("blur", () => {
        scheduleHide();
    });

    input.addEventListener("keydown", (event) => {
        if (tooltip.style.display !== "block" || flatOptions.length === 0) return;
        if (event.key === "ArrowDown") {
            event.preventDefault();
            activeIndex = (activeIndex + 1) % flatOptions.length;
            updateActiveItem();
        } else if (event.key === "ArrowUp") {
            event.preventDefault();
            activeIndex = (activeIndex - 1 + flatOptions.length) % flatOptions.length;
            updateActiveItem();
        } else if (event.key === "Enter" || event.key === "Tab") {
            event.preventDefault();
            const option = flatOptions[activeIndex];
            if (option) {
                applySuggestion(option);
            }
        }
    });

    updateSuggestions();
}

export function commitPipelineStageEdit(editor) {
    const state = editor.pipelineEditState;
    if (!state?.node || !state.slot) return;
    const trimmed = String(state.draftExpression || "").trim();
    applyPipelineStageExpression(editor, state.node, state.slot, state.draftExpression);
    editor.pipelineEditState.node = null;
    editor.pipelineEditState.slot = null;
    editor.pipelineEditState.draftExpression = "";
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
    if (!editor.pipelineEditState) return;
    editor.pipelineEditState.node = null;
    editor.pipelineEditState.slot = null;
    editor.pipelineEditState.draftExpression = "";
    editor.renderEditor();
}

export function applyPipelineStageExpression(editor, stageNode, slot, expression) {
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

    const ast = editor.buildAstFromExpression(trimmed);
    if (!ast) return;
    const encapsulated = editor.createEncapsulatedFrom(ast);
    if (slot === "filterExpression") {
        stageNode.setFilterExpression(encapsulated);
    } else if (slot === "sortExpression") {
        stageNode.setSortExpression(encapsulated);
    } else if (slot === "countExpression") {
        stageNode.setCountExpression(encapsulated);
    }
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
        exprNode = stage.node.countExpression;
        expectedType = "Int32";
    }

    if (!exprNode || !expectedType) return null;
    const actualType = editor.calculateASTReturnType(exprNode);
    if (!actualType) return null;

    let hasIssue = false;
    if (expectedType === "Int32") {
        hasIssue = !editor.isNumericType(actualType);
    } else if (!editor.areTypesCompatible(actualType, expectedType)) {
        hasIssue = true;
    }

    if (!hasIssue) return null;
    return { expectedType, actualType };
}

export function insertPipelineStage(editor, stageType, insertIndex = null) {
    const pipeline = getPipelineContext(editor);
    if (!pipeline || !pipeline.source) return;
    const newNode = createStageNode(stageType, pipeline.source);
    if (!newNode) return;

    const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
    const index = typeof insertIndex === "number" ? insertIndex : stages.length;
    stages.splice(index, 0, { type: stageType, node: newNode });

    const newRoot = rebuildListChainFromStages(pipeline.source, stages);
    editor.workspaceAST = newRoot;
    editor.astRoot = newRoot;
    editor.maintainASTRelationships(editor.astRoot);
    editor.updateExpressionFromAST();
    editor.renderEditor();
}

export function removePipelineStage(editor, index) {
    const pipeline = getPipelineContext(editor);
    if (!pipeline) return;
    const stages = Array.isArray(pipeline.stages) ? [...pipeline.stages] : [];
    if (index < 0 || index >= stages.length) return;
    stages.splice(index, 1);

    const newRoot = stages.length > 0
        ? rebuildListChainFromStages(pipeline.source, stages)
        : pipeline.source;

    editor.workspaceAST = newRoot;
    editor.astRoot = newRoot;
    editor.maintainASTRelationships(editor.astRoot);
    editor.updateExpressionFromAST();
    editor.renderEditor();
}

export function movePipelineStage(editor, fromIndex, toIndex) {
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
    editor.astRoot = newRoot;
    editor.maintainASTRelationships(editor.astRoot);
    editor.updateExpressionFromAST();
    editor.renderEditor();
}
