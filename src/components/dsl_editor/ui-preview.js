export function getReadablePreviewText(editor) {
    const node = editor.virtualRoot?.main || null;
    if (!node) return '(空)';
    return toReadableText(editor, node);
}

function getExpectedTypeForPreview(editor, parent, slot, index) {
    if (typeof editor.getExpectedTypeForSlot === 'function') {
        return editor.getExpectedTypeForSlot(parent, slot, index);
    }
    return null;
}

function formatPlaceholder(editor, expectedType) {
    if (typeof editor.getPlaceholderLabel === 'function') {
        return editor.getPlaceholderLabel(expectedType);
    }
    if (!expectedType) return '待填写';
    if (String(expectedType).toLowerCase() === 'object') return '任意类型';
    return editor.getDisplayTypeName(expectedType) || '待填写';
}

export function toReadableText(editor, node, parent = null, slot = null, index = null) {
    if (!node) {
        const expected = getExpectedTypeForPreview(editor, parent, slot, index);
        return formatPlaceholder(editor, expected);
    }

    if (node.type === 'value') {
        if (node.metadata?.type === 'encapsulated' && node.metadata.innerAST) {
            return toReadableText(editor, node.metadata.innerAST, node);
        }
        if (node.metadata?.type === 'empty') {
            const expected = getExpectedTypeForPreview(editor, parent || node.parent, slot, index);
            return formatPlaceholder(editor, expected);
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
            '\\': '向上取整除',
            '%': '取余',
            '|': '并集',
            '&': '交集',
            '~': '差集'
        };
        const left = toReadableText(editor, node.leftOperand, node, 'left');
        const right = toReadableText(editor, node.rightOperand, node, 'right');
        const op = opMap[node.operator] || node.operator;
        return `(${left} ${op} ${right})`;
    }

    if (node.type === 'functionCall') {
        const fnName = editor.getReadableFunctionName(node.functionName);
        const args = (node.args || []).map((a, i) => toReadableText(editor, a, node, 'arg', i));
        if (args.length > 0) {
            const receiver = args[0];
            const rest = args.slice(1);
            if (rest.length === 0) {
                return `${receiver}.${fnName}`;
            }
            if (rest.length === 1) {
                return `${receiver}.${fnName} ${rest[0]}`;
            }
            return `${receiver}.${fnName}(${rest.join(', ')})`;
        }
        return fnName;
    }

    if (node.type === 'listQuery') {
        const source = toReadableText(editor, node.source, node, 'source');
        const pred = toReadableText(editor, node.filterExpression, node, 'filterExpression');
        return `${source} 查询(${pred})`;
    }
    if (node.type === 'listSort') {
        const source = toReadableText(editor, node.source, node, 'source');
        const key = toReadableText(editor, node.sortExpression, node, 'sortExpression');
        const direction = String(node.direction || 'asc').toLowerCase();
        const label = direction === 'desc' ? '降序' : '升序';
        return `${source} 排序(${label}, ${key})`;
    }
    if (node.type === 'listTake') {
        const source = toReadableText(editor, node.source, node, 'source');
        const strategy = String(node.strategy || 'first').toLowerCase();
        if (strategy === 'all') {
            return `${source} 全部保留`;
        }
        const cnt = toReadableText(editor, node.countExpression, node, 'countExpression');
        const label = strategy === 'random'
            ? '随机取'
            : strategy === 'last'
                ? '取后'
                : '取前';
        return `${source} ${label}(${cnt})`;
    }
    if (node.type === 'listDistinct') {
        const source = toReadableText(editor, node.source, node, 'source');
        const key = toReadableText(editor, node.keyExpression, node, 'keyExpression');
        return `${source} 去重(${key})`;
    }
    if (node.type === 'listSelect') {
        const source = toReadableText(editor, node.source, node, 'source');
        const expr = toReadableText(editor, node.selectExpression, node, 'selectExpression');
        return `${source} 选择(${expr})`;
    }
    return '待填写';
}
