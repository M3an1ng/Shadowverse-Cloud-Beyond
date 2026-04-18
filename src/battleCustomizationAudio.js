function normalizeErrorMessage(error) {
    const message = String(error?.message ?? '').trim();
    return message || '音频播放失败。';
}

export function isAutoplayBlockedError(error) {
    const name = String(error?.name ?? '').trim();
    const message = String(error?.message ?? '').trim().toLowerCase();
    return name === 'NotAllowedError'
        || message.includes('notallowederror')
        || message.includes('user gesture')
        || message.includes('play() failed because');
}

export function createBattleCustomizationAudioController(options = {}) {
    const audioElement = options.audioElement
        || (typeof Audio === 'function'
            ? new Audio()
            : (typeof document !== 'undefined' ? document.createElement('audio') : null));
    const createObjectUrl = options.createObjectURL || (typeof URL !== 'undefined' ? URL.createObjectURL.bind(URL) : null);
    const revokeObjectUrl = options.revokeObjectURL || (typeof URL !== 'undefined' ? URL.revokeObjectURL.bind(URL) : null);
    const onBrokenAsset = typeof options.onBrokenAsset === 'function' ? options.onBrokenAsset : null;

    if (audioElement) {
        audioElement.loop = true;
        audioElement.preload = 'auto';
    }

    let currentObjectUrl = '';
    const state = {
        enabled: true,
        status: 'idle',
        assetName: '',
        autoplayBlocked: false,
        pendingInteractionRetry: false,
        message: ''
    };

    function getState() {
        return { ...state };
    }

    function releaseObjectUrl() {
        if (currentObjectUrl && revokeObjectUrl) {
            revokeObjectUrl(currentObjectUrl);
        }
        currentObjectUrl = '';
    }

    function resetState() {
        state.status = 'idle';
        state.assetName = '';
        state.autoplayBlocked = false;
        state.pendingInteractionRetry = false;
        state.message = '';
    }

    function notifyBrokenAsset(error) {
        if (onBrokenAsset) {
            onBrokenAsset(error);
        }
    }

    if (audioElement) {
        audioElement.addEventListener('error', () => {
            if (!audioElement.src) {
                return;
            }
            state.status = 'error';
            state.message = '本地 BGM 资源加载失败。';
            state.autoplayBlocked = false;
            state.pendingInteractionRetry = false;
            notifyBrokenAsset(audioElement.error || new Error(state.message));
        });
    }

    async function play() {
        if (!audioElement || !audioElement.src || !state.enabled) {
            return getState();
        }

        try {
            const playResult = audioElement.play();
            if (playResult && typeof playResult.then === 'function') {
                await playResult;
            }
            state.status = 'playing';
            state.autoplayBlocked = false;
            state.pendingInteractionRetry = false;
            state.message = '';
        } catch (error) {
            if (isAutoplayBlockedError(error)) {
                state.status = 'blocked';
                state.autoplayBlocked = true;
                state.pendingInteractionRetry = true;
                state.message = '浏览器阻止了自动播放，点击后可重试。';
            } else {
                state.status = 'error';
                state.autoplayBlocked = false;
                state.pendingInteractionRetry = false;
                state.message = normalizeErrorMessage(error);
                notifyBrokenAsset(error);
            }
        }

        return getState();
    }

    function pause() {
        if (audioElement && typeof audioElement.pause === 'function') {
            audioElement.pause();
        }
        if (state.status === 'playing') {
            state.status = 'ready';
        }
        return getState();
    }

    function setEnabled(enabled) {
        state.enabled = enabled !== false;
        if (!state.enabled) {
            pause();
        }
        return getState();
    }

    async function applyAsset(assetRecord, options = {}) {
        clear();

        if (!assetRecord?.blob || !createObjectUrl || !audioElement) {
            return getState();
        }

        currentObjectUrl = createObjectUrl(assetRecord.blob);
        audioElement.src = currentObjectUrl;
        audioElement.loop = true;
        state.assetName = String(assetRecord.fileName ?? '').trim();
        state.status = 'ready';
        state.message = '';
        state.autoplayBlocked = false;
        state.pendingInteractionRetry = false;

        if (options.attemptPlayback !== false && state.enabled) {
            return await play();
        }

        return getState();
    }

    async function retryAfterInteraction() {
        if (!state.pendingInteractionRetry || !state.enabled) {
            return getState();
        }
        return await play();
    }

    function clear() {
        if (audioElement && typeof audioElement.pause === 'function') {
            audioElement.pause();
        }
        if (audioElement) {
            audioElement.removeAttribute('src');
            if (typeof audioElement.load === 'function') {
                try {
                    audioElement.load();
                } catch {
                    // Ignore reset failures.
                }
            }
        }
        releaseObjectUrl();
        resetState();
        return getState();
    }

    function destroy() {
        clear();
        if (audioElement) {
            audioElement.remove();
        }
    }

    return {
        applyAsset,
        clear,
        destroy,
        getState,
        pause,
        play,
        retryAfterInteraction,
        setEnabled
    };
}

