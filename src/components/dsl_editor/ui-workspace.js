// Phase B - 工作区 UI 渲染（第二阶段：方法级拆分迁移）
import { renderPipelineEditorUI } from "./ui-pipeline.js";
import { renderFavoritesToolbar } from "./ui-favorites.js";
import { findIncompleteNode } from "./core.js";
export { renderPipelineEditorUI };

export function renderWorkspaceUI(editor, container) {
  // 标题
  renderWorkspaceHeader(editor, container);

  // 工作区面板
  const workspaceContainer = document.createElement("div");
  workspaceContainer.className = "dsl-workspace";
  if (editor.isPipelineInline) {
    workspaceContainer.classList.add("dsl-workspace--pipeline");
  }
  workspaceContainer.style.cssText = editor.isPipelineInline
    ? `
      border: 1px solid #90caf9;
      border-radius: 4px;
      padding: 12px;
      background: #e8f4ff;
      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      margin-bottom: 8px;
    `
    : `
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
  if (!editor.isPipelineInline) {
    renderExpressionPreviewUI(editor, workspaceContainer);
  }

  container.appendChild(workspaceContainer);
}

export function renderWorkspaceHeader(editor, container) {
  const workspaceHeader = document.createElement("div");
  workspaceHeader.className = "workspace-header";
  if (editor.isPipelineInline) {
    workspaceHeader.classList.add("workspace-header--pipeline");
  }
  workspaceHeader.style.cssText = editor.isPipelineInline
    ? `
      background: #e8f4ff;
      border: 1px solid #bbdefb;
      border-radius: 4px;
      padding: 8px 12px;
      margin-bottom: 12px;
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    `
    : `
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
  const top =
    typeof editor.peekEditingPath === "function" ? editor.peekEditingPath() : null;
  if (editor.isPipelineInline) {
    text.textContent = "筛选编辑";
  } else {
    text.textContent = top?.descriptor ? `工作区：${top.descriptor}` : "工作区：构建新表达式";
  }
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
  const expectedFromPath = top?.parentNode
    ? editor.getExpectedTypeForSlot(top.parentNode, top.slot, top.index)
    : null;
  const expectedFromFocus = editor.isLightWorkspace && editor.lightWorkspaceFocus?.parentNode
    ? editor.getExpectedTypeForSlot(
      editor.lightWorkspaceFocus.parentNode,
      editor.lightWorkspaceFocus.slot,
      editor.lightWorkspaceFocus.index
    )
    : null;
  const expectedFromIncomplete = editor.isLightWorkspace
    ? resolveExpectedTypeFromIncompleteNode(editor)
    : null;
  const expected = expectedFromFocus
    || expectedFromPath
    || expectedFromIncomplete
    || editor.slotExpectedType
    || editor.expectedType
    || null;
  if (editor.isPipelineInline) {
    hint.textContent = expected ? `目标类型：${editor.getDisplayTypeName(expected)}` : "从当前项开始构建公式";
  } else {
    hint.textContent = expected ? `期望类型：${editor.getDisplayTypeName(expected)}` : "完成后将添加到上方的已完成表达式中";
  }
  hint.style.cssText = `
    font-size: 12px;
    color: #856404;
  `;

  const statusInfo = editor.getParseStatusInfo ? editor.getParseStatusInfo() : null;
  if (!editor.isPipelineInline && statusInfo) {
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
  if (!editor.isPipelineInline) {
    rightGroup.appendChild(renderFavoritesToolbar(editor));
  }
  workspaceHeader.appendChild(rightGroup);
  container.appendChild(workspaceHeader);
}

function resolveExpectedTypeFromIncompleteNode(editor) {
  if (!editor?.workspaceAST || typeof editor.getExpectedTypeForSlot !== "function") return null;
  const incomplete = findIncompleteNode(editor.workspaceAST);
  if (!incomplete) return null;

  if (incomplete.type === "value" && incomplete.metadata?.type === "empty") {
    const info = editor.resolveEmptySlot?.(incomplete.parent, incomplete);
    if (info?.slot) {
      return editor.getExpectedTypeForSlot(incomplete.parent, info.slot, info.index);
    }
  }

  if (incomplete.type === "operator") {
    const slot = !incomplete.leftOperand ? "left" : !incomplete.rightOperand ? "right" : null;
    if (slot) {
      return editor.getExpectedTypeForSlot(incomplete, slot);
    }
  }

  if (incomplete.type === "functionCall") {
    const args = incomplete.args || [];
    const params = incomplete.parameters || [];
    let index = params.findIndex((p, i) => !p?.isOptional && !args[i]);
    if (index < 0) {
      index = args.findIndex(arg => !arg);
    }
    if (index >= 0) {
      return editor.getExpectedTypeForSlot(incomplete, "arg", index);
    }
  }

  if (incomplete.type === "listQuery") {
    if (!incomplete.filterExpression || incomplete.filterExpression.metadata?.type === "empty") {
      return editor.getExpectedTypeForSlot(incomplete, "filterExpression");
    }
  }
  if (incomplete.type === "listSort") {
    if (!incomplete.sortExpression || incomplete.sortExpression.metadata?.type === "empty") {
      return editor.getExpectedTypeForSlot(incomplete, "sortExpression");
    }
  }
  if (incomplete.type === "listTake") {
    if (!incomplete.countExpression || incomplete.countExpression.metadata?.type === "empty") {
      return editor.getExpectedTypeForSlot(incomplete, "countExpression");
    }
  }
  if (incomplete.type === "listDistinct") {
    if (!incomplete.keyExpression || incomplete.keyExpression.metadata?.type === "empty") {
      return editor.getExpectedTypeForSlot(incomplete, "keyExpression");
    }
  }
  if (incomplete.type === "listSelect") {
    if (!incomplete.selectExpression || incomplete.selectExpression.metadata?.type === "empty") {
      return editor.getExpectedTypeForSlot(incomplete, "selectExpression");
    }
  }

  return null;
}



export function renderNodeChainUI(editor, container) {
  if (!editor.workspaceAST) {
    const placeholder = document.createElement("span");
    placeholder.className = "dsl-placeholder";
    placeholder.textContent = "点击 [+] 开始构建表达式";
    placeholder.style.color = "#6c757d";
    placeholder.style.fontSize = "12px";
    placeholder.style.fontWeight = "500";
    container.appendChild(placeholder);
  } else {
    // 关键修复：使用 .call() 来确保 renderASTNodeRecursive 函数内部的 'this' 上下文正确指向 editor 实例
    editor.renderASTNodeRecursive.call(editor, editor.workspaceAST, container);
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

  // 筛选编辑：仅保留 [+] 与 [−]
  if (editor.isPipelineInline) {
    if (editor.workspaceAST || editor.awaitingRightOperand) {
      const clearBtn = document.createElement("button");
      clearBtn.className = "dsl-clear-btn";
      clearBtn.textContent = "[×]";
      clearBtn.title = "清空当前表达式";

      clearBtn.addEventListener("click", (e) => {
        e.preventDefault();
        editor.clearWorkspace(true);
      });

      buttonContainer.appendChild(clearBtn);
    }

    if (editor.workspaceAST || editor.isSubEditor) {
      const backBtn = document.createElement("button");
      backBtn.className = "dsl-back-btn";
      backBtn.textContent = "[−]";
      backBtn.title = "退回上级";

      if (editor.isSubEditor && !editor.workspaceAST) {
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

    container.appendChild(buttonContainer);
    return;
  }

  // 清空按钮（新增）
  if (editor.workspaceAST || editor.awaitingRightOperand) {
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
  if (editor.workspaceAST || editor.isSubEditor) {
    const backBtn = document.createElement("button");
    backBtn.className = "dsl-back-btn";
    backBtn.textContent = "[−]";
    backBtn.title = "退回上级";

    if (editor.isSubEditor && !editor.workspaceAST) {
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
  if (editor.workspaceAST) {
    // “提升层级”按钮：只要当前工作区表达式完整即可显示
    const isWorkspaceComplete = !findIncompleteNode(editor.workspaceAST);
    if (isWorkspaceComplete) {
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
