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
 * 写回父槽位 (仅处理 left/right；self 由上层结合路径处理)
 */
export function writeBackToParentSlot(parentNode, slot, newValueNode) {
    if (!parentNode || !newValueNode) return false;

    // 关键修复：处理虚拟根节点
    if (parentNode.type === 'virtualRoot' && slot === 'main') {
        parentNode.main = newValueNode;
        return true;
    }

    // 动态构建setter方法名
    const setterName = `set${slot.charAt(0).toUpperCase() + slot.slice(1)}`;

    if (typeof parentNode[setterName] === 'function') {
        parentNode[setterName](newValueNode);
        return true;
    }
    
    // 回退到旧的硬编码方法
    if (slot === 'left' && typeof parentNode.setLeftOperand === 'function') {
        parentNode.setLeftOperand(newValueNode);
        return true;
    }
    if (slot === 'right' && typeof parentNode.setRightOperand === 'function') {
        parentNode.setRightOperand(newValueNode);
        return true;
    }

    console.error(`writeBackToParentSlot Error: Setter method '${setterName}' not found on parentNode`, parentNode);
    return false;
}

