import { PIPELINE_STAGE_META, extractListPipeline, isListOperationNode } from "./pipeline.js";

function unmountPipelineStageModal(editor) {
    const overlay = editor?.pipelineStageModalOverlay;
    if (overlay?.parentNode) {
        overlay.parentNode.removeChild(overlay);
    }
    if (editor) {
        editor.pipelineStageModalOverlay = null;
    }
}

function mountPipelineStageModal(editor, overlay) {
    if (!editor || !overlay || typeof document === "undefined" || !document.body) {
        return;
    }
    if (editor.pipelineStageModalOverlay === overlay && overlay.parentNode === document.body) {
        return;
    }
    unmountPipelineStageModal(editor);
    document.body.appendChild(overlay);
    editor.pipelineStageModalOverlay = overlay;
}

function requestCancelPipelineStageModal(editor) {
    unmountPipelineStageModal(editor);
    editor.cancelPipelineStageEdit();
}

function requestApplyPipelineStageModal(editor) {
    editor.commitPipelineStageEdit();
    if (!editor?.pipelineEditState?.node) {
        unmountPipelineStageModal(editor);
    }
}

export function renderPipelineBlock(editor, node, container, renderNodeFn, options = {}) {
    const pipeline = extractListPipeline(node);
    if (!pipeline || !pipeline.source) return false;

    const interactionMode = options?.interactionMode === "main" ? "main" : "workspace";
    const isMainExpression = interactionMode === "main";
    const pipelineWrap = document.createElement("span");
    pipelineWrap.className = "dsl-pipeline-wrap";
    pipelineWrap.classList.add(isMainExpression ? "dsl-pipeline-wrap--main" : "dsl-pipeline-wrap--workspace");

    // 仅用于展示分组：前置路径保持常规表达式样式
    const pipelineSource = document.createElement("span");
    pipelineSource.className = "dsl-pipeline-source";
    if (typeof renderNodeFn === "function") {
        renderNodeFn(pipeline.source, pipelineSource);
    }
    pipelineWrap.appendChild(pipelineSource);

    const pipelineBadge = document.createElement("span");
    const stageCount = pipeline.stages.length;
    pipelineBadge.textContent = stageCount > 0 ? `管线 · ${stageCount}步` : "管线";
    pipelineBadge.className = "dsl-pipeline-badge";

    if (isMainExpression) {
        pipelineBadge.dataset.dslNode = "true";
        pipelineBadge.__dslNode = node;
        pipelineBadge.classList.toggle("is-selected", editor.selectedNode === node);
        pipelineBadge.addEventListener("click", (event) => {
            event.stopPropagation();
            editor.selectedNode = node;
            editor.renderEditor();
            if (typeof editor.editSubExpression === "function") {
                editor.editSubExpression(node);
            }
        });
    } else {
        pipelineBadge.addEventListener("click", (event) => {
            event.stopPropagation();
            if (typeof editor.syncPipelineContext === "function") {
                editor.syncPipelineContext();
            }
            const nextSource = pipeline.source || null;
            const sourceChanged = editor.pipelineOpenSource !== nextSource;
            editor.pipelineOpen = true;
            editor.pipelineOpenMode = null;
            editor.pipelineOpenSource = nextSource;
            if (sourceChanged && editor.pipelineEditState) {
                editor.pipelineEditState.node = null;
                editor.pipelineEditState.slot = null;
                editor.pipelineEditState.draftExpression = "";
                editor.pipelineEditState.draftCountValue = "";
            }
            if (typeof editor.markPipelineContextDirty === "function") {
                editor.markPipelineContextDirty();
            }
            editor.renderEditor();
        });
    }

    pipelineWrap.appendChild(pipelineBadge);
    container.appendChild(pipelineWrap);
    return true;
}

export function renderListQueryNodeAST(editor, node, container) {
    if (node.parent && isListOperationNode(node.parent)) {
        return;
    }
    const rendered = renderPipelineBlock(editor, node, container, editor.renderASTNodeRecursive.bind(editor), {
        interactionMode: "workspace"
    });
    if (rendered) return;

    if (node.source) {
        editor.renderASTNodeRecursive(node.source, container);
    }
    const queryElement = document.createElement("span");
    queryElement.className = "dsl-list-operator";
    let operatorName = node.type.replace('list', '').toLowerCase();
    if (node.type === 'listSort') {
        operatorName = 'orderBy';
    }
    queryElement.textContent = `.${operatorName}`;
    queryElement.dataset.listOperator = operatorName;
    container.appendChild(queryElement);

    let expressionNode = null;
    if (node.type === 'listQuery') {
        expressionNode = node.filterExpression;
    } else if (node.type === 'listSort') {
        expressionNode = node.sortExpression;
    } else if (node.type === 'listTake') {
        expressionNode = node.countExpression;
    } else if (node.type === 'listDistinct') {
        expressionNode = node.keyExpression;
    } else if (node.type === 'listSelect') {
        expressionNode = node.selectExpression;
    }

    if (expressionNode) {
        editor.renderASTNodeRecursive(expressionNode, container);
    } else {
        const slotName = node.type === 'listQuery'
            ? 'filterExpression'
            : node.type === 'listSort'
                ? 'sortExpression'
                : node.type === 'listDistinct'
                    ? 'keyExpression'
                    : node.type === 'listSelect'
                    ? 'selectExpression'
                    : 'countExpression';
        editor.renderEmptySlotPlaceholder(node, slotName, container);
    }
}

function renderPipelineStageModal(editor, stage) {
    if (!stage) return null;

    const overlay = document.createElement("div");
    overlay.className = "dsl-pipeline-stage-modal-overlay";
    overlay.tabIndex = -1;

    const modal = document.createElement("div");
    modal.className = "dsl-pipeline-stage-modal";
    modal.setAttribute("role", "dialog");
    modal.setAttribute("aria-modal", "true");
    modal.tabIndex = -1;

    const titleBar = document.createElement("div");
    titleBar.className = "dsl-pipeline-stage-modal-header";

    const title = document.createElement("div");
    title.className = "dsl-pipeline-stage-modal-title";
    title.textContent = `编辑阶段 · ${editor.getPipelineStageLabel(stage)}`;
    titleBar.appendChild(title);

    const closeBtn = document.createElement("button");
    closeBtn.type = "button";
    closeBtn.textContent = "关闭";
    closeBtn.className = "dsl-pipeline-btn dsl-pipeline-btn--neutral";
    closeBtn.addEventListener("click", (event) => {
        event.preventDefault();
        requestCancelPipelineStageModal(editor);
    });
    titleBar.appendChild(closeBtn);
    modal.appendChild(titleBar);

    const body = document.createElement("div");
    body.className = "dsl-pipeline-stage-modal-body";

    if (stage.node?.type === "listTake") {
        const strategyRow = document.createElement("div");
        strategyRow.className = "dsl-pipeline-select-row";

        const strategyLabel = document.createElement("span");
        strategyLabel.textContent = "选择方式";
        strategyLabel.className = "dsl-pipeline-select-label";
        strategyRow.appendChild(strategyLabel);

        const options = [
            { value: "first", label: "首个" },
            { value: "last", label: "末尾" },
            { value: "random", label: "随机" },
            { value: "all", label: "全部" }
        ];

        const currentStrategy = String(stage.node.strategy || "first").toLowerCase();
        options.forEach((option) => {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.textContent = option.label;
            btn.className = "dsl-pipeline-strategy-btn";
            if (currentStrategy === option.value) {
                btn.classList.add("is-active");
            }
            btn.addEventListener("click", (event) => {
                event.preventDefault();
                stage.node.setStrategy?.(option.value);
                stage.node.strategy = option.value;
                editor.renderEditor();
            });
            strategyRow.appendChild(btn);
        });
        body.appendChild(strategyRow);
    }

    if (stage.node?.type === "listSort") {
        const directionRow = document.createElement("div");
        directionRow.className = "dsl-pipeline-select-row";

        const directionLabel = document.createElement("span");
        directionLabel.textContent = "排序方向";
        directionLabel.className = "dsl-pipeline-select-label";
        directionRow.appendChild(directionLabel);

        const options = [
            { value: "asc", label: "升序" },
            { value: "desc", label: "降序" }
        ];

        const currentDirection = String(stage.node.direction || "asc").toLowerCase();
        options.forEach((option) => {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.textContent = option.label;
            btn.className = "dsl-pipeline-strategy-btn";
            if (currentDirection === option.value) {
                btn.classList.add("is-active");
            }
            btn.addEventListener("click", (event) => {
                event.preventDefault();
                stage.node.setDirection?.(option.value);
                stage.node.direction = option.value;
                editor.renderEditor();
            });
            directionRow.appendChild(btn);
        });
        body.appendChild(directionRow);
    }

    const editorHost = document.createElement("div");
    editorHost.className = "dsl-pipeline-edit-host dsl-pipeline-stage-modal-host";
    body.appendChild(editorHost);
    modal.appendChild(body);

    const footer = document.createElement("div");
    footer.className = "dsl-pipeline-stage-modal-footer";

    const applyBtn = document.createElement("button");
    applyBtn.type = "button";
    applyBtn.textContent = "应用";
    applyBtn.className = "dsl-pipeline-btn dsl-pipeline-btn--success";
    applyBtn.addEventListener("click", (event) => {
        event.preventDefault();
        requestApplyPipelineStageModal(editor);
    });
    footer.appendChild(applyBtn);

    const cancelBtn = document.createElement("button");
    cancelBtn.type = "button";
    cancelBtn.textContent = "取消";
    cancelBtn.className = "dsl-pipeline-btn dsl-pipeline-btn--neutral";
    cancelBtn.addEventListener("click", (event) => {
        event.preventDefault();
        requestCancelPipelineStageModal(editor);
    });
    footer.appendChild(cancelBtn);

    modal.appendChild(footer);
    overlay.appendChild(modal);

    overlay.addEventListener("click", (event) => {
        if (event.target === overlay) {
            requestCancelPipelineStageModal(editor);
        }
    });

    modal.addEventListener("keydown", (event) => {
        if (event.key !== "Escape") return;
        event.preventDefault();
        event.stopPropagation();
        requestCancelPipelineStageModal(editor);
    });

    if (typeof editor.renderPipelineStageInlineEditor === "function") {
        editor.renderPipelineStageInlineEditor(stage, editorHost);
    }

    // 将焦点放入子弹窗，保证 Esc 与键盘导航可用。
    queueMicrotask(() => {
        const firstAction = applyBtn || modal;
        if (typeof firstAction?.focus === "function") {
            firstAction.focus();
        } else if (typeof modal.focus === "function") {
            modal.focus();
        }
    });

    return overlay;
}

export function renderPipelineEditorUI(editor, container) {
    const pipeline = typeof editor.getPipelineContext === "function" ? editor.getPipelineContext() : null;
    if (!pipeline) {
        unmountPipelineStageModal(editor);
        return;
    }
    const stages = Array.isArray(pipeline.stages) ? pipeline.stages : [];
    if (stages.length === 0 && !pipeline.allowEmpty) {
        unmountPipelineStageModal(editor);
        return;
    }

    const panel = document.createElement("div");
    panel.className = "dsl-pipeline-editor";

    const header = document.createElement("div");
    header.className = "dsl-pipeline-editor-header";

    const title = document.createElement("div");
    title.textContent = "筛选编辑";
    title.className = "dsl-pipeline-editor-title";
    header.appendChild(title);

    panel.appendChild(header);

    const stagesContainer = document.createElement("div");
    stagesContainer.className = "dsl-pipeline-stage-list";
    let editingStage = null;

    stages.forEach((stage, index) => {
        const isRawStage = stage.type === "raw";
        const isEditing = typeof editor.isPipelineStageEditing === "function"
            ? editor.isPipelineStageEditing(stage)
            : false;
        if (isEditing) {
            editingStage = stage;
        }
        const stageRow = document.createElement("div");
        const typeIssue = typeof editor.getPipelineStageTypeIssue === "function"
            ? editor.getPipelineStageTypeIssue(stage)
            : null;
        stageRow.className = "dsl-pipeline-stage-row";
        if (typeIssue) {
            stageRow.classList.add("has-issue");
        }
        if (typeIssue) {
            const expectedDisplay = editor.getDisplayTypeName?.(typeIssue.expectedType) || typeIssue.expectedType;
            const actualDisplay = editor.getDisplayTypeName?.(typeIssue.actualType) || typeIssue.actualType;
            stageRow.title = `类型不匹配：期望 ${expectedDisplay}，实际 ${actualDisplay}`;
        }

        stageRow.draggable = !isRawStage;
        stageRow.dataset.index = String(index);

        stageRow.addEventListener("dragstart", (event) => {
            if (isRawStage) return;
            event.dataTransfer?.setData("text/plain", String(index));
        });

        stageRow.addEventListener("dragover", (event) => {
            if (isRawStage) return;
            event.preventDefault();
            stageRow.classList.add("is-drag-over");
        });

        stageRow.addEventListener("dragleave", () => {
            stageRow.classList.remove("is-drag-over");
        });

        stageRow.addEventListener("drop", (event) => {
            if (isRawStage) return;
            event.preventDefault();
            stageRow.classList.remove("is-drag-over");
            const fromIndex = Number(event.dataTransfer?.getData("text/plain"));
            const toIndex = Number(stageRow.dataset.index);
            if (!Number.isNaN(fromIndex) && !Number.isNaN(toIndex)) {
                editor.movePipelineStage(fromIndex, toIndex);
            }
        });

        const badge = document.createElement("span");
        badge.textContent = editor.getPipelineStageLabel(stage);
        badge.className = "dsl-pipeline-stage-badge";
        if (typeIssue) {
            badge.classList.add("has-issue");
        }
        stageRow.appendChild(badge);

        const summary = document.createElement("div");
        summary.textContent = editor.getPipelineStageSummary(stage);
        summary.className = "dsl-pipeline-stage-summary";
        stageRow.appendChild(summary);

        const editBtn = document.createElement("button");
        editBtn.textContent = isEditing ? "编辑中" : "编辑";
        editBtn.className = "dsl-pipeline-btn";
        if (isRawStage) {
            editBtn.classList.add("dsl-pipeline-btn--muted", "is-disabled");
        } else {
            editBtn.classList.add("dsl-pipeline-btn--primary");
        }
        if (!isRawStage) {
            editBtn.addEventListener("click", (event) => {
                event.preventDefault();
                editor.editPipelineStage(stage, index);
            });
        } else {
            editBtn.disabled = true;
        }
        stageRow.appendChild(editBtn);

        const removeBtn = document.createElement("button");
        removeBtn.textContent = "移除";
        removeBtn.className = "dsl-pipeline-btn";
        if (isRawStage) {
            removeBtn.classList.add("dsl-pipeline-btn--danger-soft", "is-disabled");
        } else {
            removeBtn.classList.add("dsl-pipeline-btn--danger");
        }
        if (!isRawStage) {
            removeBtn.addEventListener("click", (event) => {
                event.preventDefault();
                editor.removePipelineStage(index);
            });
        } else {
            removeBtn.disabled = true;
        }
        stageRow.appendChild(removeBtn);

        stagesContainer.appendChild(stageRow);
    });

    if (stages.length === 0) {
        const emptyState = document.createElement("div");
        emptyState.className = "dsl-pipeline-empty-state";
        emptyState.textContent = "尚未添加查询阶段，可点击下方按钮添加。";
        stagesContainer.appendChild(emptyState);
    }

    panel.appendChild(stagesContainer);

    const addRow = document.createElement("div");
    addRow.className = "dsl-pipeline-add-row";
    const canAddStage = Boolean(pipeline.source);

    const addFilter = document.createElement("button");
    addFilter.textContent = "+ 查询";
    addFilter.className = "dsl-pipeline-add-btn";
    if (canAddStage) {
        addFilter.addEventListener("click", (event) => {
            event.preventDefault();
            editor.insertPipelineStage("filter");
        });
    } else {
        addFilter.classList.add("is-disabled");
        addFilter.disabled = true;
        addFilter.title = "原始表达式无法拆分为阶段";
    }
    addRow.appendChild(addFilter);

    const addSort = document.createElement("button");
    addSort.textContent = "+ 排序";
    addSort.className = "dsl-pipeline-add-btn";
    if (canAddStage) {
        addSort.addEventListener("click", (event) => {
            event.preventDefault();
            editor.insertPipelineStage("sort");
        });
    } else {
        addSort.classList.add("is-disabled");
        addSort.disabled = true;
        addSort.title = "原始表达式无法拆分为阶段";
    }
    addRow.appendChild(addSort);

    const addTake = document.createElement("button");
    addTake.textContent = "+ 取样";
    addTake.className = "dsl-pipeline-add-btn";
    if (canAddStage) {
        addTake.addEventListener("click", (event) => {
            event.preventDefault();
            editor.insertPipelineStage("take");
        });
    } else {
        addTake.classList.add("is-disabled");
        addTake.disabled = true;
        addTake.title = "原始表达式无法拆分为阶段";
    }
    addRow.appendChild(addTake);

    const addDistinct = document.createElement("button");
    addDistinct.textContent = "+ 去重";
    addDistinct.className = "dsl-pipeline-add-btn";
    if (canAddStage) {
        addDistinct.addEventListener("click", (event) => {
            event.preventDefault();
            editor.insertPipelineStage("distinct");
        });
    } else {
        addDistinct.classList.add("is-disabled");
        addDistinct.disabled = true;
        addDistinct.title = "原始表达式无法拆分为阶段";
    }
    addRow.appendChild(addDistinct);

    const addSelect = document.createElement("button");
    addSelect.textContent = "+ 选择";
    addSelect.className = "dsl-pipeline-add-btn";
    if (canAddStage) {
        addSelect.addEventListener("click", (event) => {
            event.preventDefault();
            editor.insertPipelineStage("select");
        });
    } else {
        addSelect.classList.add("is-disabled");
        addSelect.disabled = true;
        addSelect.title = "原始表达式无法拆分为阶段";
    }
    addRow.appendChild(addSelect);

    panel.appendChild(addRow);
    container.appendChild(panel);

    if (editingStage) {
        const modalOverlay = renderPipelineStageModal(editor, editingStage);
        if (modalOverlay) {
            mountPipelineStageModal(editor, modalOverlay);
        }
    } else {
        unmountPipelineStageModal(editor);
    }
}
