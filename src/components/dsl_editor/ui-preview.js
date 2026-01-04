export function getReadablePreviewText(editor) {
    const node = editor.virtualRoot?.main || null;
    if (!node) return '(空)';
    return toReadableText(editor, node);
}

export function toReadableText(editor, node) {
    if (!node) return '?';

    if (node.type === 'value') {
        if (node.metadata?.type === 'encapsulated' && node.metadata.innerAST) {
            return toReadableText(editor, node.metadata.innerAST);
        }
        return editor.getReadableValueLabel(node);
    }

    if (node.type === 'operator') {
        const opMap = {
            '&&': '且',
            '||': '或',
            '==': '等于',
            '!=': '不等于',
            '>': '大于',
            '>=': '大于等于',
            '<': '小于',
            '<=': '小于等于',
            '+': '加',
            '-': '减',
            '*': '乘',
            '/': '除',
            '|': '并集',
            '&': '交集'
        };
        const left = toReadableText(editor, node.leftOperand);
        const right = toReadableText(editor, node.rightOperand);
        const op = opMap[node.operator] || node.operator;
        return `(${left} ${op} ${right})`;
    }

    if (node.type === 'functionCall') {
        const args = (node.args || []).map((a) => (a ? toReadableText(editor, a) : '?'));
        const fnName = editor.getReadableFunctionName(node.functionName);
        return `${fnName}(${args.join(', ')})`;
    }

    if (node.type === 'listQuery') {
        const source = toReadableText(editor, node.source);
        const pred = toReadableText(editor, node.filterExpression);
        return `${source} 筛选(${pred})`;
    }
    if (node.type === 'listSort') {
        const source = toReadableText(editor, node.source);
        const key = toReadableText(editor, node.sortExpression);
        return `${source} 排序(${key})`;
    }
    if (node.type === 'listTake') {
        const source = toReadableText(editor, node.source);
        const cnt = toReadableText(editor, node.countExpression);
        return `${source} 取前(${cnt})`;
    }

    return '?';
}
