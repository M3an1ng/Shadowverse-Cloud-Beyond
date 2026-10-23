const cardArtLoadPromises = new Map();
const queuedCardArtTasks = new Map();
const pendingCardArtTasks = [];
const CARD_ART_RETRY_COUNT = 2;
const CARD_ART_RETRY_DELAY_MS = 120;
const CARD_ART_PRELOAD_CONCURRENCY = 2;
const CARD_ART_PRELOAD_IDLE_TIMEOUT_MS = 180;
const CARD_ART_PRELOAD_DRAG_DEFER_MS = 220;
const CARD_ART_PRIORITY_RANK = Object.freeze({
    high: 0,
    normal: 1,
    low: 2
});

let activeCardArtLoadCount = 0;
let cardArtQueueDeferredUntilMs = 0;
let cardArtQueueIdleHandle = null;
let cardArtQueueTimerId = null;

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

function getNowMs() {
    if (typeof performance !== 'undefined' && typeof performance.now === 'function') {
        return performance.now();
    }
    return Date.now();
}

function getCardArtPriorityRank(priority = 'normal') {
    return CARD_ART_PRIORITY_RANK[priority] ?? CARD_ART_PRIORITY_RANK.normal;
}

function clearScheduledCardArtQueueDrain() {
    if (cardArtQueueIdleHandle !== null && typeof window !== 'undefined' && typeof window.cancelIdleCallback === 'function') {
        window.cancelIdleCallback(cardArtQueueIdleHandle);
    }
    if (cardArtQueueTimerId !== null && typeof window !== 'undefined') {
        window.clearTimeout(cardArtQueueTimerId);
    }
    cardArtQueueIdleHandle = null;
    cardArtQueueTimerId = null;
}

function queueCardArtTask(task) {
    const nextRank = getCardArtPriorityRank(task?.priority);
    const existingIndex = pendingCardArtTasks.findIndex(candidate => candidate?.url === task?.url);
    if (existingIndex >= 0) {
        const [existingTask] = pendingCardArtTasks.splice(existingIndex, 1);
        task = existingTask;
    }

    let insertIndex = pendingCardArtTasks.length;
    for (let index = 0; index < pendingCardArtTasks.length; index += 1) {
        const candidateRank = getCardArtPriorityRank(pendingCardArtTasks[index]?.priority);
        if (nextRank < candidateRank) {
            insertIndex = index;
            break;
        }
    }
    pendingCardArtTasks.splice(insertIndex, 0, task);
}

function drainCardArtQueue() {
    clearScheduledCardArtQueueDrain();

    if (typeof document !== 'undefined' && document.body?.classList?.contains('hand-card-dragging')) {
        scheduleCardArtQueueDrain({ preferIdle: false, delayMs: CARD_ART_PRELOAD_DRAG_DEFER_MS });
        return;
    }

    const deferredRemainingMs = cardArtQueueDeferredUntilMs - getNowMs();
    if (deferredRemainingMs > 0) {
        scheduleCardArtQueueDrain({ preferIdle: false, delayMs: deferredRemainingMs });
        return;
    }

    while (activeCardArtLoadCount < CARD_ART_PRELOAD_CONCURRENCY && pendingCardArtTasks.length > 0) {
        const task = pendingCardArtTasks.shift();
        if (!task) continue;

        activeCardArtLoadCount += 1;
        const completeTask = () => {
            activeCardArtLoadCount = Math.max(0, activeCardArtLoadCount - 1);
            if (pendingCardArtTasks.length > 0) {
                scheduleCardArtQueueDrain();
            }
        };

        loadCardArtUrl(task.url)
            .then(task.resolve)
            .catch(task.reject)
            .finally(completeTask);
    }
}

function scheduleCardArtQueueDrain({ preferIdle = true, delayMs = 0 } = {}) {
    clearScheduledCardArtQueueDrain();

    const safeDelayMs = Math.max(0, Math.ceil(Number(delayMs) || 0));
    const runDrain = () => drainCardArtQueue();

    if (safeDelayMs > 0) {
        cardArtQueueTimerId = window.setTimeout(runDrain, safeDelayMs);
        return;
    }

    if (
        preferIdle
        && typeof window !== 'undefined'
        && typeof window.requestIdleCallback === 'function'
    ) {
        cardArtQueueIdleHandle = window.requestIdleCallback(runDrain, { timeout: CARD_ART_PRELOAD_IDLE_TIMEOUT_MS });
        return;
    }

    cardArtQueueTimerId = window.setTimeout(runDrain, 0);
}

function normalizeArtPath(rawPath) {
    const trimmed = String(rawPath ?? '').trim();
    if (!trimmed) return '';
    if (/^data:image\//i.test(trimmed) || /^(https?:)?\/\//i.test(trimmed)) {
        return trimmed;
    }

    let normalized = trimmed.replace(/\\/g, '/');
    if (!normalized.startsWith('/')) {
        if (normalized.startsWith('assets/')) {
            normalized = `/${normalized}`;
        } else if (!normalized.startsWith('./') && !normalized.startsWith('../')) {
            normalized = `/${normalized}`;
        }
    }

    return normalized;
}

function withRetryQuery(url, attempt) {
    const separator = url.includes('?') ? '&' : '?';
    return `${url}${separator}r=${Date.now()}_${attempt}`;
}

async function probeImage(url) {
    await new Promise((resolve, reject) => {
        const image = new Image();
        image.decoding = 'async';
        image.onload = () => resolve();
        image.onerror = () => reject(new Error(`Image load failed: ${url}`));
        image.src = url;
    });
}

async function loadCardArtUrl(url) {
    for (let attempt = 0; attempt <= CARD_ART_RETRY_COUNT; attempt++) {
        const targetUrl = attempt === 0 ? url : withRetryQuery(url, attempt);
        try {
            await probeImage(targetUrl);
            return url;
        } catch (error) {
            if (attempt >= CARD_ART_RETRY_COUNT) {
                throw error;
            }
            await delay(CARD_ART_RETRY_DELAY_MS * (attempt + 1));
        }
    }
    return url;
}

export function resolveArtUrl(rawPath) {
    const normalized = normalizeArtPath(rawPath);
    if (!normalized) return '';
    return encodeURI(normalized).replace(/'/g, '%27');
}

export function deferCardArtQueue(durationMs = CARD_ART_PRELOAD_DRAG_DEFER_MS) {
    const safeDuration = Math.max(0, Number(durationMs) || 0);
    if (safeDuration <= 0) return;
    cardArtQueueDeferredUntilMs = Math.max(cardArtQueueDeferredUntilMs, getNowMs() + safeDuration);
    scheduleCardArtQueueDrain({ preferIdle: false, delayMs: safeDuration });
}

export function preloadCardArt(rawPath, options = {}) {
    const url = resolveArtUrl(rawPath);
    if (!url) {
        return Promise.resolve('');
    }

    const existing = cardArtLoadPromises.get(url);
    if (existing) {
        const queuedTask = queuedCardArtTasks.get(url);
        if (queuedTask && getCardArtPriorityRank(options?.priority) < getCardArtPriorityRank(queuedTask.priority)) {
            queuedTask.priority = options.priority;
            queueCardArtTask(queuedTask);
            scheduleCardArtQueueDrain({ preferIdle: options?.priority !== 'high' });
        }
        return existing;
    }

    let resolveLoad;
    let rejectLoad;
    const loadPromise = new Promise((resolve, reject) => {
        resolveLoad = resolve;
        rejectLoad = reject;
    });

    const guardedPromise = loadPromise.catch(error => {
        cardArtLoadPromises.delete(url);
        queuedCardArtTasks.delete(url);
        throw error;
    });

    cardArtLoadPromises.set(url, guardedPromise);
    queuedCardArtTasks.set(url, {
        url,
        priority: options?.priority ?? 'normal',
        resolve: loadedUrl => {
            queuedCardArtTasks.delete(url);
            resolveLoad(loadedUrl);
        },
        reject: error => {
            queuedCardArtTasks.delete(url);
            rejectLoad(error);
        }
    });
    queueCardArtTask(queuedCardArtTasks.get(url));
    scheduleCardArtQueueDrain({ preferIdle: options?.priority !== 'high' });
    return guardedPromise;
}

export function applyCardArtBackground(artEl, rawPath, options = {}) {
    if (!artEl) return;
    const resolvedUrl = resolveArtUrl(rawPath);
    artEl.dataset.artUrl = resolvedUrl;

    if (!resolvedUrl) {
        artEl.style.backgroundImage = '';
        artEl.classList.remove('has-art');
        return;
    }

    void preloadCardArt(rawPath, options)
        .then(loadedUrl => {
            if (artEl.dataset.artUrl !== loadedUrl) return;
            artEl.style.backgroundImage = `url('${loadedUrl}')`;
            artEl.classList.add('has-art');
        })
        .catch(() => {
            if (artEl.dataset.artUrl !== resolvedUrl) return;
            artEl.style.backgroundImage = '';
            artEl.classList.remove('has-art');
        });
}
