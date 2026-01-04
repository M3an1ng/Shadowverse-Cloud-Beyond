export function getReadableFunctionName(editor, functionName) {
    const functions = editor.metadata?.dsl?.functions || [];
    const match = functions.find(f =>
        String(f.name ?? f.Name ?? '').toLowerCase() === String(functionName ?? '').toLowerCase()
    );
    return match?.displayName || match?.DisplayName || match?.name || match?.Name || functionName;
}

export function getReadableValueLabel(editor, node) {
    if (!node) return '?';
    const meta = node.metadata || {};

    if (meta.type === 'eventField') {
        return meta.displayValue || node.value || '?';
    }

    if (meta.type === 'context') {
        return meta.displayName || editor.getDslContextObject(node.value)?.displayName || node.value || '?';
    }

    if (meta.type === 'property') {
        const path = meta.fullPath || node.value || '';
        if (!path.includes('.') && meta.displayName) {
            return meta.displayName;
        }
        const readable = editor.getReadablePropertyChain(path);
        return readable || meta.displayName || node.value || '?';
    }

    if (meta.type === 'staticValue') {
        return meta.staticValue || node.value || '?';
    }

    if (meta.type === 'rawText') {
        return node.value || '?';
    }

    return meta.displayName || node.value || '?';
}
