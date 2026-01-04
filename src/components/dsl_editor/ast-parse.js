import { createValueNode, createListQueryNode, createListSortNode, createListTakeNode } from "./core.js";

export function buildAstFromExpression(editor, expression) {
    const raw = (expression ?? "").trim();
    if (!raw) return null;

    const isWrappedBySingleParens = (s) => {
        const t = (s ?? "").trim();
        if (!t.startsWith('(') || !t.endsWith(')')) return false;
        let depth = 0;
        for (let i = 0; i < t.length; i++) {
            const ch = t[i];
            if (ch === '(') depth++;
            if (ch === ')') depth--;
            if (depth === 0 && i < t.length - 1) return false;
        }
        return depth === 0;
    };

    const unwrapOuterParens = (s) => {
        let t = (s ?? "").trim();
        while (isWrappedBySingleParens(t)) {
            t = t.slice(1, -1).trim();
        }
        return t;
    };

    const inferReturnTypeFromRawExpression = (s) => {
        const t = unwrapOuterParens(s);

        if (/^(true|false)$/i.test(t)) return 'Boolean';
        if (/^-?\d+$/.test(t)) return 'Int32';
        if (/^-?\d*\.\d+$/.test(t)) return 'Double';
        if ((t.startsWith('"') && t.endsWith('"')) || (t.startsWith("'") && t.endsWith("'"))) return 'String';

        const m = t.match(/^([A-Za-z_][A-Za-z0-9_]*)\s*\(/);
        if (m) {
            const fn = m[1];
            if (fn === 'eventBool') return 'Boolean';
            if (fn === 'eventString') return 'String';
            if (fn === 'eventGuid') return 'Guid';
            if (fn === 'eventInt') return 'Int32';

            const funcs = editor.metadata?.dsl?.functions || [];
            const meta = funcs.find(f => String(f.name || f.Name) === String(fn));
            const rt = meta?.returnType || meta?.returnTypeName || meta?.ReturnType || meta?.ReturnTypeName;
            if (rt) return editor.normalizeTypeName(rt) || 'Object';
        }

        if (/[<>]=?|==|!=/.test(t) || /&&|\|\|/.test(t)) return 'Boolean';
        if (/[+\-*/]/.test(t)) return 'Number';

        return 'Object';
    };

    const parseLiteralOrPropertyChain = (s, forcedType = null) => {
        const t = unwrapOuterParens(s);

        const litType = editor.inferStaticValueType(t);
        if (litType !== 'Object' && (litType !== 'String' || (t.startsWith('"') || t.startsWith("'")))) {
            return createValueNode(t, forcedType || litType, {
                type: 'staticValue',
                staticValue: t,
                isAwaitingInput: false
            });
        }

        const parts = t.split('.').filter(Boolean);
        const isIdent = (seg) => /^[A-Za-z_][A-Za-z0-9_]*$/.test(seg);
        if (parts.length > 0 && parts.every(isIdent)) {
            const ctx = editor.getDslContextObject(parts[0]);
            let currentType = editor.normalizeTypeName(ctx?.typeName) || 'Object';
            let currentPath = parts[0];
            let currentNode = createValueNode(currentPath, currentType, { type: 'context', name: parts[0], displayName: ctx?.displayName || parts[0] });

            for (let i = 1; i < parts.length; i++) {
                const seg = parts[i];
                currentPath = `${currentPath}.${seg}`;
                const tm = editor.getTypeMetadata(currentType);
                const prop = (tm?.properties || []).find(p => String(p.name).toLowerCase() === String(seg).toLowerCase());
                currentType = editor.normalizeTypeName(prop?.typeName) || 'Object';
                currentNode = createValueNode(currentPath, currentType, { type: 'property', fullPath: currentPath });
            }

            return currentNode;
        }

        return createValueNode(t, forcedType || inferReturnTypeFromRawExpression(t), { type: 'rawText' });
    };

    const parseTrailingListOp = (s) => {
        const t = (s ?? "").trim();
        if (!t.endsWith(')')) return null;

        let depth = 0;
        let openIndex = -1;
        for (let i = t.length - 1; i >= 0; i--) {
            const ch = t[i];
            if (ch === ')') depth++;
            else if (ch === '(') {
                depth--;
                if (depth === 0) {
                    openIndex = i;
                    break;
                }
            }
        }
        if (openIndex < 0) return null;

        const before = t.slice(0, openIndex).trim();
        const arg = t.slice(openIndex + 1, -1);
        const callMatch = before.match(/^(.*)\.(query|orderBy|take)$/);
        if (!callMatch) return null;

        return { source: callMatch[1], op: callMatch[2], arg };
    };

    const parseTakeArg = (arg) => {
        const trimmed = String(arg || '').trim();
        if (!trimmed) return { strategy: 'first', count: '' };
        const match = trimmed.match(/^(first|last|random)\b/i);
        if (!match) return { strategy: 'first', count: trimmed };
        const strategy = match[1].toLowerCase();
        const rest = trimmed.slice(match[0].length).trim();
        const count = rest.replace(/^,/, '').trim();
        return { strategy, count };
    };

    let toParse = unwrapOuterParens(raw);
    const ops = [];
    while (true) {
        const parsed = parseTrailingListOp(toParse);
        if (!parsed) break;
        ops.unshift(parsed);
        toParse = parsed.source;
    }

    let rootNode = parseLiteralOrPropertyChain(toParse);

    for (const op of ops) {
        if (op.op === 'query') {
            const queryNode = createListQueryNode(rootNode);
            const filterAst = parseLiteralOrPropertyChain(op.arg);
            queryNode.setFilterExpression(editor.createEncapsulatedFrom(filterAst));
            rootNode = queryNode;
        } else if (op.op === 'orderBy') {
            const sortNode = createListSortNode(rootNode);
            const sortAst = parseLiteralOrPropertyChain(op.arg);
            sortNode.setSortExpression(editor.createEncapsulatedFrom(sortAst));
            rootNode = sortNode;
        } else if (op.op === 'take') {
            const takeNode = createListTakeNode(rootNode);
            const parsed = parseTakeArg(op.arg);
            takeNode.setStrategy(parsed.strategy);
            if (parsed.count) {
                const countAst = parseLiteralOrPropertyChain(parsed.count);
                takeNode.setCountExpression(editor.createEncapsulatedFrom(countAst));
            }
            rootNode = takeNode;
        }
    }

    return rootNode;
}
