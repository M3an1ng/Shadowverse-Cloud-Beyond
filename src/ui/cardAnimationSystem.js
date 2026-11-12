const PHASE_PRIORITY = Object.freeze({
    exit: 10,
    move: 20,
    enter: 30
});

const LIVE_TRANSIENT_ROOT_CLASSES = Object.freeze([
    'card-fade-enter',
    'hand-card-fade-enter-down',
    'card-enter-hidden',
    'hand-card-enter-hidden',
    'attack-drag-hover',
    'attack-drag-invalid',
    'hand-drag-origin',
    'hand-play-committed-source'
]);

const CARD_ACTION_CHILD_SELECTOR = '.super-evolve-button, .evolve-button, .use-button, .fusion-button, .act-button, .mirage-field-controls';
const SYNCED_CARD_CSS_VARS = Object.freeze([
    '--fan-offset',
    '--fan-abs',
    '--hand-count',
    '--play-mode-label'
]);

function syncElementAttributes(liveEl, nextEl) {
    const prevAttrs = Array.from(liveEl.attributes || []);
    prevAttrs.forEach(attr => {
        const name = attr.name;
        if (name === 'class' || name === 'style' || name === 'data-render-key') return;
        if (!nextEl.hasAttribute(name)) {
            liveEl.removeAttribute(name);
        }
    });

    const nextAttrs = Array.from(nextEl.attributes || []);
    nextAttrs.forEach(attr => {
        const name = attr.name;
        if (name === 'class' || name === 'style' || name === 'data-render-key') return;
        if (liveEl.getAttribute(name) !== attr.value) {
            liveEl.setAttribute(name, attr.value);
        }
    });
}

function syncCardInlineVariables(liveEl, nextEl) {
    SYNCED_CARD_CSS_VARS.forEach(varName => {
        const nextVal = nextEl.style.getPropertyValue(varName);
        const prevVal = liveEl.style.getPropertyValue(varName);
        if (nextVal !== prevVal) {
            if (nextVal) {
                liveEl.style.setProperty(varName, nextVal);
            } else {
                liveEl.style.removeProperty(varName);
            }
        }
    });
}

function syncSingletonDecoration(liveEl, nextEl, selector, anchorAfterEl) {
    const liveNode = liveEl.querySelector(`:scope > ${selector}`);
    const nextNode = nextEl.querySelector(`:scope > ${selector}`);
    if (liveNode && nextNode) {
        if (liveNode.outerHTML !== nextNode.outerHTML) {
            liveNode.replaceWith(nextNode);
            return nextNode;
        }
        return liveNode;
    }
    if (liveNode && !nextNode) {
        liveNode.remove();
        return null;
    }
    if (!liveNode && nextNode) {
        if (anchorAfterEl && anchorAfterEl.parentElement === liveEl) {
            anchorAfterEl.insertAdjacentElement('afterend', nextNode);
        } else {
            liveEl.prepend(nextNode);
        }
        return nextNode;
    }
    return null;
}

function syncCardStatsBlock(liveEl, nextEl, anchorAfterEl) {
    const liveStats = liveEl.querySelector(':scope > .stats');
    const nextStats = nextEl.querySelector(':scope > .stats');
    if (liveStats && nextStats) {
        const liveSpans = Array.from(liveStats.children);
        const nextSpans = Array.from(nextStats.children);
        const sameShape = liveSpans.length === nextSpans.length
            && liveSpans.every((span, idx) => span.tagName === nextSpans[idx].tagName && span.classList[0] === nextSpans[idx].classList[0]);
        if (sameShape) {
            if (liveStats.className !== nextStats.className) {
                liveStats.className = nextStats.className;
            }
            liveSpans.forEach((liveSpan, idx) => {
                const nextSpan = nextSpans[idx];
                if (liveSpan.className !== nextSpan.className) {
                    liveSpan.className = nextSpan.className;
                }
                const nextTitle = nextSpan.getAttribute('title');
                if (nextTitle !== liveSpan.getAttribute('title')) {
                    if (nextTitle === null) {
                        liveSpan.removeAttribute('title');
                    } else {
                        liveSpan.setAttribute('title', nextTitle);
                    }
                }
                if (liveSpan.textContent !== nextSpan.textContent) {
                    liveSpan.textContent = nextSpan.textContent;
                }
            });
            return liveStats;
        }
        liveStats.replaceWith(nextStats);
        return nextStats;
    }
    if (liveStats && !nextStats) {
        liveStats.remove();
        return null;
    }
    if (!liveStats && nextStats) {
        if (anchorAfterEl && anchorAfterEl.parentElement === liveEl) {
            anchorAfterEl.insertAdjacentElement('afterend', nextStats);
        } else {
            liveEl.appendChild(nextStats);
        }
        return nextStats;
    }
    return null;
}

function syncCardActionChildren(liveEl, nextEl) {
    const liveActions = Array.from(liveEl.querySelectorAll(`:scope > ${CARD_ACTION_CHILD_SELECTOR}`));
    const nextActions = Array.from(nextEl.querySelectorAll(`:scope > ${CARD_ACTION_CHILD_SELECTOR}`));
    const unchanged = liveActions.length === nextActions.length
        && liveActions.every((node, idx) => node.outerHTML === nextActions[idx].outerHTML);
    if (unchanged) {
        return;
    }
    liveActions.forEach(node => node.remove());
    nextActions.forEach(node => liveEl.appendChild(node));
}

function canReconcileCardInPlace(liveEl, nextEl) {
    if (!liveEl || !nextEl) return false;
    if (liveEl.tagName !== nextEl.tagName) return false;
    const liveIsCard = liveEl.classList?.contains('card') === true;
    const nextIsCard = nextEl.classList?.contains('card') === true;
    if (liveIsCard && nextIsCard) return true;
    const liveIsBack = liveEl.classList?.contains('card-back') === true;
    const nextIsBack = nextEl.classList?.contains('card-back') === true;
    return liveIsBack && nextIsBack;
}

export function reconcileCardElementInPlace(liveEl, nextEl, preserveStateClasses = () => { }) {
    preserveStateClasses(liveEl, nextEl);
    LIVE_TRANSIENT_ROOT_CLASSES.forEach(cls => {
        if (liveEl.classList.contains(cls)) {
            nextEl.classList.add(cls);
        }
    });

    if (liveEl.className !== nextEl.className) {
        liveEl.className = nextEl.className;
    }
    syncElementAttributes(liveEl, nextEl);
    syncCardInlineVariables(liveEl, nextEl);

    if (liveEl.classList.contains('card-back')) {
        return liveEl;
    }

    let cursorAnchor = liveEl.querySelector(':scope > .card-art');
    const nextArt = nextEl.querySelector(':scope > .card-art');
    if (cursorAnchor && nextArt) {
        const prevArtUrl = cursorAnchor.dataset.artUrl || '';
        const nextArtUrl = nextArt.dataset.artUrl || '';
        if (prevArtUrl !== nextArtUrl) {
            cursorAnchor.replaceWith(nextArt);
            cursorAnchor = nextArt;
        } else if (!cursorAnchor.style.backgroundImage && nextArt.style.backgroundImage) {
            cursorAnchor.style.backgroundImage = nextArt.style.backgroundImage;
            cursorAnchor.classList.toggle('has-art', nextArt.classList.contains('has-art'));
        }
    } else if (!cursorAnchor && nextArt) {
        liveEl.prepend(nextArt);
        cursorAnchor = nextArt;
    }

    const gemAnchor = syncSingletonDecoration(liveEl, nextEl, '.bui-amulet-gem-dot', cursorAnchor);
    if (gemAnchor) cursorAnchor = gemAnchor;
    const spellAnchor = syncSingletonDecoration(liveEl, nextEl, '.bui-spell-crest', cursorAnchor);
    if (spellAnchor) cursorAnchor = spellAnchor;
    const sevoAnchor = syncSingletonDecoration(liveEl, nextEl, '.bui-sevo-ring', cursorAnchor);
    if (sevoAnchor) cursorAnchor = sevoAnchor;
    const targetRingAnchor = syncSingletonDecoration(liveEl, nextEl, '.target-hover-ring', cursorAnchor);
    if (targetRingAnchor) cursorAnchor = targetRingAnchor;

    const liveCost = liveEl.querySelector(':scope > .cost');
    const nextCost = nextEl.querySelector(':scope > .cost');
    if (liveCost && nextCost) {
        if (liveCost.className !== nextCost.className) {
            liveCost.className = nextCost.className;
        }
        const liveCostSpan = liveCost.querySelector('span');
        const nextCostSpan = nextCost.querySelector('span');
        if (liveCostSpan && nextCostSpan && liveCostSpan.textContent !== nextCostSpan.textContent) {
            liveCostSpan.textContent = nextCostSpan.textContent;
        }
        cursorAnchor = liveCost;
    } else if (!liveCost && nextCost) {
        if (cursorAnchor && cursorAnchor.parentElement === liveEl) {
            cursorAnchor.insertAdjacentElement('afterend', nextCost);
        } else {
            liveEl.appendChild(nextCost);
        }
        cursorAnchor = nextCost;
    } else if (liveCost && !nextCost) {
        liveCost.remove();
    }

    const liveName = liveEl.querySelector(':scope > .name');
    const nextName = nextEl.querySelector(':scope > .name');
    if (liveName && nextName) {
        if (liveName.className !== nextName.className) {
            liveName.className = nextName.className;
        }
        if (liveName.textContent !== nextName.textContent) {
            liveName.textContent = nextName.textContent;
        }
        cursorAnchor = liveName;
    } else if (!liveName && nextName) {
        if (cursorAnchor && cursorAnchor.parentElement === liveEl) {
            cursorAnchor.insertAdjacentElement('afterend', nextName);
        } else {
            liveEl.appendChild(nextName);
        }
        cursorAnchor = nextName;
    } else if (liveName && !nextName) {
        liveName.remove();
    }

    const statsAnchor = syncCardStatsBlock(liveEl, nextEl, cursorAnchor);
    if (statsAnchor) cursorAnchor = statsAnchor;

    syncSingletonDecoration(liveEl, nextEl, '.keyword-badges', cursorAnchor);
    syncCardActionChildren(liveEl, nextEl);

    return liveEl;
}

function isValidRect(rect) {
    return Boolean(rect)
        && Number.isFinite(rect.left)
        && Number.isFinite(rect.top)
        && Number.isFinite(rect.width)
        && Number.isFinite(rect.height)
        && rect.width > 1
        && rect.height > 1;
}

function isOffViewport(rect) {
    if (!isValidRect(rect)) return true;
    return rect.bottom <= 0
        || rect.right <= 0
        || rect.left >= window.innerWidth
        || rect.top >= window.innerHeight;
}

function defaultEnterClassResolver(element) {
    return element?.classList?.contains('hand-card')
        ? 'hand-card-fade-enter-down'
        : 'card-fade-enter';
}

function defaultExitClassResolver(element) {
    return element?.classList?.contains('hand-card')
        ? 'hand-card-fade-exit-up'
        : 'card-fade-exit';
}

function defaultTransientClassCleaner(element) {
    if (!element?.classList) return;
    element.classList.remove(
        'card-fade-enter',
        'hand-card-fade-enter-down',
        'card-fade-exit',
        'hand-card-fade-exit-up',
        'card-fade-move',
        'card-enter-hidden',
        'hand-card-enter-hidden'
    );
}

function escapeRenderKeyForSelector(renderKey) {
    return String(renderKey ?? '')
        .replace(/\\/g, '\\\\')
        .replace(/"/g, '\\"');
}

export function createCardAnimationSystem(config = {}) {
    const durations = {
        enterMs: config?.durations?.enterMs ?? 220,
        moveMs: config?.durations?.moveMs ?? 220,
        exitMs: config?.durations?.exitMs ?? 190
    };
    const sequencing = {
        enterAfterExitMs: Math.max(0, Number(config?.sequencing?.enterAfterExitMs ?? 0) || 0)
    };
    const stagger = {
        enterMs: Math.max(0, Number(config?.stagger?.enterMs ?? 45) || 0),
        moveMs: Math.max(0, Number(config?.stagger?.moveMs ?? 0) || 0),
        exitMs: Math.max(0, Number(config?.stagger?.exitMs ?? 0) || 0),
        maxMs: Math.max(0, Number(config?.stagger?.maxMs ?? 240) || 0)
    };
    const moveOptions = {
        epsilon: config?.move?.epsilon ?? 0.75
    };

    const isPageActive = typeof config.isPageActive === 'function'
        ? config.isPageActive
        : () => true;
    const ensureTransientRoot = typeof config.ensureTransientRoot === 'function'
        ? config.ensureTransientRoot
        : () => null;
    const scheduleTimeout = typeof config.scheduleTimeout === 'function'
        ? config.scheduleTimeout
        : (callback, delayMs) => window.setTimeout(callback, delayMs);
    const preserveStateClasses = typeof config.preserveStateClasses === 'function'
        ? config.preserveStateClasses
        : () => { };
    const clearTransientClasses = typeof config.clearTransientClasses === 'function'
        ? config.clearTransientClasses
        : defaultTransientClassCleaner;
    const resolveRenderedCardKey = typeof config.resolveRenderedCardKey === 'function'
        ? config.resolveRenderedCardKey
        : ((element, index) => String(element?.dataset?.renderKey || `legacy-${index}`));
    const resolveEnterClass = typeof config.resolveEnterClass === 'function'
        ? config.resolveEnterClass
        : defaultEnterClassResolver;
    const resolveExitClass = typeof config.resolveExitClass === 'function'
        ? config.resolveExitClass
        : defaultExitClassResolver;
    const supportsReducedMotion = typeof config.prefersReducedMotion === 'function'
        ? config.prefersReducedMotion
        : () => false;
    const shouldSkipEnter = typeof config.shouldSkipEnter === 'function'
        ? config.shouldSkipEnter
        : () => false;
    const shouldSkipExit = typeof config.shouldSkipExit === 'function'
        ? config.shouldSkipExit
        : () => false;
    const onElementMounted = typeof config.onElementMounted === 'function'
        ? config.onElementMounted
        : () => { };

    const renderedContainers = new WeakSet();
    const pendingEnterKeysByContainer = new WeakMap();
    const containerIds = new WeakMap();
    const moveAnimationsByElement = new WeakMap();
    const activeWaapiAnimations = new Set();
    let containerIdCounter = 1;

    let queuedEvents = [];

    function trackWaapi(anim) {
        if (!anim) return null;
        activeWaapiAnimations.add(anim);
        const cleanup = () => activeWaapiAnimations.delete(anim);
        anim.finished?.then(cleanup, cleanup);
        return anim;
    }

    function getContainerId(container) {
        if (!container) return 'container-unknown';
        const existing = containerIds.get(container);
        if (existing) return existing;
        const next = `container-${containerIdCounter++}`;
        containerIds.set(container, next);
        return next;
    }

    function getPendingEnterSet(container, createIfMissing = false) {
        if (!container) return null;
        const existing = pendingEnterKeysByContainer.get(container);
        if (existing) return existing;
        if (!createIfMissing) return null;
        const set = new Set();
        pendingEnterKeysByContainer.set(container, set);
        return set;
    }

    // 同步执行 FLIP 反演与出入场启动，确保在新 DOM 首次绘制（First Paint）前挂上反向位移，杜绝 1 帧闪烁
    function enqueue(events) {
        if (!Array.isArray(events) || events.length === 0) return;
        queuedEvents.push(...events);
        flushQueue();
    }

    function eventSignature(event) {
        const containerId = event?.containerId ?? 'container-unknown';
        const key = event?.key ?? 'key-unknown';
        return `${event?.type || 'unknown'}:${containerId}:${key}`;
    }

    function flushQueue() {
        if (!isPageActive()) {
            queuedEvents = [];
            return;
        }

        const events = queuedEvents;
        queuedEvents = [];
        if (events.length === 0) return;

        const dedupedBySignature = new Map();
        events.forEach(event => {
            if (!event || !event.type) return;
            dedupedBySignature.set(eventSignature(event), event);
        });

        const orderedEvents = Array.from(dedupedBySignature.values())
            .sort((left, right) => {
                const leftPriority = PHASE_PRIORITY[left.type] ?? Number.MAX_SAFE_INTEGER;
                const rightPriority = PHASE_PRIORITY[right.type] ?? Number.MAX_SAFE_INTEGER;
                return leftPriority - rightPriority;
            });

        const containersWithExit = new Set();
        orderedEvents.forEach(event => {
            if (event?.type !== 'exit') return;
            const containerId = String(event?.containerId ?? '');
            if (!containerId) return;
            containersWithExit.add(containerId);
        });

        const staggerIndexByGroup = new Map();
        orderedEvents.forEach(event => {
            const groupKey = `${event?.type ?? 'unknown'}:${event?.containerId ?? 'container-unknown'}`;
            const currentIndex = staggerIndexByGroup.get(groupKey) ?? 0;
            staggerIndexByGroup.set(groupKey, currentIndex + 1);

            const staggerUnitMs = event?.type === 'enter'
                ? stagger.enterMs
                : event?.type === 'move'
                    ? stagger.moveMs
                    : stagger.exitMs;
            const rawDelayMs = currentIndex * staggerUnitMs;
            const delayMs = stagger.maxMs > 0 ? Math.min(rawDelayMs, stagger.maxMs) : rawDelayMs;
            const containerId = String(event?.containerId ?? '');
            const phaseDelayMs = (
                event?.type === 'enter'
                && sequencing.enterAfterExitMs > 0
                && containerId
                && containersWithExit.has(containerId)
            )
                ? sequencing.enterAfterExitMs
                : 0;
            const totalDelayMs = phaseDelayMs + delayMs;

            if (totalDelayMs > 0 && event?.type !== 'move') {
                if (event?.type === 'enter') {
                    const targetEl = resolveCurrentElement(event);
                    targetEl?.classList?.add('card-enter-hidden');
                }
                scheduleTimeout(() => runEvent(event), totalDelayMs);
                return;
            }
            runEvent(event);
        });
    }

    function resolveCurrentElement(event) {
        if (event?.element?.isConnected) {
            return event.element;
        }
        const container = event?.container;
        const key = String(event?.key ?? '').trim();
        if (!container || !key) {
            return event?.element ?? null;
        }
        const escapedKey = escapeRenderKeyForSelector(key);
        return container.querySelector(`[data-render-key="${escapedKey}"]`) ?? event?.element ?? null;
    }

    function runEvent(event) {
        if (!event) return;
        if (supportsReducedMotion()) {
            const el = resolveCurrentElement(event);
            el?.classList?.remove('card-enter-hidden', 'hand-card-enter-hidden');
            return;
        }
        if (!isPageActive()) return;

        if (event.type === 'enter') {
            runEnterAnimation(resolveCurrentElement(event));
            return;
        }
        if (event.type === 'move') {
            runMoveAnimation(resolveCurrentElement(event), event.previousRect);
            return;
        }
        if (event.type === 'exit') {
            runExitOverlayAnimation(event.element, event.rect);
        }
    }

    function runEnterAnimation(element) {
        if (!element?.isConnected) return;
        clearTransientClasses(element);
        const enterClass = resolveEnterClass(element);
        element.classList.add(enterClass);
        const cleanup = () => {
            element.classList.remove('card-fade-enter', 'hand-card-fade-enter-down');
        };
        element.addEventListener('animationend', cleanup, { once: true });
        scheduleTimeout(cleanup, durations.enterMs + 80);
    }

    // 使用 WAAPI 独立 translate 属性驱动 FLIP，与手牌扇形 transform 及 :hover 完全正交，零内联样式残留
    function runMoveAnimation(element, previousRect) {
        if (!element?.isConnected || !isValidRect(previousRect)) return;
        const nextRect = element.getBoundingClientRect();
        if (!isValidRect(nextRect)) return;

        const prevCx = previousRect.left + previousRect.width / 2;
        const prevCy = previousRect.top + previousRect.height / 2;
        const nextCx = nextRect.left + nextRect.width / 2;
        const nextCy = nextRect.top + nextRect.height / 2;
        const deltaX = prevCx - nextCx;
        const deltaY = prevCy - nextCy;

        if (Math.hypot(deltaX, deltaY) <= moveOptions.epsilon) {
            return;
        }

        clearTransientClasses(element);
        const prevAnim = moveAnimationsByElement.get(element);
        if (prevAnim) {
            try { prevAnim.cancel(); } catch { /* ignore */ }
        }

        if (typeof element.animate === 'function') {
            const anim = trackWaapi(element.animate(
                [
                    { translate: `${deltaX.toFixed(1)}px ${deltaY.toFixed(1)}px` },
                    { translate: '0px 0px' }
                ],
                {
                    duration: durations.moveMs,
                    easing: 'cubic-bezier(0.22, 1, 0.36, 1)'
                }
            ));
            if (anim) {
                moveAnimationsByElement.set(element, anim);
            }
        }
    }

    function runExitOverlayAnimation(sourceElement, sourceRect) {
        if (!sourceElement || !isValidRect(sourceRect) || isOffViewport(sourceRect)) {
            return;
        }
        const transientRoot = ensureTransientRoot();
        if (!transientRoot) return;

        const clone = sourceElement.cloneNode(true);
        clearTransientClasses(clone);
        clone.classList.add(resolveExitClass(sourceElement), 'card-fade-exit-overlay');
        clone.style.position = 'fixed';
        clone.style.left = `${sourceRect.left}px`;
        clone.style.top = `${sourceRect.top}px`;
        clone.style.width = `${sourceRect.width}px`;
        clone.style.height = `${sourceRect.height}px`;
        clone.style.margin = '0';
        clone.style.pointerEvents = 'none';
        clone.style.zIndex = '9800';
        transientRoot.appendChild(clone);

        const cleanup = () => clone.remove();
        clone.addEventListener('animationend', cleanup, { once: true });
        scheduleTimeout(cleanup, durations.exitMs + 120);
    }

    function renderCollection(container, entries = [], options = {}) {
        if (!container) return;

        // 默认开启结构变化时的存活卡牌 FLIP 让位/聚拢
        const suppressMoveOnStructuralChange = options?.suppressMoveOnStructuralChange === true;
        const animateEnter = options?.animateEnter !== false;
        const animateExit = options?.animateExit !== false;
        const animateMove = options?.animateMove !== false;
        const animateInitialEnter = options?.animateInitialEnter !== false;

        const containerSeenBefore = renderedContainers.has(container);
        const oldElements = Array.from(container.children)
            .filter(child => child?.classList?.contains('card') || child?.classList?.contains('card-back'));
        const oldByKey = new Map();
        const oldRects = new Map();
        oldElements.forEach((element, index) => {
            const key = resolveRenderedCardKey(element, index);
            oldByKey.set(key, element);
            oldRects.set(key, element.getBoundingClientRect());
        });

        const nextItems = [];
        const nextKeySet = new Set();
        const pendingEnterKeys = getPendingEnterSet(container, true);

        entries.forEach((entry, index) => {
            if (!entry || !entry.element) return;
            const key = String(entry.key ?? `next-${index}`);
            clearTransientClasses(entry.element);
            const oldElement = oldByKey.get(key);
            let resolvedElement = entry.element;
            if (oldElement && canReconcileCardInPlace(oldElement, entry.element)) {
                resolvedElement = reconcileCardElementInPlace(oldElement, entry.element, preserveStateClasses);
            } else if (oldElement) {
                preserveStateClasses(oldElement, entry.element);
            } else {
                pendingEnterKeys.add(key);
            }
            resolvedElement.dataset.renderKey = key;
            onElementMounted(key, resolvedElement);
            nextItems.push({ key, element: resolvedElement });
            nextKeySet.add(key);
        });

        Array.from(pendingEnterKeys).forEach(key => {
            if (!nextKeySet.has(key)) {
                pendingEnterKeys.delete(key);
            }
        });

        const containerId = getContainerId(container);
        const events = [];

        if (animateExit) {
            oldByKey.forEach((oldElement, key) => {
                if (nextKeySet.has(key)) return;
                if (shouldSkipExit(key, oldElement)) return;
                const oldRect = oldRects.get(key);
                if (!oldRect) return;
                events.push({
                    type: 'exit',
                    container,
                    containerId,
                    key,
                    element: oldElement,
                    rect: oldRect
                });
            });
        }

        const removedCount = Array.from(oldByKey.keys()).filter(key => !nextKeySet.has(key)).length;
        const addedCount = nextItems.filter(item => !oldByKey.has(item.key)).length;
        const hasStructuralChange = removedCount > 0 || addedCount > 0;
        const shouldAnimateMove = !(suppressMoveOnStructuralChange && hasStructuralChange);

        const nextElementSet = new Set(nextItems.map(item => item.element));
        oldElements.forEach(oldElement => {
            if (!nextElementSet.has(oldElement) && oldElement.parentElement === container) {
                oldElement.remove();
            }
        });

        let cursor = container.firstElementChild;
        nextItems.forEach(({ element }) => {
            while (
                cursor
                && cursor !== element
                && !cursor.classList?.contains('card')
                && !cursor.classList?.contains('card-back')
            ) {
                cursor = cursor.nextElementSibling;
            }
            if (cursor === element) {
                cursor = cursor.nextElementSibling;
            } else {
                container.insertBefore(element, cursor);
            }
        });
        renderedContainers.add(container);

        nextItems.forEach(item => {
            const skipEnter = pendingEnterKeys.has(item.key) && shouldSkipEnter(item.key, item.element);
            if (skipEnter) {
                pendingEnterKeys.delete(item.key);
                return;
            }

            const shouldAnimatePendingEnter = animateEnter
                && pendingEnterKeys.has(item.key)
                && (animateInitialEnter || containerSeenBefore || oldByKey.size > 0);

            if (shouldAnimatePendingEnter) {
                events.push({
                    type: 'enter',
                    container,
                    containerId,
                    key: item.key,
                    element: item.element
                });
                pendingEnterKeys.delete(item.key);
                return;
            }

            pendingEnterKeys.delete(item.key);

            const previousRect = oldRects.get(item.key);
            if (previousRect && shouldAnimateMove && animateMove) {
                events.push({
                    type: 'move',
                    container,
                    containerId,
                    key: item.key,
                    element: item.element,
                    previousRect
                });
            }
        });

        enqueue(events);
    }

    function animateEnter(element, key = null, container = null) {
        if (!element) return;
        const resolvedKey = String(key ?? element?.dataset?.renderKey ?? element?.dataset?.uid ?? 'enter-unknown');
        if (shouldSkipEnter(resolvedKey, element)) return;
        enqueue([{
            type: 'enter',
            container,
            containerId: getContainerId(container),
            key: resolvedKey,
            element
        }]);
    }

    function animateLeaveOverlay(element, rect, key = null, container = null) {
        if (!element || !rect) return;
        const resolvedKey = String(key ?? element?.dataset?.renderKey ?? element?.dataset?.uid ?? 'exit-unknown');
        if (shouldSkipExit(resolvedKey, element)) return;
        enqueue([{
            type: 'exit',
            container,
            containerId: getContainerId(container),
            key: resolvedKey,
            element,
            rect
        }]);
    }

    function dispose() {
        queuedEvents = [];
        activeWaapiAnimations.forEach(anim => {
            try { anim.cancel(); } catch { /* ignore */ }
        });
        activeWaapiAnimations.clear();
    }

    return {
        renderCollection,
        animateEnter,
        animateLeaveOverlay,
        dispose
    };
}
