// src/ui/logPanel.js
import { gameState, findCardInstanceByUid, sendChatMessage, sendEmoteMessage } from '../gameManager.js';
import { ensureCardDefinitions, getAbilityDataSnapshot } from '../abilityDataCache.js';
import { getUserSession } from '../userSession.js';
import { hasUsableEmotes, loadEmoteCatalog } from '../emoteCatalog.js';

function normalizeArtPath(rawPath) {
    if (rawPath === null || rawPath === undefined) return '';

    const trimmed = String(rawPath).trim();
    if (!trimmed) return '';

    if (/^(https?:)?\/\//i.test(trimmed) || trimmed.startsWith('data:')) {
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

function resolveArtUrl(rawPath) {
    const normalized = normalizeArtPath(rawPath);
    if (!normalized) return '';
    return encodeURI(normalized).replace(/'/g, '%27');
}

function resolveAvatarImageSource(rawValue) {
    const text = String(rawValue ?? '').trim();
    if (!text) return '';
    if (/^data:image\//i.test(text)) return text;
    if (/^(https?:)?\/\//i.test(text)) return text;
    if (text.startsWith('/') || text.startsWith('./') || text.startsWith('../')) {
        return encodeURI(text).replace(/'/g, '%27');
    }
    return '';
}

function resolvePlayerOrderBadge(playerState) {
    const raw = playerState?.isFirst ?? playerState?.IsFirst;
    if (typeof raw !== 'boolean') {
        return null;
    }
    return raw
        ? { text: '先手', className: 'first' }
        : { text: '后手', className: 'second' };
}
let isLogExpanded = true; // Default to expanded
const LOG_RENDER_LIMIT = 200; // Keep in sync with GAME_LOG_LIMIT in gameManager.js
const LOG_AUTO_SCROLL_THRESHOLD_PX = 12;
const LOG_PANEL_VIEW_LOG = 'log';
const LOG_PANEL_VIEW_CHAT = 'chat';
const LOG_PANEL_VIEW_USED_HISTORY = 'history-used';
const LOG_PANEL_VIEW_DESTROYED_HISTORY = 'history-destroyed';
const LOG_PANEL_CHAT_HISTORY_LIMIT = 200;
const MATCH_USED_CARD_HISTORY_LIMIT = 300;
const MATCH_DESTROYED_CARD_HISTORY_LIMIT = 300;
const MATCH_HISTORY_SIDE_FRIENDLY = 'friendly';
const MATCH_HISTORY_SIDE_ENEMY = 'enemy';
const MATCH_HISTORY_RECORD_USED = 'used';
const MATCH_HISTORY_RECORD_DESTROYED = 'destroyed';
const USED_CARD_HISTORY_STORAGE_PREFIX = 'svcb.used-card-history:';
const DESTROYED_CARD_HISTORY_STORAGE_PREFIX = 'svcb.destroyed-card-history:';
const CHAT_BARRAGE_MAX_QUEUE = 30;
const CHAT_BARRAGE_LANE_TOPS = [7, 35, 63, 91, 119];
let logPanelActiveView = LOG_PANEL_VIEW_LOG;
let shouldScrollLogToLatestOnRender = false;
let shouldScrollChatToLatestOnRender = false;
let shouldScrollHistoryToLatestOnRender = false;
let activeMatchHistorySide = MATCH_HISTORY_SIDE_FRIENDLY;
let activeMatchHistoryRecordMode = MATCH_HISTORY_RECORD_USED;
let logPanelChatDraft = '';
let roomChatMessages = [];
let usedCardHistoryEntries = [];
let destroyedCardHistoryEntries = [];
let hydratedUsedCardHistoryGameId = '';
let hydratedDestroyedCardHistoryGameId = '';
const historyScrollTopByViewState = new Map();
const roomChatMessageIdSet = new Set();
const spectatorAliasBySenderKey = new Map();
let chatSendStatus = '';
let chatSendStatusTone = 'neutral';
let isChatSending = false;
let isEmotePanelOpen = false;
let emoteCatalogLoadState = 'idle';
let emoteCatalog = null;
let selectedEmotePackId = '';
let isEmoteSending = false;
const chatBarrageQueue = [];
const chatBarrageActiveLanes = new Set();

function getLatestGameLogs(fallbackLogs) {
    if (Array.isArray(gameState.gameLog)) {
        return gameState.gameLog;
    }
    return Array.isArray(fallbackLogs) ? fallbackLogs : [];
}

function isGamePageActive() {
    const gamePage = document.getElementById('game-page');
    return !!gamePage && gamePage.classList.contains('active');
}

function getBarrageHostElement() {
    return document.getElementById('game-container') || document.body;
}

function isActiveChatTab() {
    return logPanelActiveView === LOG_PANEL_VIEW_CHAT;
}

function isActiveUsedHistoryTab() {
    return logPanelActiveView === LOG_PANEL_VIEW_USED_HISTORY;
}

function isActiveDestroyedHistoryTab() {
    return logPanelActiveView === LOG_PANEL_VIEW_DESTROYED_HISTORY;
}

function isActiveHistoryTab() {
    return isActiveUsedHistoryTab() || isActiveDestroyedHistoryTab();
}

function saveCurrentChatDraft() {
    if (!isActiveChatTab()) return;
    const input = document.getElementById('game-chat-input');
    if (!input) return;
    logPanelChatDraft = input.value || '';
}

function normalizeChatText(rawValue) {
    return String(rawValue || '').replace(/\s+/g, ' ').trim();
}

function formatChatTimestamp(timestamp) {
    const date = timestamp instanceof Date ? timestamp : new Date(timestamp);
    if (Number.isNaN(date.getTime())) {
        return '--:--';
    }
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${hours}:${minutes}`;
}

function setChatSendStatus(text, tone = 'neutral') {
    chatSendStatus = String(text || '').trim();
    chatSendStatusTone = tone === 'error' ? 'error' : (tone === 'success' ? 'success' : 'neutral');
}

function clearChatSendStatus() {
    chatSendStatus = '';
    chatSendStatusTone = 'neutral';
}

function closeEmotePanel() {
    isEmotePanelOpen = false;
    isEmoteSending = false;
}

function getFirstEmotePackId(catalog) {
    const pack = Array.isArray(catalog?.packs)
        ? catalog.packs.find(candidate => Array.isArray(candidate?.items) && candidate.items.length > 0)
        : null;
    return String(pack?.id || '').trim();
}

function getSelectedEmotePack() {
    const packs = Array.isArray(emoteCatalog?.packs) ? emoteCatalog.packs : [];
    return packs.find(pack => pack?.id === selectedEmotePackId)
        || packs.find(pack => Array.isArray(pack?.items) && pack.items.length > 0)
        || null;
}

function ensureEmoteCatalogLoaded() {
    if (emoteCatalogLoadState !== 'idle') return;
    emoteCatalogLoadState = 'loading';
    void loadEmoteCatalog().then(result => {
        if (result?.ok) {
            emoteCatalog = result.catalog;
            emoteCatalogLoadState = hasUsableEmotes(emoteCatalog) ? 'ready' : 'empty';
            selectedEmotePackId = selectedEmotePackId || getFirstEmotePackId(emoteCatalog);
        } else {
            emoteCatalog = null;
            emoteCatalogLoadState = 'failed';
            setChatSendStatus(result?.error || '表情清单加载失败。', 'error');
        }
        renderGameLog(getLatestGameLogs());
    });
}

function isEmoteChatMessage(message) {
    return String(message?.messageType || '').toLowerCase() === 'emote';
}

function getChatMessageClasses(message) {
    const classes = ['chat-message-entry'];
    if (isEmoteChatMessage(message)) {
        classes.push('is-emote');
    }
    if (message?.senderType === 'system') {
        classes.push('is-system');
    } else if (message?.senderType === 'spectator') {
        classes.push('is-spectator');
    } else if (message?.isSelf) {
        classes.push('is-self');
    } else {
        classes.push('is-opponent');
    }
    return classes.join(' ');
}

function createEmoteImageView(message, imageClassName, fallbackClassName) {
    const wrapper = document.createElement('div');
    wrapper.className = fallbackClassName.includes('barrage')
        ? 'chat-barrage-emote'
        : 'chat-message-emote';

    const label = String(message?.emoteName || message?.content || message?.emoteId || '表情').trim();
    const image = document.createElement('img');
    image.className = imageClassName;
    image.src = String(message?.emoteUrl || '').trim();
    image.alt = label;
    image.loading = 'lazy';
    image.onerror = () => {
        wrapper.innerHTML = '';
        const fallback = document.createElement('span');
        fallback.className = fallbackClassName;
        fallback.textContent = label;
        wrapper.appendChild(fallback);
    };
    wrapper.appendChild(image);
    return wrapper;
}

function renderEmotePanel(panel) {
    if (!panel) return;
    panel.hidden = !isEmotePanelOpen;
    panel.innerHTML = '';
    if (!isEmotePanelOpen) return;

    if (emoteCatalogLoadState === 'idle' || emoteCatalogLoadState === 'loading') {
        const state = document.createElement('div');
        state.className = 'chat-emote-state';
        state.textContent = '加载中...';
        panel.appendChild(state);
        return;
    }

    if (emoteCatalogLoadState === 'failed') {
        const state = document.createElement('div');
        state.className = 'chat-emote-state is-error';
        state.textContent = '表情加载失败';
        panel.appendChild(state);
        return;
    }

    if (emoteCatalogLoadState === 'empty' || !hasUsableEmotes(emoteCatalog)) {
        const state = document.createElement('div');
        state.className = 'chat-emote-state';
        state.textContent = '暂无表情';
        panel.appendChild(state);
        return;
    }

    const packs = emoteCatalog.packs.filter(pack => Array.isArray(pack?.items) && pack.items.length > 0);
    if (!packs.some(pack => pack.id === selectedEmotePackId)) {
        selectedEmotePackId = getFirstEmotePackId(emoteCatalog);
    }

    const tabs = document.createElement('div');
    tabs.className = 'chat-emote-tabs';
    packs.forEach(pack => {
        const tab = document.createElement('button');
        tab.type = 'button';
        tab.className = 'chat-emote-tab';
        tab.classList.toggle('is-active', pack.id === selectedEmotePackId);
        tab.textContent = pack.name || pack.id;
        tab.onclick = () => {
            selectedEmotePackId = pack.id;
            renderGameLog(getLatestGameLogs());
        };
        tabs.appendChild(tab);
    });

    const grid = document.createElement('div');
    grid.className = 'chat-emote-grid';
    const selectedPack = getSelectedEmotePack();
    (selectedPack?.items || []).forEach(item => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'chat-emote-item';
        button.disabled = isEmoteSending;
        button.title = item.name || item.id;
        button.onclick = async () => {
            if (isEmoteSending) return;
            isEmoteSending = true;
            setChatSendStatus('发送中...', 'neutral');
            renderGameLog(getLatestGameLogs());

            const result = await sendEmoteMessage(item.id);
            isEmoteSending = false;
            if (!result?.ok) {
                setChatSendStatus(result?.error || '表情发送失败。', 'error');
            } else {
                setChatSendStatus('已发送', 'success');
                window.setTimeout(() => {
                    if (chatSendStatusTone === 'success') {
                        clearChatSendStatus();
                        renderGameLog(getLatestGameLogs());
                    }
                }, 1400);
            }
            renderGameLog(getLatestGameLogs());
        };

        const image = document.createElement('img');
        image.src = item.url;
        image.alt = item.name || item.id;
        image.loading = 'lazy';
        image.onerror = () => {
            button.classList.add('is-broken');
            button.textContent = item.name || '表情';
        };
        button.appendChild(image);
        grid.appendChild(button);
    });

    panel.appendChild(tabs);
    panel.appendChild(grid);
}

function isGenericChatPlayerName(name) {
    const value = String(name || '').trim();
    if (!value) return true;
    return /^玩家\s*\d*$/i.test(value)
        || /^player\s*\d*$/i.test(value)
        || /^opponent$/i.test(value)
        || /^you$/i.test(value);
}

function resolveChatPlayerDisplayName(message) {
    const senderId = String(message?.senderId || '').trim().toLowerCase();
    if (senderId) {
        const players = Array.isArray(gameState.players) ? gameState.players : [];
        const matchedPlayer = players.find(player => String(player?.id || '').trim().toLowerCase() === senderId);
        if (matchedPlayer) {
            const localId = String(gameState.localPlayerId || '').trim().toLowerCase();
            const isLocal = localId.length > 0 && senderId === localId;
            const candidates = [
                matchedPlayer?.displayName,
                matchedPlayer?.DisplayName,
                matchedPlayer?.name,
                matchedPlayer?.Name,
                matchedPlayer?.username,
                matchedPlayer?.Username
            ];
            const explicit = candidates
                .map(value => String(value || '').trim())
                .find(value => value && !isGenericChatPlayerName(value));
            if (explicit) return explicit;

            if (isLocal) {
                const session = getUserSession();
                const sessionName = String(session?.displayName || '').trim();
                if (sessionName) return sessionName;
            }

            const fallback = candidates
                .map(value => String(value || '').trim())
                .find(value => value);
            if (fallback) return fallback;
        }

        const localId = String(gameState.localPlayerId || '').trim().toLowerCase();
        if (localId.length > 0 && senderId === localId) {
            const session = getUserSession();
            const sessionName = String(session?.displayName || '').trim();
            if (sessionName) return sessionName;
        }
    }
    return '';
}

function resolveChatAuthorLabel(message) {
    if (message?.senderType === 'system') return '系统';
    if (message?.senderType === 'spectator') {
        const explicitName = String(message?.senderName || '').trim();
        if (explicitName && !/^观战者$/i.test(explicitName) && !/^spectator$/i.test(explicitName)) {
            return explicitName;
        }
        if (message?.isSelf) {
            const session = getUserSession();
            const selfName = String(session?.displayName || '').trim();
            if (selfName) return selfName;
        }

        const rawKey = String(message?.senderId || message?.senderName || message?.messageId || '').trim();
        const key = rawKey || `spectator:${Math.random().toString(36).slice(2, 10)}`;
        const existing = spectatorAliasBySenderKey.get(key);
        if (existing) return existing;

        const code = Math.random().toString(36).slice(2, 8).toUpperCase();
        const alias = `游客${code}`;
        spectatorAliasBySenderKey.set(key, alias);
        return alias;
    }

    const rawName = String(message?.senderName || '').trim();
    if (!isGenericChatPlayerName(rawName)) {
        return rawName;
    }

    const resolvedName = resolveChatPlayerDisplayName(message);
    if (resolvedName) {
        return resolvedName;
    }

    return rawName || '玩家';
}

function resolveChatAvatarInfo(message, authorLabel) {
    const fallbackText = String(authorLabel || '聊').trim().slice(0, 1).toUpperCase() || '聊';

    if (message?.senderType === 'spectator') {
        const spectatorAvatar = resolveAvatarImageSource(
            message?.senderAvatarUrl
            ?? message?.SenderAvatarUrl
            ?? ''
        );
        if (spectatorAvatar) {
            return { imageSrc: spectatorAvatar, fallbackText };
        }
        if (message?.isSelf) {
            const session = getUserSession();
            const sessionAvatar = resolveAvatarImageSource(session?.avatar);
            if (sessionAvatar) {
                return { imageSrc: sessionAvatar, fallbackText };
            }
        }
        return { imageSrc: '', fallbackText };
    }

    if (message?.senderType !== 'player') {
        return { imageSrc: '', fallbackText };
    }

    const senderId = String(message?.senderId || '').trim().toLowerCase();
    if (!senderId) {
        return { imageSrc: '', fallbackText };
    }

    const players = Array.isArray(gameState.players) ? gameState.players : [];
    const matchedPlayer = players.find(player => String(player?.id || '').trim().toLowerCase() === senderId);
    if (!matchedPlayer) {
        const localId = String(gameState.localPlayerId || '').trim().toLowerCase();
        if (senderId === localId) {
            const session = getUserSession();
            const localImage = resolveAvatarImageSource(session?.avatar);
            if (localImage) {
                return { imageSrc: localImage, fallbackText };
            }
        }
        return { imageSrc: '', fallbackText };
    }

    const imageSrc = resolveAvatarImageSource(
        matchedPlayer?.avatarUrl
        ?? matchedPlayer?.AvatarUrl
        ?? matchedPlayer?.avatar
        ?? matchedPlayer?.Avatar
    );
    return { imageSrc, fallbackText };
}

function resolveChatPlayerOrderBadge(message) {
    if (message?.senderType !== 'player') {
        return null;
    }

    const senderId = String(message?.senderId || '').trim().toLowerCase();
    const players = Array.isArray(gameState.players) ? gameState.players : [];
    let matchedPlayer = null;

    if (senderId) {
        matchedPlayer = players.find(player => String(player?.id || '').trim().toLowerCase() === senderId) || null;
    }

    if (!matchedPlayer && message?.isSelf) {
        const localId = String(gameState.localPlayerId || '').trim().toLowerCase();
        if (localId) {
            matchedPlayer = players.find(player => String(player?.id || '').trim().toLowerCase() === localId) || null;
        }
    }

    if (!matchedPlayer) {
        return null;
    }

    return resolvePlayerOrderBadge(matchedPlayer);
}

function syncLogPanelChatToggleState(logContainer) {
    if (!logContainer) return;
    const logToggleBtn = logContainer.querySelector('#game-log-toggle');
    const chatToggleBtn = logContainer.querySelector('#game-chat-toggle');
    const historyToggleBtn = logContainer.querySelector('#game-history-toggle');
    const destroyedHistoryToggleBtn = logContainer.querySelector('#game-destroy-history-toggle');
    if (logToggleBtn) {
        logToggleBtn.classList.toggle('is-active', logPanelActiveView === LOG_PANEL_VIEW_LOG);
    }
    if (chatToggleBtn) {
        chatToggleBtn.classList.toggle('is-active', logPanelActiveView === LOG_PANEL_VIEW_CHAT);
    }
    if (historyToggleBtn) {
        historyToggleBtn.classList.toggle('is-active', isActiveUsedHistoryTab());
    }
    if (destroyedHistoryToggleBtn) {
        destroyedHistoryToggleBtn.classList.toggle('is-active', isActiveDestroyedHistoryTab());
    }
}

function bindLogPanelToggleHandlers(logContainer, fallbackLogs) {
    if (!logContainer) return;
    const logToggleBtn = logContainer.querySelector('#game-log-toggle');
    const chatToggleBtn = logContainer.querySelector('#game-chat-toggle');
    const historyToggleBtn = logContainer.querySelector('#game-history-toggle');
    const destroyedHistoryToggleBtn = logContainer.querySelector('#game-destroy-history-toggle');

    if (logToggleBtn) {
        logToggleBtn.onclick = () => {
            const switchingToLog = logPanelActiveView !== LOG_PANEL_VIEW_LOG;
            saveCurrentChatDraft();
            if (logPanelActiveView === LOG_PANEL_VIEW_LOG) {
                isLogExpanded = !isLogExpanded;
            } else {
                logPanelActiveView = LOG_PANEL_VIEW_LOG;
                isLogExpanded = true;
                if (switchingToLog) {
                    shouldScrollLogToLatestOnRender = true;
                }
            }
            renderGameLog(getLatestGameLogs(fallbackLogs));
        };
    }

    if (chatToggleBtn) {
        chatToggleBtn.onclick = () => {
            const switchingToChat = logPanelActiveView !== LOG_PANEL_VIEW_CHAT;
            saveCurrentChatDraft();
            if (logPanelActiveView === LOG_PANEL_VIEW_CHAT && isLogExpanded) {
                isLogExpanded = false;
            } else {
                logPanelActiveView = LOG_PANEL_VIEW_CHAT;
                isLogExpanded = true;
                if (switchingToChat) {
                    shouldScrollChatToLatestOnRender = true;
                }
            }
            renderGameLog(getLatestGameLogs(fallbackLogs));
        };
    }

    if (historyToggleBtn) {
        historyToggleBtn.onclick = () => {
            const switchingToHistory = !isActiveUsedHistoryTab();
            saveCurrentChatDraft();
            if (isActiveUsedHistoryTab() && isLogExpanded) {
                isLogExpanded = false;
            } else {
                logPanelActiveView = LOG_PANEL_VIEW_USED_HISTORY;
                activeMatchHistoryRecordMode = MATCH_HISTORY_RECORD_USED;
                isLogExpanded = true;
                if (switchingToHistory) {
                    shouldScrollHistoryToLatestOnRender = true;
                }
            }
            renderGameLog(getLatestGameLogs(fallbackLogs));
        };
    }

    if (destroyedHistoryToggleBtn) {
        destroyedHistoryToggleBtn.onclick = () => {
            const switchingToDestroyedHistory = !isActiveDestroyedHistoryTab();
            saveCurrentChatDraft();
            if (isActiveDestroyedHistoryTab() && isLogExpanded) {
                isLogExpanded = false;
            } else {
                logPanelActiveView = LOG_PANEL_VIEW_DESTROYED_HISTORY;
                activeMatchHistoryRecordMode = MATCH_HISTORY_RECORD_DESTROYED;
                isLogExpanded = true;
                if (switchingToDestroyedHistory) {
                    shouldScrollHistoryToLatestOnRender = true;
                }
            }
            renderGameLog(getLatestGameLogs(fallbackLogs));
        };
    }
}

function ensureChatPanelStructure(chatContent) {
    if (!chatContent) {
        return null;
    }

    let messageList = chatContent.querySelector('.chat-message-list');
    if (!messageList) {
        messageList = document.createElement('div');
        messageList.className = 'chat-message-list';
        chatContent.appendChild(messageList);
    }

    let status = chatContent.querySelector('.chat-send-status');
    if (!status) {
        status = document.createElement('div');
        status.className = 'chat-send-status';
        chatContent.appendChild(status);
    }

    let emotePanel = chatContent.querySelector('.chat-emote-panel');
    if (!emotePanel) {
        emotePanel = document.createElement('div');
        emotePanel.className = 'chat-emote-panel';
        emotePanel.hidden = true;
        chatContent.appendChild(emotePanel);
    }

    let form = chatContent.querySelector('.chat-input-form');
    let input = chatContent.querySelector('#game-chat-input');
    let emoteButton = chatContent.querySelector('.chat-emote-btn');
    let sendButton = chatContent.querySelector('.chat-send-btn');
    if (!form || !input) {
        form = document.createElement('form');
        form.className = 'chat-input-form';

        input = document.createElement('input');
        input.id = 'game-chat-input';
        input.className = 'chat-input';
        input.type = 'text';
        input.maxLength = 200;
        input.placeholder = '输入聊天消息';
        input.autocomplete = 'off';
        input.addEventListener('input', () => {
            if (!isActiveChatTab()) return;
            logPanelChatDraft = input.value || '';
            if (chatSendStatusTone === 'error') {
                clearChatSendStatus();
                renderGameLog(getLatestGameLogs());
            }
        });

        sendButton = document.createElement('button');
        sendButton.type = 'submit';
        sendButton.className = 'chat-send-btn';
        sendButton.textContent = '发送';

        emoteButton = document.createElement('button');
        emoteButton.type = 'button';
        emoteButton.className = 'chat-emote-btn';
        emoteButton.textContent = '☺';
        emoteButton.title = '表情';
        emoteButton.setAttribute('aria-label', '表情');
        emoteButton.addEventListener('click', () => {
            if (!isActiveChatTab()) return;
            isEmotePanelOpen = !isEmotePanelOpen;
            if (isEmotePanelOpen) {
                ensureEmoteCatalogLoaded();
            }
            renderGameLog(getLatestGameLogs());
        });

        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (!isActiveChatTab()) return;
            const normalized = normalizeChatText(input.value);
            if (!normalized) {
                setChatSendStatus('消息不能为空。', 'error');
                renderGameLog(getLatestGameLogs());
                return;
            }

            logPanelChatDraft = normalized;
            setChatSendStatus('发送中...', 'neutral');
            isChatSending = true;
            sendButton.disabled = true;
            renderGameLog(getLatestGameLogs());

            const result = await sendChatMessage(normalized);
            if (!result?.ok) {
                setChatSendStatus(result?.error || '发送失败，请稍后重试。', 'error');
                isChatSending = false;
                sendButton.disabled = false;
                renderGameLog(getLatestGameLogs());
                return;
            }

            logPanelChatDraft = '';
            setChatSendStatus('已发送', 'success');
            isChatSending = false;
            sendButton.disabled = false;
            renderGameLog(getLatestGameLogs());
            window.setTimeout(() => {
                if (chatSendStatusTone === 'success') {
                    clearChatSendStatus();
                    renderGameLog(getLatestGameLogs());
                }
            }, 1400);
        });

        form.appendChild(emoteButton);
        form.appendChild(input);
        form.appendChild(sendButton);
        chatContent.appendChild(form);
    }

    return { messageList, status, emotePanel, input, emoteButton, sendButton };
}

function renderActiveChatPanel(chatContent) {
    const structure = ensureChatPanelStructure(chatContent);
    if (!structure) return;
    bindTopShadowMask(structure.messageList, chatContent);

    if (!isActiveChatTab()) {
        structure.messageList.innerHTML = '';
        structure.messageList.scrollTop = 0;
        structure.status.textContent = '';
        closeEmotePanel();
        structure.input.value = '';
        renderEmotePanel(structure.emotePanel);
        syncTopShadowMask(structure.messageList, chatContent);
        return;
    }

    structure.input.value = logPanelChatDraft;
    structure.status.textContent = chatSendStatus || '';
    structure.status.dataset.tone = chatSendStatusTone;
    if (structure.sendButton) {
        structure.sendButton.disabled = isChatSending;
    }
    if (structure.emoteButton) {
        structure.emoteButton.classList.toggle('is-active', isEmotePanelOpen);
        structure.emoteButton.disabled = isChatSending;
    }
    renderEmotePanel(structure.emotePanel);

    const previousScrollTop = structure.messageList.scrollTop;
    const wasNearBottom = isLogNearBottom(structure.messageList);
    const history = roomChatMessages;

    structure.messageList.innerHTML = '';
    if (history.length > 0) {
        history.forEach(message => {
            const row = document.createElement('div');
            row.className = getChatMessageClasses(message);

            const authorLabel = resolveChatAuthorLabel(message);
            const avatarInfo = resolveChatAvatarInfo(message, authorLabel);

            const meta = document.createElement('div');
            meta.className = 'chat-message-meta';

            const avatar = document.createElement('span');
            avatar.className = 'chat-message-avatar';
            if (avatarInfo.imageSrc) {
                const image = document.createElement('img');
                image.className = 'chat-message-avatar-img';
                image.src = avatarInfo.imageSrc;
                image.alt = `${authorLabel} avatar`;
                image.onerror = () => {
                    avatar.classList.remove('has-image');
                    avatar.textContent = avatarInfo.fallbackText;
                };
                avatar.appendChild(image);
                avatar.classList.add('has-image');
            } else {
                avatar.textContent = avatarInfo.fallbackText;
            }

            const author = document.createElement('span');
            author.className = 'chat-message-author';
            author.textContent = authorLabel;

            const orderBadge = resolveChatPlayerOrderBadge(message);
            let orderBadgeEl = null;
            if (orderBadge) {
                orderBadgeEl = document.createElement('span');
                orderBadgeEl.className = `player-order-badge chat-message-order-badge ${orderBadge.className}`;
                orderBadgeEl.textContent = orderBadge.text;
            }

            const time = document.createElement('span');
            time.className = 'chat-message-time';
            time.textContent = formatChatTimestamp(message.sentAtUtc || Date.now());

            const content = isEmoteChatMessage(message)
                ? createEmoteImageView(message, 'chat-message-emote-img', 'chat-message-emote-fallback')
                : document.createElement('div');
            if (!isEmoteChatMessage(message)) {
                content.className = 'chat-message-text';
                content.textContent = message.content || '';
            }

            meta.appendChild(avatar);
            meta.appendChild(author);
            if (orderBadgeEl) {
                meta.appendChild(orderBadgeEl);
            }
            meta.appendChild(time);
            row.appendChild(meta);
            row.appendChild(content);
            structure.messageList.appendChild(row);
        });
    }

    const shouldScrollToLatest = shouldScrollChatToLatestOnRender || wasNearBottom;
    if (shouldScrollToLatest) {
        structure.messageList.scrollTop = structure.messageList.scrollHeight;
    } else {
        const maxScrollTop = Math.max(0, structure.messageList.scrollHeight - structure.messageList.clientHeight);
        structure.messageList.scrollTop = Math.min(previousScrollTop, maxScrollTop);
    }
    shouldScrollChatToLatestOnRender = false;
    syncTopShadowMask(structure.messageList, chatContent);
}

function normalizeUsedCardHistoryEntry(entry) {
    if (!entry || typeof entry !== 'object') return null;

    const ownerId = String(entry.ownerId ?? entry.OwnerId ?? '').trim() || 'unknown';
    const cardUid = String(entry.cardUid ?? entry.CardUid ?? '').trim();
    const cardName = String(entry.cardName ?? entry.CardName ?? '').trim() || '未知卡牌';
    const cardIdRaw = entry.cardId ?? entry.CardId;
    const cardId = Number.isFinite(Number(cardIdRaw)) ? Number(cardIdRaw) : null;
    const costRaw = entry.cost ?? entry.Cost;
    const cost = Number.isFinite(Number(costRaw)) ? Number(costRaw) : null;
    const artPreview = String(
        entry.artPreview
        ?? entry.ArtPreview
        ?? entry.artDetail
        ?? entry.ArtDetail
        ?? ''
    ).trim();
    const turnNumberRaw = entry.turnNumber ?? entry.TurnNumber;
    const turnNumber = Number.isFinite(Number(turnNumberRaw))
        ? Math.max(0, Math.trunc(Number(turnNumberRaw)))
        : 0;
    const timestampRaw = entry.timestampUtc ?? entry.TimestampUtc ?? entry.timestamp ?? Date.now();
    const parsedTs = typeof timestampRaw === 'number' ? timestampRaw : Date.parse(String(timestampRaw));
    const timestampMs = Number.isFinite(parsedTs) ? parsedTs : Date.now();

    return {
        ownerId,
        cardUid,
        cardId,
        cardName,
        cost,
        artPreview,
        turnNumber,
        timestampMs
    };
}

function normalizeDestroyedCardHistoryEntry(entry) {
    if (!entry || typeof entry !== 'object') return null;

    const ownerId = String(entry.ownerId ?? entry.OwnerId ?? '').trim() || 'unknown';
    const cardUid = String(entry.cardUid ?? entry.CardUid ?? '').trim();
    const cardName = String(entry.cardName ?? entry.CardName ?? '').trim() || '未知卡牌';
    const cardIdRaw = entry.cardId ?? entry.CardId;
    const cardId = Number.isFinite(Number(cardIdRaw)) ? Number(cardIdRaw) : null;
    const costRaw = entry.cost ?? entry.Cost;
    const cost = Number.isFinite(Number(costRaw)) ? Number(costRaw) : null;
    const artPreview = String(
        entry.artPreview
        ?? entry.ArtPreview
        ?? entry.artDetail
        ?? entry.ArtDetail
        ?? ''
    ).trim();
    const destroyReason = String(entry.destroyReason ?? entry.DestroyReason ?? entry.reason ?? '').trim();
    const turnNumberRaw = entry.turnNumber ?? entry.TurnNumber;
    const turnNumber = Number.isFinite(Number(turnNumberRaw))
        ? Math.max(0, Math.trunc(Number(turnNumberRaw)))
        : 0;
    const timestampRaw = entry.timestampUtc ?? entry.TimestampUtc ?? entry.timestamp ?? Date.now();
    const parsedTs = typeof timestampRaw === 'number' ? timestampRaw : Date.parse(String(timestampRaw));
    const timestampMs = Number.isFinite(parsedTs) ? parsedTs : Date.now();

    return {
        ownerId,
        cardUid,
        cardId,
        cardName,
        cost,
        artPreview,
        destroyReason,
        turnNumber,
        timestampMs
    };
}

function getUsedCardHistorySessionStorage() {
    if (typeof window === 'undefined') return null;
    try {
        return window.sessionStorage;
    } catch {
        return null;
    }
}

function getCurrentUsedCardHistoryGameId() {
    return String(gameState.gameId || '').trim();
}

function getUsedCardHistoryStorageKey(gameId) {
    const normalizedGameId = String(gameId || '').trim();
    if (!normalizedGameId) return '';
    return `${USED_CARD_HISTORY_STORAGE_PREFIX}${normalizedGameId}`;
}

function getDestroyedCardHistoryStorageKey(gameId) {
    const normalizedGameId = String(gameId || '').trim();
    if (!normalizedGameId) return '';
    return `${DESTROYED_CARD_HISTORY_STORAGE_PREFIX}${normalizedGameId}`;
}

function persistUsedCardHistoryToSession(gameId = getCurrentUsedCardHistoryGameId()) {
    const storage = getUsedCardHistorySessionStorage();
    if (!storage) return;
    const key = getUsedCardHistoryStorageKey(gameId);
    if (!key) return;

    const payload = {
        version: 1,
        gameId,
        entries: usedCardHistoryEntries
    };

    try {
        storage.setItem(key, JSON.stringify(payload));
    } catch {
        // Ignore storage quota or serialization failures.
    }
}

function restoreUsedCardHistoryFromSession(gameId = getCurrentUsedCardHistoryGameId()) {
    const storage = getUsedCardHistorySessionStorage();
    if (!storage) return [];
    const key = getUsedCardHistoryStorageKey(gameId);
    if (!key) return [];

    let raw = '';
    try {
        raw = storage.getItem(key) || '';
    } catch {
        return [];
    }
    if (!raw) return [];

    try {
        const parsed = JSON.parse(raw);
        const rawEntries = Array.isArray(parsed)
            ? parsed
            : (Array.isArray(parsed?.entries) ? parsed.entries : []);
        if (!Array.isArray(rawEntries) || !rawEntries.length) {
            return [];
        }

        const normalizedEntries = rawEntries
            .map(item => normalizeUsedCardHistoryEntry(item))
            .filter(Boolean);
        if (normalizedEntries.length <= MATCH_USED_CARD_HISTORY_LIMIT) {
            return normalizedEntries;
        }
        return normalizedEntries.slice(normalizedEntries.length - MATCH_USED_CARD_HISTORY_LIMIT);
    } catch {
        return [];
    }
}

function persistDestroyedCardHistoryToSession(gameId = getCurrentUsedCardHistoryGameId()) {
    const storage = getUsedCardHistorySessionStorage();
    if (!storage) return;
    const key = getDestroyedCardHistoryStorageKey(gameId);
    if (!key) return;

    const payload = {
        version: 1,
        gameId,
        entries: destroyedCardHistoryEntries
    };

    try {
        storage.setItem(key, JSON.stringify(payload));
    } catch {
        // Ignore storage quota or serialization failures.
    }
}

function restoreDestroyedCardHistoryFromSession(gameId = getCurrentUsedCardHistoryGameId()) {
    const storage = getUsedCardHistorySessionStorage();
    if (!storage) return [];
    const key = getDestroyedCardHistoryStorageKey(gameId);
    if (!key) return [];

    let raw = '';
    try {
        raw = storage.getItem(key) || '';
    } catch {
        return [];
    }
    if (!raw) return [];

    try {
        const parsed = JSON.parse(raw);
        const rawEntries = Array.isArray(parsed)
            ? parsed
            : (Array.isArray(parsed?.entries) ? parsed.entries : []);
        if (!Array.isArray(rawEntries) || !rawEntries.length) {
            return [];
        }

        const normalizedEntries = rawEntries
            .map(item => normalizeDestroyedCardHistoryEntry(item))
            .filter(Boolean);
        if (normalizedEntries.length <= MATCH_DESTROYED_CARD_HISTORY_LIMIT) {
            return normalizedEntries;
        }
        return normalizedEntries.slice(normalizedEntries.length - MATCH_DESTROYED_CARD_HISTORY_LIMIT);
    } catch {
        return [];
    }
}

function ensureUsedCardHistoryHydratedForCurrentGame() {
    const gameId = getCurrentUsedCardHistoryGameId();
    if (!gameId) return;
    if (hydratedUsedCardHistoryGameId === gameId) return;

    usedCardHistoryEntries = restoreUsedCardHistoryFromSession(gameId);
    hydratedUsedCardHistoryGameId = gameId;
}

function ensureDestroyedCardHistoryHydratedForCurrentGame() {
    const gameId = getCurrentUsedCardHistoryGameId();
    if (!gameId) return;
    if (hydratedDestroyedCardHistoryGameId === gameId) return;

    destroyedCardHistoryEntries = restoreDestroyedCardHistoryFromSession(gameId);
    hydratedDestroyedCardHistoryGameId = gameId;
}

function ensureMatchHistoryHydratedForCurrentGame() {
    ensureUsedCardHistoryHydratedForCurrentGame();
    ensureDestroyedCardHistoryHydratedForCurrentGame();
}

function ensureMatchHistoryPanelStructure(historyContent) {
    if (!historyContent) return null;

    const legacyModeSwitch = historyContent.querySelector('.match-history-mode-switch');
    if (legacyModeSwitch) {
        legacyModeSwitch.remove();
    }

    let sideSwitch = historyContent.querySelector('.match-history-side-switch');
    if (!sideSwitch) {
        sideSwitch = document.createElement('div');
        sideSwitch.className = 'match-history-side-switch';
        sideSwitch.setAttribute('role', 'tablist');
        historyContent.appendChild(sideSwitch);
    }

    let list = historyContent.querySelector('.match-history-list');
    if (!list) {
        list = document.createElement('div');
        list.className = 'match-history-list';
        historyContent.appendChild(list);
    }

    return { sideSwitch, list };
}

function getMatchHistoryPlayerSlots() {
    const players = Array.isArray(gameState.players) ? gameState.players : [];
    const playerIds = players
        .map(player => String(player?.id || '').trim())
        .filter(Boolean);
    const localPlayerId = String(gameState.localPlayerId || '').trim();

    const ordered = [];
    const tryPush = (id) => {
        if (!id || ordered.includes(id)) return;
        ordered.push(id);
    };

    tryPush(localPlayerId);
    playerIds.forEach(id => {
        if (!localPlayerId || id !== localPlayerId) {
            tryPush(id);
        }
    });
    usedCardHistoryEntries.forEach(entry => {
        const id = String(entry?.ownerId || '').trim();
        if (id && id !== 'unknown') {
            tryPush(id);
        }
    });
    destroyedCardHistoryEntries.forEach(entry => {
        const id = String(entry?.ownerId || '').trim();
        if (id && id !== 'unknown') {
            tryPush(id);
        }
    });

    return ordered.slice(0, 2);
}

function getMatchHistoryPlayersBySide(recordMode = MATCH_HISTORY_RECORD_USED) {
    const slots = getMatchHistoryPlayerSlots();
    const localPlayerId = String(gameState.localPlayerId || '').trim();

    let friendlyPlayerId = '';
    let enemyPlayerId = '';

    if (localPlayerId) {
        friendlyPlayerId = localPlayerId;
        enemyPlayerId = slots.find(id => id && id !== localPlayerId) || '';
    } else {
        friendlyPlayerId = slots[0] || '';
        enemyPlayerId = slots[1] || '';
    }

    if (!friendlyPlayerId && enemyPlayerId) {
        friendlyPlayerId = enemyPlayerId;
        enemyPlayerId = '';
    }

    const isDestroyedMode = recordMode === MATCH_HISTORY_RECORD_DESTROYED;
    const friendlyLabel = isDestroyedMode ? '自己被破坏的卡牌' : '自己使用的卡牌';
    const enemyLabel = isDestroyedMode ? '对手被破坏的卡牌' : '对手使用的卡牌';

    return {
        [MATCH_HISTORY_SIDE_FRIENDLY]: {
            side: MATCH_HISTORY_SIDE_FRIENDLY,
            playerId: friendlyPlayerId,
            label: friendlyLabel
        },
        [MATCH_HISTORY_SIDE_ENEMY]: {
            side: MATCH_HISTORY_SIDE_ENEMY,
            playerId: enemyPlayerId,
            label: enemyLabel
        }
    };
}

function resolveActiveMatchHistorySide(playersBySide) {
    if (playersBySide?.[activeMatchHistorySide]?.playerId) {
        return activeMatchHistorySide;
    }
    if (playersBySide?.[MATCH_HISTORY_SIDE_FRIENDLY]?.playerId) {
        return MATCH_HISTORY_SIDE_FRIENDLY;
    }
    if (playersBySide?.[MATCH_HISTORY_SIDE_ENEMY]?.playerId) {
        return MATCH_HISTORY_SIDE_ENEMY;
    }
    return MATCH_HISTORY_SIDE_FRIENDLY;
}

function normalizeMatchHistoryCost(cost) {
    const numericCost = Number(cost);
    if (!Number.isFinite(numericCost)) return null;
    return Math.max(0, Math.trunc(numericCost));
}

function normalizeMatchHistoryCardId(cardId) {
    const numericCardId = Number(cardId);
    if (!Number.isFinite(numericCardId)) return null;
    const normalized = Math.trunc(numericCardId);
    return normalized > 0 ? normalized : null;
}

function normalizeMatchHistoryPreviewSource(source) {
    if (!source) return null;

    const cardName = String(source.cardName ?? source.cardname ?? '').trim() || '未知卡牌';
    const cardId = normalizeMatchHistoryCardId(source.cardId ?? source.cardid);
    const cost = normalizeMatchHistoryCost(source.cost);
    const artPreview = String(source.artPreview ?? source.artpreview ?? '').trim();
    const ownerId = String(source.ownerId ?? source.ownerid ?? '').trim();

    return {
        cardId,
        cardName,
        cost,
        artPreview,
        ownerId
    };
}

function getCardDefinitionForMatchHistory(cardId) {
    if (cardId === null) return null;
    const snapshot = getAbilityDataSnapshot();
    return snapshot?.cardIndex?.get(cardId) ?? null;
}

export async function buildMatchHistoryPreviewCard(source) {
    const normalized = normalizeMatchHistoryPreviewSource(source);
    if (!normalized) return null;

    let definition = getCardDefinitionForMatchHistory(normalized.cardId);
    if (!definition && normalized.cardId !== null) {
        await ensureCardDefinitions();
        definition = getCardDefinitionForMatchHistory(normalized.cardId);
    }

    const resolvedCardId = normalizeMatchHistoryCardId(
        normalized.cardId
        ?? definition?.id
        ?? definition?.CardId
        ?? definition?.Id
    );
    const resolvedCost = normalized.cost
        ?? normalizeMatchHistoryCost(definition?.cost ?? definition?.Cost)
        ?? 0;
    const resolvedAttack = Number(definition?.attack ?? definition?.Attack ?? 0) || 0;
    const resolvedHealth = Number(definition?.health ?? definition?.Health ?? 0) || 0;
    const resolvedCountdown = Number(definition?.countdown ?? definition?.Countdown ?? 0) || 0;
    const resolvedArtPreview = normalized.artPreview
        || String(definition?.artPreview ?? definition?.ArtPreview ?? '').trim();
    const resolvedArtDetail = String(definition?.artDetail ?? definition?.ArtDetail ?? '').trim()
        || resolvedArtPreview;

    return {
        source: 'match-history',
        id: resolvedCardId,
        cardId: resolvedCardId,
        name: normalized.cardName || String(definition?.name ?? definition?.Name ?? '').trim() || '未知卡牌',
        cost: resolvedCost,
        currentCost: resolvedCost,
        attack: resolvedAttack,
        currentAttack: resolvedAttack,
        health: resolvedHealth,
        currentHealth: resolvedHealth,
        countdown: resolvedCountdown > 0 ? resolvedCountdown : null,
        type: definition?.type ?? definition?.Type ?? '',
        rarity: definition?.rarity ?? definition?.Rarity ?? 'Common',
        class: definition?.class ?? definition?.Class ?? 'Neutral',
        description: String(definition?.description ?? definition?.Description ?? '').trim(),
        artPreview: resolvedArtPreview,
        artDetail: resolvedArtDetail,
        keywords: Array.isArray(definition?.keywords)
            ? definition.keywords
            : (Array.isArray(definition?.Keywords) ? definition.Keywords : []),
        tribe: Array.isArray(definition?.tribe)
            ? definition.tribe
            : (Array.isArray(definition?.Tribe) ? definition.Tribe : []),
        effectDescriptions: [],
        ownerId: normalized.ownerId || null,
        zone: 'History'
    };
}

function buildUsedCardSummaryForPlayer(playerId, entries = usedCardHistoryEntries) {
    const normalizedPlayerId = String(playerId || '').trim();
    const perCard = new Map();
    let totalPlays = 0;

    const sourceEntries = Array.isArray(entries) ? entries : [];
    sourceEntries.forEach(entry => {
        if (!entry) return;
        const ownerId = String(entry.ownerId || '').trim();
        if (ownerId !== normalizedPlayerId) return;

        totalPlays += 1;
        const cardKey = entry.cardId !== null
            ? `id:${entry.cardId}`
            : `name:${String(entry.cardName || '').trim().toLowerCase()}`;
        if (!cardKey || cardKey === 'name:') return;

        const existing = perCard.get(cardKey);
        if (!existing) {
            perCard.set(cardKey, {
                ownerId,
                cardName: entry.cardName || '未知卡牌',
                cardId: entry.cardId,
                cost: entry.cost,
                artPreview: entry.artPreview || '',
                count: 1,
                lastTimestampMs: entry.timestampMs,
                lastTurnNumber: entry.turnNumber
            });
            return;
        }

        existing.count += 1;
        if (entry.timestampMs >= existing.lastTimestampMs) {
            existing.lastTimestampMs = entry.timestampMs;
            existing.lastTurnNumber = entry.turnNumber;
            if (entry.artPreview) {
                existing.artPreview = entry.artPreview;
            }
            if (entry.cost !== null && entry.cost !== undefined) {
                existing.cost = entry.cost;
            }
        }
    });

    const cards = Array.from(perCard.values())
        .sort((a, b) => {
            if (b.lastTimestampMs !== a.lastTimestampMs) return b.lastTimestampMs - a.lastTimestampMs;
            if (b.count !== a.count) return b.count - a.count;
            return String(a.cardName || '').localeCompare(String(b.cardName || ''), 'zh-Hans-CN');
        });

    return { totalPlays, cards };
}

function buildUsedCardCostGroups(cards) {
    const groups = new Map();

    cards.forEach(card => {
        const normalizedCost = normalizeMatchHistoryCost(card.cost);
        const groupKey = normalizedCost === null ? 'unknown' : String(normalizedCost);
        let group = groups.get(groupKey);
        if (!group) {
            const label = normalizedCost === null ? 'Cost ?' : `Cost ${normalizedCost}`;
            group = {
                key: groupKey,
                cost: normalizedCost,
                label,
                cards: []
            };
            groups.set(groupKey, group);
        }
        group.cards.push(card);
    });

    const sortedGroups = Array.from(groups.values())
        .sort((a, b) => {
            const aOrder = a.cost === null ? Number.POSITIVE_INFINITY : a.cost;
            const bOrder = b.cost === null ? Number.POSITIVE_INFINITY : b.cost;
            if (aOrder !== bOrder) return aOrder - bOrder;
            return String(a.label || '').localeCompare(String(b.label || ''), 'zh-Hans-CN');
        });

    sortedGroups.forEach(group => {
        group.cards.sort((a, b) => {
            if (b.lastTimestampMs !== a.lastTimestampMs) return b.lastTimestampMs - a.lastTimestampMs;
            if (b.count !== a.count) return b.count - a.count;
            return String(a.cardName || '').localeCompare(String(b.cardName || ''), 'zh-Hans-CN');
        });
    });

    return sortedGroups;
}

function getActiveMatchHistoryEntries() {
    return activeMatchHistoryRecordMode === MATCH_HISTORY_RECORD_DESTROYED
        ? destroyedCardHistoryEntries
        : usedCardHistoryEntries;
}

function getMatchHistoryScrollStateKey(recordMode, side) {
    return `${recordMode}:${side}`;
}

function renderMatchHistoryCardRow(container, card) {
    if (!container || !card) return;

    const row = document.createElement('div');
    row.className = 'match-history-card-row';
    row.setAttribute('role', 'button');
    row.tabIndex = 0;
    if (card.cardId !== null && card.cardId !== undefined) {
        row.dataset.historyCardId = String(card.cardId);
    }
    row.dataset.historyCardName = String(card.cardName || '未知卡牌');
    row.dataset.historyCardCost = card.cost !== null && card.cost !== undefined
        ? String(card.cost)
        : '';
    row.dataset.historyCardArtPreview = String(card.artPreview || '');
    row.dataset.historyOwnerId = String(card.ownerId || '');
    row.title = '点击查看卡牌预览';
    row.addEventListener('keydown', event => {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        event.preventDefault();
        row.click();
    });

    const avatar = document.createElement('div');
    avatar.className = 'match-history-card-avatar';
    const artUrl = resolveArtUrl(card.artPreview || '');
    if (artUrl) {
        avatar.style.backgroundImage = `url('${artUrl}')`;
        avatar.classList.add('has-art');
    } else {
        avatar.textContent = String(card.cardName || '卡').trim().slice(0, 1) || '卡';
    }

    const main = document.createElement('div');
    main.className = 'match-history-card-main';

    const name = document.createElement('div');
    name.className = 'match-history-card-name';
    name.textContent = card.cardName || '未知卡牌';
    main.appendChild(name);

    const count = document.createElement('div');
    count.className = 'match-history-card-count';
    count.textContent = `×${card.count}`;

    row.appendChild(avatar);
    row.appendChild(main);
    row.appendChild(count);
    container.appendChild(row);
}

function renderMatchHistoryCostGroup(listRoot, group) {
    if (!listRoot || !group) return;

    const section = document.createElement('section');
    section.className = 'match-history-cost-group';

    const header = document.createElement('header');
    header.className = 'match-history-cost-header';
    header.textContent = group.label;
    section.appendChild(header);

    const body = document.createElement('div');
    body.className = 'match-history-cost-body';
    group.cards.forEach(card => {
        renderMatchHistoryCardRow(body, card);
    });

    section.appendChild(body);
    listRoot.appendChild(section);
}

function renderActiveUsedCardPanel(historyContent) {
    const structure = ensureMatchHistoryPanelStructure(historyContent);
    if (!structure) return;
    bindTopShadowMask(structure.list, historyContent);

    if (!isActiveHistoryTab()) {
        structure.sideSwitch.innerHTML = '';
        structure.list.innerHTML = '';
        structure.list.scrollTop = 0;
        delete structure.list.dataset.activeSide;
        delete structure.list.dataset.activeMode;
        syncTopShadowMask(structure.list, historyContent);
        return;
    }

    ensureMatchHistoryHydratedForCurrentGame();

    const previousSide = String(structure.list.dataset.activeSide || '');
    const previousMode = String(structure.list.dataset.activeMode || activeMatchHistoryRecordMode || MATCH_HISTORY_RECORD_USED);
    if (previousSide) {
        const previousViewKey = getMatchHistoryScrollStateKey(previousMode, previousSide);
        historyScrollTopByViewState.set(previousViewKey, structure.list.scrollTop);
    }

    activeMatchHistoryRecordMode = isActiveDestroyedHistoryTab()
        ? MATCH_HISTORY_RECORD_DESTROYED
        : MATCH_HISTORY_RECORD_USED;
    const activeEntries = getActiveMatchHistoryEntries();
    const playersBySide = getMatchHistoryPlayersBySide(activeMatchHistoryRecordMode);
    const previousPlayerBySide = getMatchHistoryPlayersBySide(previousMode);
    if (previousSide && previousMode !== activeMatchHistoryRecordMode) {
        const previousPlayerId = String(previousPlayerBySide?.[previousSide]?.playerId || '').trim();
        const mirroredSide = previousSide === MATCH_HISTORY_SIDE_FRIENDLY ? MATCH_HISTORY_SIDE_ENEMY : MATCH_HISTORY_SIDE_FRIENDLY;
        const candidateSides = [previousSide, mirroredSide];
        const matchedSide = candidateSides.find(side => {
            const candidatePlayerId = String(playersBySide?.[side]?.playerId || '').trim();
            return Boolean(previousPlayerId && candidatePlayerId && candidatePlayerId === previousPlayerId);
        });
        if (matchedSide) {
            activeMatchHistorySide = matchedSide;
        }
    }

    activeMatchHistorySide = resolveActiveMatchHistorySide(playersBySide);

    const sideConfigs = [
        playersBySide[MATCH_HISTORY_SIDE_FRIENDLY],
        playersBySide[MATCH_HISTORY_SIDE_ENEMY]
    ];
    const summaryBySide = new Map();
    sideConfigs.forEach(config => {
        summaryBySide.set(config.side, buildUsedCardSummaryForPlayer(config.playerId, activeEntries));
    });

    structure.sideSwitch.innerHTML = '';
    sideConfigs.forEach(config => {
        const summary = summaryBySide.get(config.side) || { totalPlays: 0, cards: [] };
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'match-history-side-btn';
        button.setAttribute('role', 'tab');
        button.setAttribute('aria-selected', config.side === activeMatchHistorySide ? 'true' : 'false');
        if (config.side === activeMatchHistorySide) {
            button.classList.add('is-active');
        }
        if (!config.playerId && summary.totalPlays === 0) {
            button.disabled = true;
        }

        const text = document.createElement('span');
        text.className = 'match-history-side-btn-text';
        text.textContent = config.label;
        button.appendChild(text);

        const count = document.createElement('span');
        count.className = 'match-history-side-btn-count';
        count.textContent = `${summary.totalPlays}`;
        button.appendChild(count);

        button.addEventListener('click', () => {
            if (activeMatchHistorySide === config.side) return;
            const currentKey = getMatchHistoryScrollStateKey(activeMatchHistoryRecordMode, activeMatchHistorySide);
            historyScrollTopByViewState.set(currentKey, structure.list.scrollTop);
            activeMatchHistorySide = config.side;
            renderGameLog(getLatestGameLogs());
        });
        structure.sideSwitch.appendChild(button);
    });

    structure.list.innerHTML = '';
    const activeSummary = summaryBySide.get(activeMatchHistorySide) || { totalPlays: 0, cards: [] };
    const activeGroups = buildUsedCardCostGroups(activeSummary.cards || []);

    if (activeGroups.length) {
        activeGroups.forEach(group => {
            renderMatchHistoryCostGroup(structure.list, group);
        });
    }

    let nextScrollTop = 0;
    if (shouldScrollHistoryToLatestOnRender) {
        nextScrollTop = 0;
    } else {
        const viewKey = getMatchHistoryScrollStateKey(activeMatchHistoryRecordMode, activeMatchHistorySide);
        const storedScrollTop = historyScrollTopByViewState.get(viewKey);
        nextScrollTop = Number.isFinite(storedScrollTop) ? storedScrollTop : 0;
    }
    const maxScrollTop = Math.max(0, structure.list.scrollHeight - structure.list.clientHeight);
    structure.list.scrollTop = Math.min(nextScrollTop, maxScrollTop);
    shouldScrollHistoryToLatestOnRender = false;
    const activeViewKey = getMatchHistoryScrollStateKey(activeMatchHistoryRecordMode, activeMatchHistorySide);
    historyScrollTopByViewState.set(activeViewKey, structure.list.scrollTop);
    structure.list.dataset.activeSide = activeMatchHistorySide;
    structure.list.dataset.activeMode = activeMatchHistoryRecordMode;
    syncTopShadowMask(structure.list, historyContent);
}

function syncLogPanelView(logContent, chatContent, historyContent) {
    if (!logContent || !chatContent || !historyContent) return;
    const showChat = isActiveChatTab();
    const showHistory = isActiveHistoryTab();
    logContent.classList.toggle('is-active', !showChat && !showHistory);
    chatContent.classList.toggle('is-active', showChat);
    historyContent.classList.toggle('is-active', showHistory);
}

function normalizeInMatchChatMessage(message) {
    if (!message || typeof message !== 'object') return null;
    const messageTypeRaw = String(message.messageType || '').trim().toLowerCase();
    const messageType = messageTypeRaw === 'emote' ? 'emote' : 'text';
    const emoteId = String(message.emoteId || '').trim();
    const emoteName = String(message.emoteName || '').trim();
    const emoteUrl = String(message.emoteUrl || '').trim();
    const content = messageType === 'emote'
        ? (String(message.content || '').trim() || emoteName || emoteId)
        : String(message.content || '').trim();
    if (messageType === 'text' && !content) return null;
    if (messageType === 'emote' && (!emoteId || !emoteUrl)) return null;

    const senderTypeRaw = String(message.senderType || '').trim().toLowerCase();
    const senderType = senderTypeRaw === 'spectator'
        ? 'spectator'
        : senderTypeRaw === 'system'
            ? 'system'
            : 'player';

    return {
        messageId: String(message.messageId || '').trim(),
        senderType,
        senderId: String(message.senderId || '').trim(),
        senderName: String(message.senderName || '').trim(),
        senderAvatarUrl: String(message.senderAvatarUrl || message.SenderAvatarUrl || '').trim(),
        messageType,
        content,
        emoteId: messageType === 'emote' ? emoteId : '',
        emoteName: messageType === 'emote' ? (emoteName || content) : '',
        emoteUrl: messageType === 'emote' ? emoteUrl : '',
        sentAtUtc: String(message.sentAtUtc || '').trim(),
        clientMessageId: String(message.clientMessageId || '').trim(),
        isSelf: Boolean(message.isSelf)
    };
}

function pushRoomChatMessage(message) {
    const normalized = normalizeInMatchChatMessage(message);
    if (!normalized) return false;

    if (normalized.messageId && roomChatMessageIdSet.has(normalized.messageId)) {
        return false;
    }

    roomChatMessages.push(normalized);
    if (normalized.messageId) {
        roomChatMessageIdSet.add(normalized.messageId);
    }
    if (roomChatMessages.length > LOG_PANEL_CHAT_HISTORY_LIMIT) {
        const overflow = roomChatMessages.length - LOG_PANEL_CHAT_HISTORY_LIMIT;
        const removed = roomChatMessages.splice(0, overflow);
        removed.forEach(item => {
            if (item?.messageId) {
                roomChatMessageIdSet.delete(item.messageId);
            }
        });
    }

    return true;
}

function ensureChatBarrageRoot() {
    let root = document.getElementById('chat-barrage-root');
    if (root) return root;

    root = document.createElement('div');
    root.id = 'chat-barrage-root';
    root.className = 'chat-barrage-root';
    const host = getBarrageHostElement();
    host.appendChild(root);
    return root;
}

function clearChatBarrageUi() {
    chatBarrageQueue.splice(0, chatBarrageQueue.length);
    chatBarrageActiveLanes.clear();
    const root = document.getElementById('chat-barrage-root');
    if (root) {
        root.remove();
    }
}

function enqueueChatBarrage(message) {
    if (!message || !isGamePageActive()) return;
    if (chatBarrageQueue.length >= CHAT_BARRAGE_MAX_QUEUE) {
        chatBarrageQueue.shift();
    }
    chatBarrageQueue.push(message);
    dispatchChatBarrages();
}

function pickChatBarrageLane() {
    const freeLanes = CHAT_BARRAGE_LANE_TOPS
        .map((_, index) => index)
        .filter(index => !chatBarrageActiveLanes.has(index));
    const lanePool = freeLanes.length > 0
        ? freeLanes
        : CHAT_BARRAGE_LANE_TOPS.map((_, index) => index);
    return lanePool[Math.floor(Math.random() * lanePool.length)];
}

function dispatchChatBarrages() {
    if (!isGamePageActive()) {
        clearChatBarrageUi();
        return;
    }

    while (
        chatBarrageQueue.length > 0
        && chatBarrageActiveLanes.size < CHAT_BARRAGE_LANE_TOPS.length
    ) {
        const message = chatBarrageQueue.shift();
        const laneIndex = pickChatBarrageLane();
        chatBarrageActiveLanes.add(laneIndex);
        void playSingleChatBarrage(message, laneIndex).finally(() => {
            chatBarrageActiveLanes.delete(laneIndex);
            if (!isGamePageActive()) {
                clearChatBarrageUi();
                return;
            }
            dispatchChatBarrages();
        });
    }
}

function playSingleChatBarrage(message, laneIndex = 0) {
    return new Promise(resolve => {
        if (!message || !isGamePageActive()) {
            resolve();
            return;
        }

        const root = ensureChatBarrageRoot();
        const item = document.createElement('div');
        item.className = 'chat-barrage-item';
        const author = resolveChatAuthorLabel(message);
        const avatarInfo = resolveChatAvatarInfo(message, author);

        const avatar = document.createElement('span');
        avatar.className = 'chat-barrage-avatar';
        if (avatarInfo.imageSrc) {
            const image = document.createElement('img');
            image.className = 'chat-barrage-avatar-img';
            image.src = avatarInfo.imageSrc;
            image.alt = `${author} avatar`;
            image.onerror = () => {
                avatar.classList.remove('has-image');
                avatar.textContent = avatarInfo.fallbackText;
            };
            avatar.appendChild(image);
            avatar.classList.add('has-image');
        } else {
            avatar.textContent = avatarInfo.fallbackText;
        }

        const content = isEmoteChatMessage(message)
            ? createEmoteImageView(message, 'chat-barrage-emote-img', 'chat-barrage-emote-fallback')
            : document.createElement('span');
        if (!isEmoteChatMessage(message)) {
            content.className = 'chat-barrage-text';
            content.textContent = message.content || '';
        }

        item.appendChild(avatar);
        item.appendChild(content);
        const laneTop = CHAT_BARRAGE_LANE_TOPS[Math.max(0, Math.min(laneIndex, CHAT_BARRAGE_LANE_TOPS.length - 1))] ?? 0;
        item.style.top = `${laneTop}px`;
        root.appendChild(item);

        const rootWidth = Math.max(root.clientWidth, window.innerWidth - 32);
        const itemWidth = Math.max(item.offsetWidth, 120);
        const startX = rootWidth + 16;
        const endX = -(itemWidth + 16);
        const distance = Math.max(1, Math.abs(endX - startX));
        const durationMs = Math.min(7000, Math.max(2800, Math.round((distance / 180) * 1000)));

        item.style.transform = `translateX(${startX}px)`;
        item.style.opacity = '0.96';

        const cleanup = () => {
            if (item.isConnected) {
                item.remove();
            }
            if (root.childElementCount === 0) {
                root.remove();
            }
            resolve();
        };

        const timeoutId = window.setTimeout(cleanup, durationMs + 420);
        item.addEventListener('transitionend', event => {
            if (event.propertyName !== 'transform') return;
            window.clearTimeout(timeoutId);
            cleanup();
        }, { once: true });

        requestAnimationFrame(() => {
            item.style.transition = `transform ${durationMs}ms linear, opacity 180ms ease`;
            item.style.transform = `translateX(${endX}px)`;
        });
    });
}

export function replaceInMatchChatHistory(messages) {
    roomChatMessages = [];
    roomChatMessageIdSet.clear();
    const list = Array.isArray(messages) ? messages : [];
    list.forEach(message => {
        pushRoomChatMessage(message);
    });

    if (isActiveChatTab()) {
        renderGameLog(getLatestGameLogs());
    }
}

export function appendInMatchChatMessage(message, playBarrage = false) {
    const appended = pushRoomChatMessage(message);
    if (!appended) return;

    if (isActiveChatTab()) {
        renderGameLog(getLatestGameLogs());
    }

    if (playBarrage) {
        enqueueChatBarrage(message);
    }
}

export function appendInMatchUsedCard(entry) {
    ensureUsedCardHistoryHydratedForCurrentGame();

    const normalized = normalizeUsedCardHistoryEntry(entry);
    if (!normalized) return false;

    usedCardHistoryEntries.push(normalized);
    if (usedCardHistoryEntries.length > MATCH_USED_CARD_HISTORY_LIMIT) {
        const overflow = usedCardHistoryEntries.length - MATCH_USED_CARD_HISTORY_LIMIT;
        usedCardHistoryEntries.splice(0, overflow);
    }
    persistUsedCardHistoryToSession();

    if (isActiveHistoryTab()) {
        renderGameLog(getLatestGameLogs());
    }
    return true;
}

export function appendInMatchDestroyedCard(entry) {
    ensureDestroyedCardHistoryHydratedForCurrentGame();

    const normalized = normalizeDestroyedCardHistoryEntry(entry);
    if (!normalized) return false;

    destroyedCardHistoryEntries.push(normalized);
    if (destroyedCardHistoryEntries.length > MATCH_DESTROYED_CARD_HISTORY_LIMIT) {
        const overflow = destroyedCardHistoryEntries.length - MATCH_DESTROYED_CARD_HISTORY_LIMIT;
        destroyedCardHistoryEntries.splice(0, overflow);
    }
    persistDestroyedCardHistoryToSession();

    if (isActiveHistoryTab()) {
        renderGameLog(getLatestGameLogs());
    }
    return true;
}

export function replaceInMatchCardHistory(payload) {
    const usedSource = Array.isArray(payload?.usedCards)
        ? payload.usedCards
        : (Array.isArray(payload?.UsedCards) ? payload.UsedCards : []);
    const destroyedSource = Array.isArray(payload?.destroyedCards)
        ? payload.destroyedCards
        : (Array.isArray(payload?.DestroyedCards) ? payload.DestroyedCards : []);

    const normalizedUsed = usedSource
        .map(item => normalizeUsedCardHistoryEntry(item))
        .filter(Boolean);
    const normalizedDestroyed = destroyedSource
        .map(item => normalizeDestroyedCardHistoryEntry(item))
        .filter(Boolean);

    usedCardHistoryEntries = normalizedUsed.length > MATCH_USED_CARD_HISTORY_LIMIT
        ? normalizedUsed.slice(normalizedUsed.length - MATCH_USED_CARD_HISTORY_LIMIT)
        : normalizedUsed;
    destroyedCardHistoryEntries = normalizedDestroyed.length > MATCH_DESTROYED_CARD_HISTORY_LIMIT
        ? normalizedDestroyed.slice(normalizedDestroyed.length - MATCH_DESTROYED_CARD_HISTORY_LIMIT)
        : normalizedDestroyed;

    const payloadGameId = String(payload?.gameId ?? payload?.GameId ?? '').trim();
    const currentGameId = getCurrentUsedCardHistoryGameId();
    const targetGameId = payloadGameId || currentGameId;
    if (targetGameId) {
        hydratedUsedCardHistoryGameId = targetGameId;
        hydratedDestroyedCardHistoryGameId = targetGameId;
        persistUsedCardHistoryToSession(targetGameId);
        persistDestroyedCardHistoryToSession(targetGameId);
    } else {
        hydratedUsedCardHistoryGameId = '';
        hydratedDestroyedCardHistoryGameId = '';
    }

    if (isActiveHistoryTab()) {
        renderGameLog(getLatestGameLogs());
    }
}

export function clearInMatchUsedCardUiState(options = {}) {
    const gameId = getCurrentUsedCardHistoryGameId();
    const shouldClearSession = options?.clearSession === true;

    usedCardHistoryEntries = [];
    destroyedCardHistoryEntries = [];
    shouldScrollHistoryToLatestOnRender = false;
    activeMatchHistorySide = MATCH_HISTORY_SIDE_FRIENDLY;
    activeMatchHistoryRecordMode = MATCH_HISTORY_RECORD_USED;
    historyScrollTopByViewState.clear();
    hydratedUsedCardHistoryGameId = '';
    hydratedDestroyedCardHistoryGameId = '';

    if (shouldClearSession) {
        const storage = getUsedCardHistorySessionStorage();
        const usedKey = getUsedCardHistoryStorageKey(gameId);
        const destroyedKey = getDestroyedCardHistoryStorageKey(gameId);
        if (storage && usedKey) {
            try {
                storage.removeItem(usedKey);
            } catch {
                // Ignore storage failures.
            }
        }
        if (storage && destroyedKey) {
            try {
                storage.removeItem(destroyedKey);
            } catch {
                // Ignore storage failures.
            }
        }
    }

    const historyContainer = document.getElementById('game-match-history-content');
    if (historyContainer) {
        historyContainer.innerHTML = '';
    }
}

export function clearInMatchChatUiState() {
    roomChatMessages = [];
    roomChatMessageIdSet.clear();
    spectatorAliasBySenderKey.clear();
    logPanelActiveView = LOG_PANEL_VIEW_LOG;
    shouldScrollLogToLatestOnRender = false;
    shouldScrollChatToLatestOnRender = false;
    shouldScrollHistoryToLatestOnRender = false;
    activeMatchHistorySide = MATCH_HISTORY_SIDE_FRIENDLY;
    activeMatchHistoryRecordMode = MATCH_HISTORY_RECORD_USED;
    historyScrollTopByViewState.clear();
    logPanelChatDraft = '';
    clearChatSendStatus();
    isChatSending = false;
    closeEmotePanel();
    clearChatBarrageUi();
    const chatContainer = document.getElementById('game-chat-content');
    if (chatContainer) {
        chatContainer.innerHTML = '';
    }
}

function syncTopShadowMask(scrollContainer, maskTarget = scrollContainer) {
    if (!scrollContainer || !maskTarget) return;
    const hasOverflow = (scrollContainer.scrollHeight - scrollContainer.clientHeight) > 1;
    const canScrollUp = hasOverflow && scrollContainer.scrollTop > 0;
    maskTarget.classList.toggle('has-scroll-up', canScrollUp);
}

function bindTopShadowMask(scrollContainer, maskTarget = scrollContainer) {
    if (!scrollContainer || !maskTarget) return;
    if (scrollContainer.dataset.topMaskBound === '1') {
        syncTopShadowMask(scrollContainer, maskTarget);
        return;
    }

    scrollContainer.dataset.topMaskBound = '1';
    scrollContainer.addEventListener('scroll', () => {
        syncTopShadowMask(scrollContainer, maskTarget);
    }, { passive: true });
    syncTopShadowMask(scrollContainer, maskTarget);
}

function isLogNearBottom(container) {
    if (!container) return true;
    const remaining = container.scrollHeight - container.scrollTop - container.clientHeight;
    return remaining < LOG_AUTO_SCROLL_THRESHOLD_PX;
}

function parseLogTokens(text) {
    const segments = [];
    const pattern = /\[\[(.+?)\]\]/g;
    let lastIndex = 0;
    let match = pattern.exec(text);

    while (match) {
        if (match.index > lastIndex) {
            segments.push({ type: 'text', value: text.slice(lastIndex, match.index) });
        }
        segments.push({ type: 'token', value: match[1] });
        lastIndex = pattern.lastIndex;
        match = pattern.exec(text);
    }

    if (lastIndex < text.length) {
        segments.push({ type: 'text', value: text.slice(lastIndex) });
    }

    return segments;
}

function getPlayerLabel(playerId) {
    if (!playerId || playerId === 'unknown') return '玩家';
    if (gameState.localPlayerId) {
        return playerId === gameState.localPlayerId ? '我方' : '敌方';
    }

    const players = Array.isArray(gameState.players) ? gameState.players : [];
    const index = players.findIndex(p => p && p.id === playerId);
    if (index === 0) return '玩家1';
    if (index === 1) return '玩家2';
    return '玩家';
}

function getSideClass(ownerId) {
    if (!gameState.localPlayerId || !ownerId || ownerId === 'unknown') return '';
    return ownerId === gameState.localPlayerId ? 'log-token-friendly' : 'log-token-enemy';
}

function findCardInstanceForLog(uid) {
    if (!uid) return null;
    const primary = findCardInstanceByUid(uid);
    if (primary) return primary;

    const players = Array.isArray(gameState.players) ? gameState.players : [];
    const zones = ['graveyard', 'deck', 'extra', 'emblem'];
    for (const player of players) {
        if (!player) continue;
        for (const zone of zones) {
            const list = player[zone];
            if (!Array.isArray(list)) continue;
            const match = list.find(card => card && (card.uid === uid || card.InstanceId === uid || card.instanceId === uid));
            if (match) return match;
        }
    }
    return null;
}

function getCardUidForLog(card) {
    if (!card || typeof card !== 'object') return '';
    return String(card.uid || card.InstanceId || card.instanceId || '').trim();
}

function normalizeLogName(value) {
    return String(value || '')
        .replace(/\[\[|\]\]/g, '')
        .replace(/\s+/g, ' ')
        .trim();
}

function formatCardTokenText(value) {
    return normalizeLogName(value);
}

function isWhitespaceChar(value) {
    return /\s/.test(value);
}

function isPunctuationChar(value) {
    return /[，。！？、：；…）)]/.test(value);
}

function shouldInsertLeadingSpace(prevChar) {
    if (!prevChar) return false;
    if (isWhitespaceChar(prevChar)) return false;
    return true;
}

function shouldInsertTrailingSpace(nextChar, nextIsToken) {
    if (nextIsToken) return true;
    if (!nextChar) return false;
    if (isWhitespaceChar(nextChar)) return false;
    if (isPunctuationChar(nextChar)) return false;
    return true;
}

function parseCardToken(rawToken) {
    const text = String(rawToken || '').trim();
    if (!text) return { id: '', name: '', ownerId: '' };

    const parts = text.split('|').map(part => part.trim()).filter(Boolean);
    const id = parts.shift() || '';
    let name = '';
    let ownerId = '';

    for (const part of parts) {
        const idx = part.indexOf(':');
        if (idx > 0) {
            const key = part.slice(0, idx).trim().toLowerCase();
            const value = part.slice(idx + 1).trim();
            if (key === 'name') {
                name = value;
                continue;
            }
            if (key === 'owner') {
                ownerId = value;
                continue;
            }
        }
        if (!name) {
            name = part;
        }
    }

    return { id, name, ownerId };
}

function shouldRevealLogCard(fallbackName) {
    return Boolean(fallbackName);
}

function resolveCardToken(rawToken) {
    const { id, name, ownerId } = parseCardToken(rawToken);
    const card = findCardInstanceForLog(id);
    if (!card) {
        const fallbackName = normalizeLogName(name);
        if (shouldRevealLogCard(fallbackName)) {
            const sideClass = getSideClass(ownerId);
            return {
                text: formatCardTokenText(fallbackName),
                className: `log-token log-token-card ${sideClass}`.trim(),
                tokenType: 'card',
                cardUid: id,
                isClickable: Boolean(id)
            };
        }
        return {
            text: formatCardTokenText('未知卡牌'),
            className: 'log-token log-token-card is-unknown',
            tokenType: 'card',
            cardUid: id,
            isClickable: Boolean(id)
        };
    }

    const sideClass = getSideClass(card.ownerId);
    const cardUid = getCardUidForLog(card) || id;
    if (card.type === 'Leader') {
        const side = getPlayerLabel(card.ownerId);
        return {
            text: formatCardTokenText(`${side}主战者`),
            className: `log-token log-token-card log-token-leader ${sideClass}`.trim(),
            tokenType: 'card',
            cardUid,
            isClickable: Boolean(cardUid)
        };
    }

    const snapshotName = normalizeLogName(name);
    const resolvedName = snapshotName || normalizeLogName(card.name || card.Name || '未知卡牌');
    return {
        text: formatCardTokenText(resolvedName),
        className: `log-token log-token-card ${sideClass}`.trim(),
        tokenType: 'card',
        cardUid,
        isClickable: Boolean(cardUid)
    };
}

function resolveLogToken(rawToken) {
    const token = String(rawToken || '').trim();
    if (token.startsWith('card:')) {
        return resolveCardToken(token.slice(5));
    }
    if (token.startsWith('cardname:')) {
        const name = normalizeLogName(token.slice(9)) || '未知卡牌';
        return { text: formatCardTokenText(name), className: 'log-token log-token-card' };
    }
    if (token.startsWith('player:')) {
        const playerId = token.slice(7).trim();
        const label = getPlayerLabel(playerId);
        return { text: label, className: `log-token log-token-player ${getSideClass(playerId)}`.trim() };
    }
    if (token.startsWith('num:')) {
        const value = token.slice(4).trim();
        return { text: value, className: 'log-token log-token-number' };
    }
    if (token.startsWith('text:')) {
        const value = token.slice(5).trim();
        return { text: value, className: 'log-token' };
    }

    return { text: token, className: 'log-token' };
}

export function renderGameLog(logs) {
    let logContainer = document.getElementById('game-log-container');
    let logContent = document.getElementById('game-log-content');
    let chatContent = document.getElementById('game-chat-content');
    let historyContent = document.getElementById('game-match-history-content');

    // Create container structure if it doesn't exist
    if (!logContainer) {
        logContainer = document.createElement('div');
        logContainer.id = 'game-log-container';
        logContainer.className = isLogExpanded ? 'expanded' : 'collapsed'; // Set initial state

        // Create Header Container
        const header = document.createElement('div');
        header.className = 'log-header';

        // Create Log Toggle Button (keep existing style/behavior semantics)
        const toggleBtn = document.createElement('button');
        toggleBtn.id = 'game-log-toggle';
        toggleBtn.className = 'log-toggle-btn';
        toggleBtn.title = 'Toggle Log';
        toggleBtn.setAttribute('aria-label', '战斗日志');
        toggleBtn.innerHTML = `
            <svg viewBox="0 0 24 24" class="icon-svg" aria-hidden="true">
                <path d="M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/>
            </svg>
        `;

        // Create left title group
        const headerMarker = document.createElement('span');
        headerMarker.className = 'log-header-marker';
        headerMarker.setAttribute('aria-hidden', 'true');

        const titleGroup = document.createElement('div');
        titleGroup.className = 'log-title-group';

        const titleMain = document.createElement('span');
        titleMain.className = 'log-title-main';
        titleMain.textContent = '战斗记录';

        const titleSub = document.createElement('span');
        titleSub.className = 'log-title-sub';
        titleSub.textContent = 'BATTLE TIMELINE';

        titleGroup.appendChild(titleMain);
        titleGroup.appendChild(titleSub);

        header.appendChild(headerMarker);
        header.appendChild(titleGroup);

        // Create Chat Toggle Button (new, below original game-log-toggle)
        const chatToggleBtn = document.createElement('button');
        chatToggleBtn.id = 'game-chat-toggle';
        chatToggleBtn.className = 'log-chat-toggle-btn';
        chatToggleBtn.title = '聊天日志';
        chatToggleBtn.setAttribute('aria-label', '聊天日志');
        chatToggleBtn.innerHTML = `
            <svg viewBox="0 0 24 24" class="icon-svg" aria-hidden="true">
                <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
            </svg>
        `;

        const historyToggleBtn = document.createElement('button');
        historyToggleBtn.id = 'game-history-toggle';
        historyToggleBtn.className = 'log-history-toggle-btn';
        historyToggleBtn.title = '使用记录';
        historyToggleBtn.setAttribute('aria-label', '使用记录');
        historyToggleBtn.textContent = '≡';

        const destroyedHistoryToggleBtn = document.createElement('button');
        destroyedHistoryToggleBtn.id = 'game-destroy-history-toggle';
        destroyedHistoryToggleBtn.className = 'log-destroy-history-toggle-btn';
        destroyedHistoryToggleBtn.title = '破坏记录';
        destroyedHistoryToggleBtn.setAttribute('aria-label', '破坏记录');
        destroyedHistoryToggleBtn.textContent = '✝';

        header.appendChild(destroyedHistoryToggleBtn);
        header.appendChild(historyToggleBtn);
        header.appendChild(chatToggleBtn);
        header.appendChild(toggleBtn);

        const panelContent = document.createElement('div');
        panelContent.id = 'game-log-panel-content';
        panelContent.className = 'log-panel-content';

        logContent = document.createElement('div');
        logContent.id = 'game-log-content';
        logContent.className = 'log-panel-view is-active';

        chatContent = document.createElement('div');
        chatContent.id = 'game-chat-content';
        chatContent.className = 'log-panel-view';

        historyContent = document.createElement('div');
        historyContent.id = 'game-match-history-content';
        historyContent.className = 'log-panel-view';

        logContainer.appendChild(header);
        panelContent.appendChild(logContent);
        panelContent.appendChild(chatContent);
        panelContent.appendChild(historyContent);
        logContainer.appendChild(panelContent);

        // Append to game-container
        const gameContainer = document.getElementById('game-container');
        if (gameContainer) {
            gameContainer.appendChild(logContainer);
        } else {
            document.body.appendChild(logContainer); // Fallback
        }
    } else {
        // Ensure content element exists if container was found but might be malformed (legacy support)
        const header = document.querySelector('#game-log-container .log-header');
        const logToggle = document.getElementById('game-log-toggle');
        const chatToggle = document.getElementById('game-chat-toggle');
        const historyToggle = document.getElementById('game-history-toggle');
        const destroyedHistoryToggle = document.getElementById('game-destroy-history-toggle');
        logContent = document.getElementById('game-log-content');
        chatContent = document.getElementById('game-chat-content');
        historyContent = document.getElementById('game-match-history-content');
        if (!header || !logToggle || !chatToggle || !historyToggle || !destroyedHistoryToggle || !logContent || !chatContent || !historyContent) {
            logContainer.remove(); // Reset malformed legacy structure
            renderGameLog(logs);
            return;
        }
        const legacyToggleStack = document.getElementById('game-log-toggle-stack');
        if (legacyToggleStack) {
            logContainer.remove(); // Reset legacy stacked toggle structure
            renderGameLog(logs);
            return;
        }
        // Sync class with state
        logContainer.className = isLogExpanded ? 'expanded' : 'collapsed';
    }

    bindLogPanelToggleHandlers(logContainer, logs);
    syncLogPanelChatToggleState(logContainer);
    syncLogPanelView(logContent, chatContent, historyContent);
    bindTopShadowMask(logContent, logContent);
    const titleMain = logContainer.querySelector('.log-title-main');
    const titleSub = logContainer.querySelector('.log-title-sub');
    if (titleMain) {
        if (isActiveUsedHistoryTab()) {
            titleMain.textContent = '对战历史';
        } else if (isActiveDestroyedHistoryTab()) {
            titleMain.textContent = '破坏记录';
        } else if (isActiveChatTab()) {
            titleMain.textContent = '聊天记录';
        } else {
            titleMain.textContent = '战斗记录';
        }
    }
    if (titleSub) {
        if (isActiveUsedHistoryTab()) {
            titleSub.textContent = 'USED CARDS';
        } else if (isActiveDestroyedHistoryTab()) {
            titleSub.textContent = 'DESTROYED CARDS';
        } else if (isActiveChatTab()) {
            titleSub.textContent = 'MATCH CHAT';
        } else {
            titleSub.textContent = 'BATTLE TIMELINE';
        }
    }
    if (isActiveChatTab()) {
        renderActiveChatPanel(chatContent);
    }
    if (isActiveHistoryTab()) {
        renderActiveUsedCardPanel(historyContent);
    }

    const previousScrollTop = logContent?.scrollTop ?? 0;
    const wasNearBottom = isLogExpanded ? isLogNearBottom(logContent) : true;

    // Update Logs Content
    if (!logs || !Array.isArray(logs)) {
        syncTopShadowMask(logContent, logContent);
        return;
    }

    // Clear existing logs in content wrapper
    logContent.innerHTML = '';

    // Render recent logs with a safe cap
    const recentLogs = logs.slice(-LOG_RENDER_LIMIT);

    recentLogs.forEach(log => {
        const logEntry = document.createElement('div');
        logEntry.className = 'log-entry';
        const rawText = String(log ?? '');
        const segments = parseLogTokens(rawText);
        let lastChar = '';
        segments.forEach((segment, index) => {
            if (segment.type === 'text') {
                if (segment.value) {
                    logEntry.appendChild(document.createTextNode(segment.value));
                    lastChar = segment.value.slice(-1);
                }
                return;
            }

            const resolved = resolveLogToken(segment.value);
            const isSpacedToken = resolved.className.includes('log-token-card')
                || resolved.className.includes('log-token-player')
                || resolved.className.includes('log-token-number');
            const nextSegment = segments[index + 1] || null;
            const nextIsToken = nextSegment?.type === 'token';
            const nextChar = nextSegment?.type === 'text' && nextSegment.value
                ? nextSegment.value[0]
                : '';
            if (isSpacedToken && shouldInsertLeadingSpace(lastChar)) {
                logEntry.appendChild(document.createTextNode(' '));
                lastChar = ' ';
            }
            const span = document.createElement('span');
            span.className = resolved.className;
            span.textContent = resolved.text;
            if (resolved.tokenType === 'card' && resolved.isClickable && resolved.cardUid) {
                span.dataset.logCardUid = resolved.cardUid;
                span.classList.add('log-token-clickable');
                span.title = '点击查看卡牌详情';
            }
            logEntry.appendChild(span);
            if (resolved.text) {
                lastChar = resolved.text.slice(-1);
            }
            if (isSpacedToken && shouldInsertTrailingSpace(nextChar, nextIsToken)) {
                logEntry.appendChild(document.createTextNode(' '));
                lastChar = ' ';
            }
        });
        logContent.appendChild(logEntry);
    });

    // Auto-scroll only if user is already near bottom
    if (isLogExpanded && !isActiveChatTab() && !isActiveHistoryTab()) {
        const shouldScrollToLatest = shouldScrollLogToLatestOnRender || wasNearBottom;
        if (shouldScrollToLatest) {
            logContent.scrollTop = logContent.scrollHeight;
        } else {
            const maxScrollTop = Math.max(0, logContent.scrollHeight - logContent.clientHeight);
            logContent.scrollTop = Math.min(previousScrollTop, maxScrollTop);
        }
        shouldScrollLogToLatestOnRender = false;
    }
    syncTopShadowMask(logContent, logContent);
}


// --- 处理高亮 (可交互状态) 的辅助函数 ---
// --- 处理高亮 (可交互状态) 的辅助函数 --- // Keeping the comment from original line 411


export { findCardInstanceForLog as findLogCardInstanceByUid };
