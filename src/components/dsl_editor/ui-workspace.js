// Phase B - 工作区 UI 渲染（第二阶段：方法级拆分迁移）

export function renderWorkspaceUI(editor, container) {
  // 标题
  editor.renderWorkspaceHeader(container);

  // 工作区面板
  const workspaceContainer = document.createElement("div");
  workspaceContainer.className = "dsl-workspace";
  workspaceContainer.style.cssText = `
    border: 1px solid #ffc107;
    border-radius: 4px;
    padding: 12px;
    background: #fff3cd;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    margin-bottom: 8px;
  `;

  // 节点容器
  const nodesContainer = document.createElement("div");
  nodesContainer.className = "dsl-nodes-container";
  workspaceContainer.appendChild(nodesContainer);

  // 渲染节点链/按钮/预览（模块化）
  renderNodeChainUI(editor, nodesContainer);
  renderPipelineEditorUI(editor, nodesContainer);
  renderAddButtonUI(editor, nodesContainer);
  renderExpressionPreviewUI(editor, workspaceContainer);

  container.appendChild(workspaceContainer);
}

export function renderPipelineEditorUI(editor, container) {
  const pipeline = typeof editor.getPipelineContext === "function" ? editor.getPipelineContext() : null;
  if (!pipeline) return;
  const stages = Array.isArray(pipeline.stages) ? pipeline.stages : [];
  if (stages.length === 0 && !pipeline.allowEmpty) return;

  const panel = document.createElement("div");
  panel.className = "dsl-pipeline-editor";
  panel.style.cssText = `
    margin-top: 10px;
    padding: 10px;
    border: 1px solid #90caf9;
    border-radius: 6px;
    background: #e3f2fd;
  `;

  const header = document.createElement("div");
  header.style.cssText = "display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;";

  const title = document.createElement("div");
  title.textContent = "Pipeline 编辑";
  title.style.cssText = "font-weight: 700; color: #1565c0; font-size: 13px;";
  header.appendChild(title);

  panel.appendChild(header);

  const stagesContainer = document.createElement("div");
  stagesContainer.style.cssText = "display: flex; flex-direction: column; gap: 6px;";

  stages.forEach((stage, index) => {
    const isRawStage = stage.type === "raw";
    const isEditing = typeof editor.isPipelineStageEditing === "function"
      ? editor.isPipelineStageEditing(stage)
      : false;
    const stageRow = document.createElement("div");
    const typeIssue = typeof editor.getPipelineStageTypeIssue === "function"
      ? editor.getPipelineStageTypeIssue(stage)
      : null;
    stageRow.style.cssText = `
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 8px;
      background: ${typeIssue ? "#ffebee" : "#ffffff"};
      border: 1px solid ${typeIssue ? "#ef5350" : "#bbdefb"};
      border-radius: 6px;
    `;
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
      stageRow.style.borderColor = "#42a5f5";
    });

    stageRow.addEventListener("dragleave", () => {
      stageRow.style.borderColor = "#bbdefb";
    });

    stageRow.addEventListener("drop", (event) => {
      if (isRawStage) return;
      event.preventDefault();
      stageRow.style.borderColor = "#bbdefb";
      const fromIndex = Number(event.dataTransfer?.getData("text/plain"));
      const toIndex = Number(stageRow.dataset.index);
      if (!Number.isNaN(fromIndex) && !Number.isNaN(toIndex)) {
        editor.movePipelineStage(fromIndex, toIndex);
      }
    });

    const badge = document.createElement("span");
    badge.textContent = editor.getPipelineStageLabel(stage);
    badge.style.cssText = `
      padding: 2px 6px;
      border-radius: 10px;
      background: ${typeIssue ? "#ef9a9a" : "#bbdefb"};
      color: ${typeIssue ? "#b71c1c" : "#0d47a1"};
      font-size: 11px;
      font-weight: 700;
    `;
    stageRow.appendChild(badge);

    const summary = document.createElement("div");
    summary.textContent = editor.getPipelineStageSummary(stage);
    summary.style.cssText = "flex: 1; font-size: 12px; color: #1a237e;";
    stageRow.appendChild(summary);

    const editBtn = document.createElement("button");
    editBtn.textContent = isEditing ? "收起" : "编辑";
    editBtn.style.cssText = isRawStage
      ? "border: none; background: #cfd8dc; color: #607d8b; padding: 4px 8px; border-radius: 4px; font-size: 12px;"
      : "border: none; background: #1976d2; color: #fff; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 12px;";
    if (!isRawStage) {
      editBtn.addEventListener("click", (event) => {
        event.preventDefault();
        editor.editPipelineStage(stage);
      });
    } else {
      editBtn.disabled = true;
    }
    stageRow.appendChild(editBtn);

    const removeBtn = document.createElement("button");
    removeBtn.textContent = "移除";
    removeBtn.style.cssText = isRawStage
      ? "border: none; background: #ef9a9a; color: #8e0000; padding: 4px 8px; border-radius: 4px; font-size: 12px;"
      : "border: none; background: #e53935; color: #fff; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 12px;";
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
      editPanel.style.cssText = `
        margin-left: 20px;
        padding: 8px;
        border: 1px dashed #90caf9;
        border-radius: 6px;
        background: #f8fbff;
      `;

      const actionRow = document.createElement("div");
      actionRow.style.cssText = "display: flex; gap: 6px; justify-content: flex-end; margin-bottom: 6px;";

      const applyBtn = document.createElement("button");
      applyBtn.textContent = "应用";
      applyBtn.style.cssText = "border: none; background: #43a047; color: #fff; padding: 4px 10px; border-radius: 4px; font-size: 12px; cursor: pointer;";
      applyBtn.addEventListener("click", (event) => {
        event.preventDefault();
        editor.commitPipelineStageEdit();
      });
      actionRow.appendChild(applyBtn);

      const cancelBtn = document.createElement("button");
      cancelBtn.textContent = "取消";
      cancelBtn.style.cssText = "border: none; background: #cfd8dc; color: #455a64; padding: 4px 10px; border-radius: 4px; font-size: 12px; cursor: pointer;";
      cancelBtn.addEventListener("click", (event) => {
        event.preventDefault();
        editor.cancelPipelineStageEdit();
      });
      actionRow.appendChild(cancelBtn);

      const editorHost = document.createElement("div");
      editorHost.style.cssText = "background: #ffffff; border-radius: 6px; padding: 6px;";

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
    emptyState.style.cssText = "font-size: 12px; color: #607d8b; padding: 6px 4px;";
    emptyState.textContent = "尚未添加筛选阶段，可点击下方按钮添加。";
    stagesContainer.appendChild(emptyState);
  }

  panel.appendChild(stagesContainer);

  const addRow = document.createElement("div");
  addRow.style.cssText = "display: flex; gap: 6px; margin-top: 8px;";
  const canAddStage = Boolean(pipeline.source);

  const addFilter = document.createElement("button");
  addFilter.textContent = "+ 筛选";
  addFilter.style.cssText = canAddStage
    ? "border: 1px solid #64b5f6; background: #e3f2fd; color: #1565c0; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 12px;"
    : "border: 1px solid #cfd8dc; background: #eceff1; color: #90a4ae; padding: 4px 8px; border-radius: 4px; font-size: 12px;";
  if (canAddStage) {
    addFilter.addEventListener("click", (event) => {
      event.preventDefault();
      editor.insertPipelineStage("filter");
    });
  } else {
    addFilter.disabled = true;
    addFilter.title = "原始表达式无法拆分为阶段";
  }
  addRow.appendChild(addFilter);

  const addSort = document.createElement("button");
  addSort.textContent = "+ 排序";
  addSort.style.cssText = canAddStage
    ? "border: 1px solid #64b5f6; background: #e3f2fd; color: #1565c0; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 12px;"
    : "border: 1px solid #cfd8dc; background: #eceff1; color: #90a4ae; padding: 4px 8px; border-radius: 4px; font-size: 12px;";
  if (canAddStage) {
    addSort.addEventListener("click", (event) => {
      event.preventDefault();
      editor.insertPipelineStage("sort");
    });
  } else {
    addSort.disabled = true;
    addSort.title = "原始表达式无法拆分为阶段";
  }
  addRow.appendChild(addSort);

  const addSelect = document.createElement("button");
  addSelect.textContent = "+ 选择";
  addSelect.style.cssText = canAddStage
    ? "border: 1px solid #64b5f6; background: #e3f2fd; color: #1565c0; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 12px;"
    : "border: 1px solid #cfd8dc; background: #eceff1; color: #90a4ae; padding: 4px 8px; border-radius: 4px; font-size: 12px;";
  if (canAddStage) {
    addSelect.addEventListener("click", (event) => {
      event.preventDefault();
      editor.insertPipelineStage("select");
    });
  } else {
    addSelect.disabled = true;
    addSelect.title = "原始表达式无法拆分为阶段";
  }
  addRow.appendChild(addSelect);

  const disabledPost = document.createElement("button");
  disabledPost.textContent = "+ 后处理";
  disabledPost.disabled = true;
  disabledPost.title = "当前DSL尚不支持后处理阶段";
  disabledPost.style.cssText = "border: 1px solid #cfd8dc; background: #eceff1; color: #90a4ae; padding: 4px 8px; border-radius: 4px; font-size: 12px;";
  addRow.appendChild(disabledPost);

  panel.appendChild(addRow);
  container.appendChild(panel);
}

export function renderNodeChainUI(editor, container) {
  if (!editor.astRoot) {
    const placeholder = document.createElement("span");
    placeholder.className = "dsl-placeholder";
    placeholder.textContent = "点击 [+] 开始构建表达式";
    placeholder.style.color = "#6c757d";
    placeholder.style.fontSize = "12px";
    placeholder.style.fontWeight = "500";
    container.appendChild(placeholder);
  } else {
    // 关键修复：使用 .call() 来确保 renderASTNodeRecursive 函数内部的 'this' 上下文正确指向 editor 实例
    editor.renderASTNodeRecursive.call(editor, editor.astRoot, container);
  }
}

export function renderAddButtonUI(editor, container) {
  const buttonContainer = document.createElement("div");
  buttonContainer.style.cssText = "display: inline-flex; gap: 4px; margin-left: 8px;";

  // 添加节点按钮
  const addBtn = document.createElement("button");
  addBtn.className = "dsl-add-btn";
  addBtn.textContent = "[+]";
  addBtn.title = "添加节点";

  addBtn.addEventListener("click", (e) => {
    e.preventDefault();
    editor.showAddNodeMenu(addBtn);
  });

  buttonContainer.appendChild(addBtn);

  // 清空按钮（新增）
  if (editor.astRoot || editor.awaitingRightOperand) {
    const clearBtn = document.createElement("button");
    clearBtn.className = "dsl-clear-btn";
    clearBtn.textContent = "[×]";
    clearBtn.title = editor.awaitingRightOperand ? "取消操作符，返回上一级" : "清空当前表达式";

    clearBtn.addEventListener("click", (e) => {
      e.preventDefault();
      editor.clearWorkspace();
    });

    buttonContainer.appendChild(clearBtn);
  }

  // 退回上级按钮（仅在有节点时显示，或者是子编辑器时总是显示）
  if (editor.astRoot || editor.isSubEditor) {
    const backBtn = document.createElement("button");
    backBtn.className = "dsl-back-btn";
    backBtn.textContent = "[−]";
    backBtn.title = "退回上级";

    if (editor.isSubEditor && !editor.astRoot) {
      backBtn.title = "退出筛选编辑器";
      backBtn.addEventListener("click", (e) => {
        e.preventDefault();
        editor.exitSubEditor();
      });
    } else {
      backBtn.addEventListener("click", (e) => {
        e.preventDefault();
        editor.goBack();
      });
    }

    buttonContainer.appendChild(backBtn);
  }

  // 完成表达式按钮（有AST时始终显示）
  if (editor.astRoot) {
    // 新增：“提升层级”按钮
    // 只有当工作区是一个可封装的值节点时才显示
    if (editor.workspaceAST && editor.workspaceAST.type === 'value') {
        const elevateBtn = document.createElement("button");
        elevateBtn.className = "dsl-elevate-btn";
        elevateBtn.textContent = "()";
        elevateBtn.title = "将当前项创建为独立表达式";

        elevateBtn.addEventListener("click", (e) => {
            e.preventDefault();
            editor.handleElevateExpression(editor.workspaceAST);
        });

        buttonContainer.appendChild(elevateBtn);
    }

    const completeBtn = document.createElement("button");
    completeBtn.className = "dsl-complete-btn";
    completeBtn.textContent = "[✓]";
    completeBtn.title = "保存当前层（封装并返回上一级）";

    completeBtn.addEventListener("click", (e) => {
      e.preventDefault();
      editor.saveWorkspace(); // 新架构：调用简化后的保存方法
      // saveWorkspace 内部会调用 renderEditor，所以这里不再需要
    });

    buttonContainer.appendChild(completeBtn);

    // 移除 Done 按钮 - 封装完最上层表达式即视为完成
  }

  container.appendChild(buttonContainer);
}

export function renderExpressionPreviewUI(editor, container) {
  const preview = document.createElement("div");
  preview.className = "expression-preview";

  const label = document.createElement("label");
  label.textContent = "DSL表达式:";
  label.style.cssText = "font-size: 12px; color: #2c3e50; font-weight: 600; margin-right: 8px;";

  const code = document.createElement("code");
  code.textContent = editor.expression || "(空)";
  code.style.cssText = "font-size: 12px; color: #495057; background: #f8f9fa; padding: 2px 4px; border-radius: 3px;";

  preview.appendChild(label);
  preview.appendChild(code);
  container.appendChild(preview);

  const readable = document.createElement("div");
  readable.className = "expression-readable-preview";
  readable.style.cssText = "margin-top: 6px; font-size: 12px; color: #495057;";

  const readableLabel = document.createElement("span");
  readableLabel.textContent = "可读化预览: ";
  readableLabel.style.cssText = "font-weight: 600; margin-right: 6px; color: #2c3e50;";

  const readableText = document.createElement("span");
  readableText.textContent = typeof editor.getReadablePreviewText === "function" ? editor.getReadablePreviewText() : "(空)";

  readable.appendChild(readableLabel);
  readable.appendChild(readableText);
  container.appendChild(readable);
}
