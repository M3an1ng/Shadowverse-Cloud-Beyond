export function setParseStatus(editor, state, message = "", errors = []) {
    editor.parseStatus = {
        state,
        message,
        errors: Array.isArray(errors) ? errors : []
    };
}

export function getParseStatusInfo(editor) {
    const state = editor.parseStatus?.state;
    if (!state || state === "idle" || state === "empty") return null;

    const message = editor.parseStatus?.message || "";
    const errors = editor.parseStatus?.errors || [];
    const detail = [message, ...errors].filter(Boolean).join("\n");

    if (state === "parsed") {
        return { label: "解析成功", color: "#2e7d32", bg: "#e8f5e9", border: "#a5d6a7", detail };
    }
    if (state === "loading") {
        return { label: "解析中...", color: "#1565c0", bg: "#e3f2fd", border: "#90caf9", detail };
    }
    if (state === "fallback") {
        return { label: "解析失败(降级)", color: "#c62828", bg: "#ffebee", border: "#ef9a9a", detail };
    }

    return { label: "解析异常", color: "#ad1457", bg: "#fce4ec", border: "#f8bbd0", detail };
}

export function showExpressionError(editor, message) {
    // 移除现有的错误提示
    const existingError = editor.container?.querySelector?.('.dsl-expression-error');
    if (existingError) {
        existingError.remove();
    }

    // 创建错误提示
    const errorDiv = document.createElement('div');
    errorDiv.className = 'dsl-expression-error';
    errorDiv.style.cssText = `
        background: #ffebee;
        border: 1px solid #f44336;
        border-radius: 4px;
        padding: 8px 12px;
        margin: 8px 0;
        color: #c62828;
        font-size: 12px;
        display: flex;
        align-items: center;
        gap: 8px;
    `;

    const errorIcon = document.createElement('span');
    errorIcon.textContent = '⚠️';
    errorDiv.appendChild(errorIcon);

    const errorText = document.createElement('span');
    errorText.textContent = message;
    errorDiv.appendChild(errorText);

    // 添加关闭按钮
    const closeBtn = document.createElement('button');
    closeBtn.textContent = '×';
    closeBtn.style.cssText = `
        background: none;
        border: none;
        color: #c62828;
        font-size: 16px;
        cursor: pointer;
        margin-left: auto;
    `;
    closeBtn.addEventListener('click', () => errorDiv.remove());
    errorDiv.appendChild(closeBtn);

    // 插入到编辑器容器中
    const editorContainer = editor.container?.querySelector?.('.dsl-editor');
    if (editorContainer) {
        editorContainer.insertBefore(errorDiv, editorContainer.firstChild);
    }

    // 3秒后自动消失
    setTimeout(() => {
        if (errorDiv.parentNode) {
            errorDiv.remove();
        }
    }, 3000);
}

export function showError(editor, message, container) {
    if (typeof editor.showError === 'function') {
        return editor.showError(message, container);
    }
    console.error(message);
    if (container) {
        container.textContent = message;
    }
    return null;
}
