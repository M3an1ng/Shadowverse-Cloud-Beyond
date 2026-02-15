export function getReadableFunctionName(editor, functionName) {
    const localizationMap = {
        abs: "绝对值",
        contains: "包含",
        count: "计数",
        sum: "求和",
        min: "最小值",
        max: "最大值",
        frequency: "频次",
        isempty: "为空",
        bytype: "按类型",
        if: "如果",
        find: "查找",
        var: "计数器",
        eventint: "事件整型",
        eventbool: "事件布尔",
        eventstring: "事件文本",
        eventguid: "事件Guid",
        eventguidlist: "事件Guid列表",
        eventcard: "事件卡牌",
        eventcards: "事件卡牌列表"
    };

    const functions = editor.metadata?.dsl?.functions || [];
    const match = functions.find(f =>
        String(f.name ?? f.Name ?? '').toLowerCase() === String(functionName ?? '').toLowerCase()
    );
    const rawName = match?.name || match?.Name || functionName;
    const displayName = match?.displayName || match?.DisplayName;
    if (displayName && String(displayName).toLowerCase() !== String(rawName ?? '').toLowerCase()) {
        return displayName;
    }
    const localized = localizationMap[String(rawName ?? '').toLowerCase()];
    return localized || displayName || rawName || functionName;
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

    if (meta.type === 'enumValue') {
        return meta.displayValue || meta.enumValue || meta.staticValue || node.value || '?';
    }

    if (meta.type === 'rawText') {
        return node.value || '?';
    }

    return meta.displayName || node.value || '?';
}
