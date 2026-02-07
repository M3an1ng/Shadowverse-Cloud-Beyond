// Phase B - Main Expression UI Rendering (V2 Tech Theme)

export function renderMainExpressionUI(editor, container) {
    if (!editor.virtualRoot || !editor.virtualRoot.main) return;

    // Create main container with V2 class
    const mainDiv = document.createElement("div");
    mainDiv.className = "dsl-v2-main-expression";

    // Header
    const header = document.createElement("div");
    header.className = "main-expression-header";

    const title = document.createElement("span");
    title.className = "header-title";
    title.textContent = "ACTIVE EXPRESSION";

    const status = document.createElement("span");
    status.className = "header-status";
    status.textContent = "EDITING";

    header.appendChild(title);
    header.appendChild(status);

    // Expression Content Area
    const expressionContent = document.createElement("div");
    expressionContent.className = "expression-content-v2 custom-scrollbar";

    // Render the AST using the editor's recursive renderer
    // This ensures the nodes themselves look correct (assuming ui-nodes.js handles V2 styles or we override them via CSS)
    const ast = editor.virtualRoot.main;
    editor.renderASTNodeRecursive(ast, expressionContent);

    expressionContent.addEventListener('click', (e) => {
        if (e.target === expressionContent) {
            editor.expandMainExpression();
        }
    });

    mainDiv.appendChild(header);
    mainDiv.appendChild(expressionContent);

    // Append to the specific V2 workspace container if available, otherwise fallback
    const workspace = container.querySelector('#dsl-v2-workspace') || container;
    workspace.appendChild(mainDiv);
}
