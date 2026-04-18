const PHASE_PRIORITY = Object.freeze({
    exit: 10,
    move: 20,
    enter: 30
});

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
        // If a container has exit events in the same batch, delay enter events by this amount.
        enterAfterExitMs: Math.max(0, Number(config?.sequencing?.enterAfterExitMs ?? durations.exitMs) || 0)
    };
    const stagger = {
        enterMs: Math.max(0, Number(config?.stagger?.enterMs ?? 0) || 0),
        moveMs: Math.max(0, Number(config?.stagger?.moveMs ?? 0) || 0),
        exitMs: Math.max(0, Number(config?.stagger?.exitMs ?? 0) || 0),
        maxMs: Math.max(0, Number(config?.stagger?.maxMs ?? 0) || 0)
    };
    const moveOptions = {
        epsilon: config?.move?.epsilon ?? 0.5,
        opacity: config?.move?.opacity ?? '0.82'
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

    const renderedContainers = new WeakSet();
    const pendingEnterKeysByContainer = new WeakMap();
    const containerIds = new WeakMap();
    let containerIdCounter = 1;

    let queuedEvents = [];
    let frameScheduled = false;
    let frameHandle = null;

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

    function scheduleFlush() {
        if (frameScheduled) return;
        frameScheduled = true;
        frameHandle = window.requestAnimationFrame(() => {
            frameScheduled = false;
            frameHandle = null;
            flushQueue();
        });
    }

    function enqueue(events) {
        if (!Array.isArray(events) || events.length === 0) return;
        queuedEvents.push(...events);
        scheduleFlush();
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

            if (totalDelayMs > 0) {
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
        if (supportsReducedMotion()) return;
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

    function runMoveAnimation(element, previousRect) {
        if (!element?.isConnected || !isValidRect(previousRect)) return;
        const nextRect = element.getBoundingClientRect();
        const deltaX = previousRect.left - nextRect.left;
        const deltaY = previousRect.top - nextRect.top;
        if (Math.abs(deltaX) <= moveOptions.epsilon && Math.abs(deltaY) <= moveOptions.epsilon) {
            return;
        }

        clearTransientClasses(element);
        element.classList.add('card-fade-move');
        element.style.transform = `translate(${deltaX}px, ${deltaY}px)`;
        element.style.opacity = moveOptions.opacity;
        void element.offsetWidth;

        requestAnimationFrame(() => {
            element.style.transform = '';
            element.style.opacity = '';
        });

        const cleanup = () => {
            element.classList.remove('card-fade-move');
            if (!element.matches(':hover')) {
                element.style.transform = '';
            }
            element.style.opacity = '';
        };

        const onTransitionEnd = event => {
            if (event.propertyName !== 'transform') return;
            cleanup();
            element.removeEventListener('transitionend', onTransitionEnd);
        };

        element.addEventListener('transitionend', onTransitionEnd);
        scheduleTimeout(() => {
            cleanup();
            element.removeEventListener('transitionend', onTransitionEnd);
        }, durations.moveMs + 90);
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

        const suppressMoveOnStructuralChange = options?.suppressMoveOnStructuralChange !== false;
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
            if (oldElement) {
                preserveStateClasses(oldElement, entry.element);
            } else {
                pendingEnterKeys.add(key);
            }
            entry.element.dataset.renderKey = key;
            nextItems.push({ key, element: entry.element });
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

        container.replaceChildren(...nextItems.map(item => item.element));
        renderedContainers.add(container);

        nextItems.forEach(item => {
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

            // Initial render explicitly disabled: consume pending enter markers.
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
        enqueue([{
            type: 'enter',
            container,
            containerId: getContainerId(container),
            key: String(key ?? element?.dataset?.renderKey ?? element?.dataset?.uid ?? 'enter-unknown'),
            element
        }]);
    }

    function animateLeaveOverlay(element, rect, key = null, container = null) {
        if (!element || !rect) return;
        enqueue([{
            type: 'exit',
            container,
            containerId: getContainerId(container),
            key: String(key ?? element?.dataset?.renderKey ?? element?.dataset?.uid ?? 'exit-unknown'),
            element,
            rect
        }]);
    }

    function dispose() {
        queuedEvents = [];
        if (frameHandle !== null) {
            window.cancelAnimationFrame(frameHandle);
            frameHandle = null;
            frameScheduled = false;
        }
    }

    return {
        renderCollection,
        animateEnter,
        animateLeaveOverlay,
        dispose
    };
}
