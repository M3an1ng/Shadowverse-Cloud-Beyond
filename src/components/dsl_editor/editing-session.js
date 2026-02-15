import { pushPath, popPath, peekPath, clearPath as clearPathStack, writeBackToParentSlot as writeBackSlot } from "./path.js";

export function pushEditingPath(editor, entry) {
    if (!editor.editingPath) editor.editingPath = [];
    try {
        pushPath(editor.editingPath, entry);
    } catch (e) {
        editor.editingPath.push(entry);
    }
}

export function popEditingPath(editor) {
    if (!editor.editingPath) editor.editingPath = [];
    let entry;
    try {
        entry = popPath(editor.editingPath);
    } catch (e) {
        entry = editor.editingPath.length > 0 ? editor.editingPath.pop() : undefined;
    }
    return entry;
}

export function peekEditingPath(editor) {
    if (!editor.editingPath) editor.editingPath = [];
    try {
        return peekPath(editor.editingPath);
    } catch (e) {
        return editor.editingPath.length === 0 ? undefined : editor.editingPath[editor.editingPath.length - 1];
    }
}

export function clearEditingPath(editor) {
    try {
        clearPathStack(editor.editingPath);
    } catch (e) {
        editor.editingPath = [];
    }
}

export function getHighlightTargetNode(editor) {
    const top = editor.peekEditingPath ? editor.peekEditingPath() : null;
    if (!top || !top.parentNode) return null;
    if (top.slot === 'main') return top.parentNode.main || null;
    if (top.slot === 'left') return top.parentNode.leftOperand || null;
    if (top.slot === 'right') return top.parentNode.rightOperand || null;
    if (top.slot === 'arg') {
        const argIndex = typeof top.index === 'number' ? top.index : 0;
        return (top.parentNode.args || [])[argIndex] || null;
    }
    if (top.slot === 'filterExpression') return top.parentNode.filterExpression || null;
    if (top.slot === 'sortExpression') return top.parentNode.sortExpression || null;
    if (top.slot === 'countExpression') return top.parentNode.countExpression || null;
    if (top.slot === 'keyExpression') return top.parentNode.keyExpression || null;
    if (top.slot === 'selectExpression') return top.parentNode.selectExpression || null;
    return top.parentNode || null;
}

export function ensureAstRootSync(editor) {
    if (editor.astRoot !== editor.workspaceAST) {
        editor.astRoot = editor.workspaceAST;
    }
}

export function writeBackToParentSlot(editor, parentNode, slot, newValueNode, index = null) {
    if (editor && typeof editor.setSlotNode === 'function') {
        return editor.setSlotNode(parentNode, slot, index, newValueNode);
    }
    return writeBackSlot(parentNode, slot, newValueNode, index);
}

export function getDslContextObject(editor, name) {
    const normalizedName = String(name || "").trim().toLowerCase();
    if (editor.contextOverride && String(editor.contextOverride.name || "").toLowerCase() === normalizedName) {
        return {
            name: editor.contextOverride.name,
            displayName: editor.contextOverride.displayName,
            typeName: editor.contextOverride.typeName,
            properties: editor.getTypeMetadata(editor.contextOverride.typeName)?.properties || []
        };
    }

    if (editor.fixedRootContext && String(editor.fixedRootContext.name || "").toLowerCase() === normalizedName) {
        return {
            name: editor.fixedRootContext.name,
            displayName: editor.fixedRootContext.displayName || editor.fixedRootContext.name,
            typeName: editor.fixedRootContext.typeName,
            properties: editor.getTypeMetadata(editor.fixedRootContext.typeName)?.properties || []
        };
    }

    const contextObjects = editor.metadata?.dsl?.contextObjects || [];
    const result = contextObjects.find((obj) => String(obj.name || "").toLowerCase() === normalizedName);
    return result;
}
