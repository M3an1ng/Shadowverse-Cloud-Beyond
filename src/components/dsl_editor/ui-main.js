// Phase B - 主表达式 UI 渲染（胶囊样式 + 当前编辑高亮）

export function renderMainExpressionUI(editor, container) {
  if (!editor.virtualRoot || !editor.virtualRoot.main) return;

  const mainDiv = document.createElement("div");
  mainDiv.className = "main-expression";
  mainDiv.style.cssText = `
    background: #e9ecef;
    border: 1px solid #ced4da;
    border-radius: 4px;
    padding: 8px 12px;
    margin-bottom: 12px;
  `;

  const header = document.createElement("div");
  header.style.cssText = `display:flex; align-items:center; gap:8px; margin-bottom:6px;`;
  const title = document.createElement("span");
  title.textContent = "已完成表达式";
  title.style.cssText = "font-weight:600;color:#495057;";
  header.appendChild(title);

  // 移除"正在编辑"状态提示 - 改为依靠样式框的加粗边框显示选中状态

  const expressionContent = document.createElement("div");
  expressionContent.className = "expression-content";
  expressionContent.style.cssText = `
    background: #f8f9fa;
    border: 1px dashed #ced4da;
    border-radius: 4px;
    padding: 6px;
    color: #212529;
    cursor: pointer;
  `;

  // 使用已有的封装渲染：当节点是封装胶囊时，沿用 editor 的渲染以保持一致
  // 否则回退为可点击文本渲染
  const ast = editor.virtualRoot.main;
  // 决定性修复：统一渲染逻辑，直接复用工作区的 AST 递归渲染函数
  // 这将确保主表达式区域的样式（包括占位符、高亮等）与工作区完全一致
  editor.renderASTNodeRecursive(ast, expressionContent);

  expressionContent.addEventListener('click', (e) => {
    if (e.target === expressionContent) {
      editor.expandMainExpression();
    }
  });

  mainDiv.appendChild(header);
  mainDiv.appendChild(expressionContent);
  container.appendChild(mainDiv);
}

