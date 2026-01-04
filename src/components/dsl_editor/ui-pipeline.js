import { PIPELINE_STAGE_META, extractListPipeline, isListOperationNode } from "./pipeline.js";

export function renderPipelineBlock(editor, node, container, renderNodeFn) {
    const pipeline = extractListPipeline(node);
    if (!pipeline || !pipeline.source) return false;

    const pipelineWrapper = document.createElement("span");
    pipelineWrapper.className = "dsl-pipeline-wrap";

    const sourceIsEncapsulated = pipeline.source?.type === 'value' && pipeline.source?.metadata?.type === 'encapsulated';
    if (sourceIsEncapsulated) {
        renderNodeFn(pipeline.source, pipelineWrapper);
    } else {
        const sourceWrapper = document.createElement("span");
        sourceWrapper.className = "dsl-pipeline-source";
        renderNodeFn(pipeline.source, sourceWrapper);
        pipelineWrapper.appendChild(sourceWrapper);
    }

    const pipelineBadge = document.createElement("span");
    const stageCount = pipeline.stages.length;
    pipelineBadge.textContent = stageCount > 0 ? `管线 · ${stageCount}步` : "管线";
    pipelineBadge.className = "dsl-pipeline-badge";
    pipelineWrapper.appendChild(pipelineBadge);

    pipelineWrapper.addEventListener("click", (event) => {
        event.stopPropagation();
        editor.pipelineOpen = true;
        editor.pipelineOpenSource = pipeline.source || null;
        editor.renderEditor();
    });

    container.appendChild(pipelineWrapper);
    return true;
}

export function renderListQueryNodeAST(editor, node, container) {
    if (node.parent && isListOperationNode(node.parent)) {
        return;
    }
    const rendered = renderPipelineBlock(editor, node, container, editor.renderASTNodeRecursive.bind(editor));
    if (rendered) return;

    const queryContainer = document.createElement("div");
    queryContainer.className = "dsl-list-query-container";

    if (node.source) {
        editor.renderASTNodeRecursive(node.source, queryContainer);
    }

    const queryElement = document.createElement("div");
    queryElement.className = "dsl-list-query-operator";
    let operatorName = node.type.replace('list', '').toLowerCase();
    if (node.type === 'listSort') {
        operatorName = 'orderBy';
    }
    queryElement.textContent = `.${operatorName}`;

    const arrowLeft = document.createElement("div");
    arrowLeft.className = "dsl-list-query-arrow dsl-list-query-arrow--left";

    const arrowRight = document.createElement("div");
    arrowRight.className = "dsl-list-query-arrow dsl-list-query-arrow--right";

    queryElement.appendChild(arrowLeft);
    queryElement.appendChild(arrowRight);
    queryContainer.appendChild(queryElement);

    let expressionNode = null;
    if (node.type === 'listQuery') {
        expressionNode = node.filterExpression;
    } else if (node.type === 'listSort') {
        expressionNode = node.sortExpression;
    } else if (node.type === 'listTake') {
        expressionNode = node.countExpression;
    }

    if (expressionNode) {
        editor.renderASTNodeRecursive(expressionNode, queryContainer);
    } else {
        const slotName = node.type === 'listQuery'
            ? 'filterExpression'
            : node.type === 'listSort'
                ? 'sortExpression'
                : 'countExpression';
        editor.renderEmptySlotPlaceholder(node, slotName, queryContainer);
    }

    container.appendChild(queryContainer);
}

export function renderPipelineEditorUI(editor, container) {
    const pipeline = typeof editor.getPipelineContext === "function" ? editor.getPipelineContext() : null;
    if (!pipeline) return;
    const stages = Array.isArray(pipeline.stages) ? pipeline.stages : [];
    if (stages.length === 0 && !pipeline.allowEmpty) return;

    const panel = document.createElement("div");
    panel.className = "dsl-pipeline-editor";

    const header = document.createElement("div");
    header.className = "dsl-pipeline-editor-header";

    const title = document.createElement("div");
    title.textContent = "Pipeline 编辑";
    title.className = "dsl-pipeline-editor-title";
    header.appendChild(title);

    panel.appendChild(header);

    const stagesContainer = document.createElement("div");
    stagesContainer.className = "dsl-pipeline-stage-list";

    stages.forEach((stage, index) => {
        const isRawStage = stage.type === "raw";
        const isEditing = typeof editor.isPipelineStageEditing === "function"
            ? editor.isPipelineStageEditing(stage)
            : false;
        const stageRow = document.createElement("div");
        const typeIssue = typeof editor.getPipelineStageTypeIssue === "function"
            ? editor.getPipelineStageTypeIssue(stage)
            : null;
        stageRow.className = "dsl-pipeline-stage-row";
        if (typeIssue) {
            stageRow.classList.add("has-issue");
        }
        if (typeIssue) {
            stageRow.title = `类型不匹配：期望 ${typeIssue.expectedType}，实际 ${typeIssue.actualType}`;
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
        editBtn.textContent = isEditing ? "收起" : "编辑";
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

        if (isEditing) {
            const editPanel = document.createElement("div");
            editPanel.className = "dsl-pipeline-edit-panel";

            const actionRow = document.createElement("div");
            actionRow.className = "dsl-pipeline-edit-actions";

            const applyBtn = document.createElement("button");
            applyBtn.textContent = "应用";
            applyBtn.className = "dsl-pipeline-btn dsl-pipeline-btn--success";
            applyBtn.addEventListener("click", (event) => {
                event.preventDefault();
                editor.commitPipelineStageEdit();
            });
            actionRow.appendChild(applyBtn);

            const cancelBtn = document.createElement("button");
            cancelBtn.textContent = "取消";
            cancelBtn.className = "dsl-pipeline-btn dsl-pipeline-btn--neutral";
            cancelBtn.addEventListener("click", (event) => {
                event.preventDefault();
                editor.cancelPipelineStageEdit();
            });
            actionRow.appendChild(cancelBtn);

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
                    { value: "random", label: "随机" }
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

                editPanel.appendChild(strategyRow);
            }

            const editorHost = document.createElement("div");
            editorHost.className = "dsl-pipeline-edit-host";

            editPanel.appendChild(actionRow);
            editPanel.appendChild(editorHost);
            stagesContainer.appendChild(editPanel);

            if (typeof editor.renderPipelineStageInlineEditor === "function") {
                editor.renderPipelineStageInlineEditor(stage, editorHost);
            }
        }
    });

    if (stages.length === 0) {
        const emptyState = document.createElement("div");
        emptyState.className = "dsl-pipeline-empty-state";
        emptyState.textContent = "尚未添加筛选阶段，可点击下方按钮添加。";
        stagesContainer.appendChild(emptyState);
    }

    panel.appendChild(stagesContainer);

    const addRow = document.createElement("div");
    addRow.className = "dsl-pipeline-add-row";
    const canAddStage = Boolean(pipeline.source);

    const addFilter = document.createElement("button");
    addFilter.textContent = "+ 筛选";
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

    const disabledPost = document.createElement("button");
    disabledPost.textContent = "+ 后处理";
    disabledPost.disabled = true;
    disabledPost.title = "当前DSL尚不支持后处理阶段";
    disabledPost.className = "dsl-pipeline-add-btn is-disabled";
    addRow.appendChild(disabledPost);

    panel.appendChild(addRow);
    container.appendChild(panel);
}
