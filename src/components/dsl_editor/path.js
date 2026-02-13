// Phase B modularization - path stack utilities (skeleton)

/**
 * 压栈
 */
export function pushPath(stack, entry) {
  if (!Array.isArray(stack)) return;
  stack.push(entry);
}

/**
 * 出栈
 */
export function popPath(stack) {
  if (!Array.isArray(stack) || stack.length === 0) return undefined;
  return stack.pop();
}

/**
 * 查看栈顶
 */
export function peekPath(stack) {
  if (!Array.isArray(stack) || stack.length === 0) return undefined;
  return stack[stack.length - 1];
}

/**
 * 清空路径
 */
export function clearPath(stack) {
  if (!Array.isArray(stack)) return;
  stack.length = 0;
}

/**
 * 写回父槽位 (兜底路径；优先应通过 editor.setSlotNode 统一处理)
 */
export function writeBackToParentSlot(parentNode, slot, newValueNode, index = null) {
    if (!parentNode || !slot) return false;

    if (parentNode.type === 'virtualRoot' && slot === 'main') {
        parentNode.main = newValueNode;
        return true;
    }

    if (parentNode.type === 'value' && slot === 'innerAST') {
        if (!parentNode.metadata || typeof parentNode.metadata !== 'object') {
            parentNode.metadata = {};
        }
        parentNode.metadata.innerAST = newValueNode || null;
        if (newValueNode) newValueNode.parent = parentNode;
        parentNode.updateCompleteness?.();
        return true;
    }

    if (parentNode.type === 'operator') {
        if (slot === 'left') {
            if (typeof parentNode.setLeftOperand === 'function') {
                parentNode.setLeftOperand(newValueNode);
            } else {
                parentNode.leftOperand = newValueNode || null;
                if (newValueNode) newValueNode.parent = parentNode;
                parentNode.updateCompleteness?.();
            }
            return true;
        }
        if (slot === 'right') {
            if (typeof parentNode.setRightOperand === 'function') {
                parentNode.setRightOperand(newValueNode);
            } else {
                parentNode.rightOperand = newValueNode || null;
                if (newValueNode) newValueNode.parent = parentNode;
                parentNode.updateCompleteness?.();
            }
            return true;
        }
    }

    if (parentNode.type === 'functionCall' && slot === 'arg') {
        const argIndex = typeof index === 'number' ? index : 0;
        if (typeof parentNode.setArg === 'function') {
            parentNode.setArg(argIndex, newValueNode);
            return true;
        }
        return false;
    }

    if (
        (parentNode.type === 'listQuery'
            || parentNode.type === 'listSort'
            || parentNode.type === 'listTake'
            || parentNode.type === 'listDistinct'
            || parentNode.type === 'listSelect')
        && slot === 'source'
    ) {
        parentNode.source = newValueNode || null;
        if (newValueNode) newValueNode.parent = parentNode;
        parentNode.updateCompleteness?.();
        return true;
    }

    if (parentNode.type === 'listQuery' && slot === 'filterExpression') {
        if (typeof parentNode.setFilterExpression === 'function') {
            parentNode.setFilterExpression(newValueNode);
        } else {
            parentNode.filterExpression = newValueNode || null;
            if (newValueNode) newValueNode.parent = parentNode;
            parentNode.updateCompleteness?.();
        }
        return true;
    }

    if (parentNode.type === 'listSort' && slot === 'sortExpression') {
        if (typeof parentNode.setSortExpression === 'function') {
            parentNode.setSortExpression(newValueNode);
        } else {
            parentNode.sortExpression = newValueNode || null;
            if (newValueNode) newValueNode.parent = parentNode;
            parentNode.updateCompleteness?.();
        }
        return true;
    }

    if (parentNode.type === 'listTake' && slot === 'countExpression') {
        if (typeof parentNode.setCountExpression === 'function') {
            parentNode.setCountExpression(newValueNode);
        } else {
            parentNode.countExpression = newValueNode || null;
            if (newValueNode) newValueNode.parent = parentNode;
            parentNode.updateCompleteness?.();
        }
        return true;
    }

    if (parentNode.type === 'listDistinct' && slot === 'keyExpression') {
        if (typeof parentNode.setKeyExpression === 'function') {
            parentNode.setKeyExpression(newValueNode);
        } else {
            parentNode.keyExpression = newValueNode || null;
            if (newValueNode) newValueNode.parent = parentNode;
            parentNode.updateCompleteness?.();
        }
        return true;
    }

    if (parentNode.type === 'listSelect' && slot === 'selectExpression') {
        if (typeof parentNode.setSelectExpression === 'function') {
            parentNode.setSelectExpression(newValueNode);
        } else {
            parentNode.selectExpression = newValueNode || null;
            if (newValueNode) newValueNode.parent = parentNode;
            parentNode.updateCompleteness?.();
        }
        return true;
    }

    return false;
}
