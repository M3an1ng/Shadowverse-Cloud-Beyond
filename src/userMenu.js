import { clearUserSession, getUserSession, setUserSession, USER_SESSION_CHANGED_EVENT } from './userSession.js';
import { QUEST_REWARD_CLAIMED_EVENT } from './questFeedback.js';
import {
    ensureDeckEnvironmentOptions,
    getDeckEnvironmentLabel as getDeckEnvironmentLabelFromOptions,
    getDeckEnvironmentOptions,
    getDefaultDeckEnvironment,
    normalizeDeckEnvironment as normalizeDeckEnvironmentFromOptions
} from './deckEnvironmentOptions.js';

export function setupUserInfoUI({ apiGetJson, apiPostJson, syncTopNavHoverLock }) {
    const toggle = document.getElementById('user-info-toggle');
    const card = document.getElementById('user-info-card');
    const aboutCard = document.getElementById('about-card');
    const dailyQuestCard = document.getElementById('daily-quest-card');
    const devConsoleToggle = document.getElementById('dev-console-toggle');
    const nameEl = document.getElementById('user-info-name');
    const roleEl = document.getElementById('user-info-role');
    const idEl = document.getElementById('user-info-id');
    const avatarEl = document.getElementById('user-info-avatar');
    const avatarImgEl = document.getElementById('user-info-avatar-img');
    const avatarTextEl = document.getElementById('user-info-avatar-text');
    const idInline = document.getElementById('user-id-inline');
    const coinsEl = document.getElementById('user-info-coins');
    const coinsItemEl = coinsEl?.closest('.user-resource-item.resource-coin') ?? null;
    const totalWinsEl = document.getElementById('user-info-total-wins');
    const authSection = document.getElementById('user-auth');
    const usernameInput = document.getElementById('user-auth-username');
    const passwordInput = document.getElementById('user-auth-password');
    const registerBtn = document.getElementById('user-auth-register');
    const loginBtn = document.getElementById('user-auth-login');
    const logoutBtn = document.getElementById('user-auth-logout');
    const statusEl = document.getElementById('user-auth-status');

    if (!toggle || !card || !nameEl || !roleEl || !idEl || !avatarEl || !avatarTextEl) {
        return;
    }

    const winStatColumns = Array.from(card.querySelectorAll('.win-stat-column[data-class]')).map((column) => ({
        key: String(column.getAttribute('data-class') || '').trim().toLowerCase(),
        fillEl: column.querySelector('.win-stat-fill'),
        valueEl: column.querySelector('.win-stat-value')
    })).filter((item) => item.key.length > 0);
    const winStatsTitleEl = card.querySelector('#win-stats-title');
    const winStatsEnvironmentToggleBtn = card.querySelector('#win-stats-mode-toggle');

    const isAvatarUrl = (value) => {
        if (!value) return false;
        return value.startsWith('/') || value.startsWith('http://') || value.startsWith('https://') || value.startsWith('data:');
    };

    const setStatus = (text, isError = false) => {
        if (!statusEl) return;
        statusEl.textContent = text || '';
        statusEl.style.color = isError ? 'rgba(255, 120, 120, 0.9)' : 'rgba(255, 255, 255, 0.7)';
    };

    const setAuthBusy = (busy) => {
        if (registerBtn) registerBtn.disabled = busy;
        if (loginBtn) loginBtn.disabled = busy;
        if (logoutBtn) logoutBtn.disabled = busy;
    };

    const normalizeRole = (role) => {
        const normalized = String(role || "").trim().toLowerCase();
        if (normalized === "admin") return "admin";
        if (normalized === "guest") return "guest";
        return "player";
    };

    const resolvePermissionLevel = (role, permissionLevel) => {
        if (Number.isFinite(permissionLevel)) {
            return Math.max(0, Math.trunc(permissionLevel));
        }
        const normalizedRole = normalizeRole(role);
        if (normalizedRole === "admin") return 100;
        if (normalizedRole === "player") return 10;
        return 0;
    };

    const formatRoleDisplay = (role, permissionLevel) => {
        const normalizedRole = normalizeRole(role);
        const level = resolvePermissionLevel(normalizedRole, permissionLevel);
        const label = normalizedRole === "admin"
            ? "ADMIN"
            : (normalizedRole === "guest" ? "GUEST" : "PLAYER");
        return `${label} · L${level}`;
    };

    const maskIdValue = (value) => {
        if (!value || value === '-') return '-';
        return '********';
    };

    const resetIdRow = (value) => {
        if (!idInline || !idEl) return;
        const fullValue = value || '-';
        idInline.classList.remove('expanded');
        idInline.setAttribute('aria-expanded', 'false');
        idEl.dataset.fullId = fullValue;
        idEl.textContent = maskIdValue(fullValue);
        idEl.setAttribute('title', '点击展示');
    };

    const expandIdRow = () => {
        if (!idInline || !idEl) return;
        idInline.classList.add('expanded');
        idInline.setAttribute('aria-expanded', 'true');
        const fullValue = idEl.dataset.fullId || idEl.textContent || '-';
        idEl.textContent = fullValue;
        idEl.setAttribute('title', '点击隐藏');
    };

    const toggleIdRow = () => {
        if (!idInline || !idEl) return;
        if (idInline.classList.contains('expanded')) {
            resetIdRow(idEl.dataset.fullId || idEl.textContent || '-');
            return;
        }
        expandIdRow();
    };

    let economyRequest = null;
    let winStatsRequest = null;
    const economyCache = {
        userId: null,
        coins: null,
        fetchedAt: 0
    };
    const winStatsCache = {
        userId: null,
        environment: null,
        totalWins: null,
        classWins: null,
        fetchedAt: 0
    };
    const DEFAULT_WIN_STATS_ENVIRONMENT = getDefaultDeckEnvironment();
    const FIXED_WIN_STATS_DECK_RULE = 'standard';
    let activeWinStatsEnvironment = DEFAULT_WIN_STATS_ENVIRONMENT;
    let winStatsEnvironmentsLoaded = false;
    let winStatsEnvironments = [];

    const pickValue = (source, camelKey, pascalKey) => {
        if (!source || typeof source !== 'object') return null;
        if (source[camelKey] != null) return source[camelKey];
        if (source[pascalKey] != null) return source[pascalKey];
        return null;
    };

    const toSafeInt = (value, fallback = 0) => {
        const parsed = Number.parseInt(String(value ?? ''), 10);
        return Number.isFinite(parsed) ? parsed : fallback;
    };

    const normalizeClassKey = (value) => String(value ?? '').trim().toLowerCase();
    const normalizeWinStatsEnvironment = (value) =>
        normalizeDeckEnvironmentFromOptions(value, getDefaultDeckEnvironment());
    const getDefaultWinStatsEnvironments = () =>
        getDeckEnvironmentOptions().map((item) => ({ key: item.value, label: item.label }));
    const resolveWinStatsEnvironmentOption = (environment) => {
        const normalized = normalizeWinStatsEnvironment(environment);
        return winStatsEnvironments.find((item) => item.key === normalized) || null;
    };
    const resolveWinStatsEnvironment = (environment) =>
        resolveWinStatsEnvironmentOption(environment)?.key
        || (winStatsEnvironments[0]?.key ?? DEFAULT_WIN_STATS_ENVIRONMENT);
    const getNextWinStatsEnvironment = (environment) => {
        const current = resolveWinStatsEnvironment(environment);
        if (winStatsEnvironments.length <= 1) return current;
        const currentIndex = winStatsEnvironments.findIndex((item) => item.key === current);
        const nextIndex = currentIndex >= 0 ? (currentIndex + 1) % winStatsEnvironments.length : 0;
        return winStatsEnvironments[nextIndex].key;
    };
    const getWinStatsEnvironmentLabel = (environment) => {
        return resolveWinStatsEnvironmentOption(environment)?.label
            || getDeckEnvironmentLabelFromOptions(environment);
    };
    const getWinStatsTitleText = (environment) => `位阶对战：${getWinStatsEnvironmentLabel(environment)}`;
    const getEnvironmentToggleAriaLabel = (environment) => {
        if (winStatsEnvironments.length <= 1) {
            return `当前环境：${getWinStatsEnvironmentLabel(environment)}`;
        }
        const nextEnvironment = getNextWinStatsEnvironment(environment);
        return `切换到${getWinStatsEnvironmentLabel(nextEnvironment)}职业胜场`;
    };

    const ensureWinStatsEnvironmentsLoaded = async () => {
        if (winStatsEnvironmentsLoaded) return;
        winStatsEnvironments = getDefaultWinStatsEnvironments();
        try {
            await ensureDeckEnvironmentOptions();
            const options = getDeckEnvironmentOptions();
            if (options.length > 0) {
                winStatsEnvironments = options.map((item) => ({
                    key: item.value,
                    label: item.label
                }));
            }
        } catch (error) {
            console.warn('MAIN: Failed to load DeckEnvironment enum localization, fallback to default environment list.', error);
        } finally {
            winStatsEnvironmentsLoaded = true;
            activeWinStatsEnvironment = resolveWinStatsEnvironment(activeWinStatsEnvironment);
            syncWinStatsEnvironmentToggle();
        }
    };

    const syncWinStatsEnvironmentToggle = () => {
        const normalizedEnvironment = resolveWinStatsEnvironment(activeWinStatsEnvironment);
        activeWinStatsEnvironment = normalizedEnvironment;
        if (winStatsTitleEl) {
            winStatsTitleEl.textContent = getWinStatsTitleText(normalizedEnvironment);
        }
        if (!winStatsEnvironmentToggleBtn) return;
        winStatsEnvironmentToggleBtn.dataset.environment = normalizedEnvironment;
        winStatsEnvironmentToggleBtn.setAttribute('aria-label', getEnvironmentToggleAriaLabel(normalizedEnvironment));
        winStatsEnvironmentToggleBtn.disabled = winStatsEnvironments.length <= 1;
        const nextEnvironment = getNextWinStatsEnvironment(normalizedEnvironment);
        const nextEnvironmentText = getWinStatsEnvironmentLabel(nextEnvironment);
        winStatsEnvironmentToggleBtn.setAttribute('title', winStatsEnvironments.length <= 1 ? `当前仅支持${getWinStatsEnvironmentLabel(normalizedEnvironment)}` : `切换到${nextEnvironmentText}`);
        if (winStatsEnvironmentToggleBtn instanceof HTMLButtonElement) {
            winStatsEnvironmentToggleBtn.dataset.nextEnvironment = nextEnvironment;
        }
    };

    const setWinStatsEnvironment = (environment, forceRefresh = false) => {
        const normalized = resolveWinStatsEnvironment(environment);
        const changed = normalized !== activeWinStatsEnvironment;
        activeWinStatsEnvironment = normalized;
        syncWinStatsEnvironmentToggle();
        if (changed || forceRefresh) {
            refreshWinStats(true).catch(() => { });
        }
    };

    const createZeroClassWins = () => {
        const wins = {};
        for (const item of winStatColumns) {
            wins[item.key] = 0;
        }
        return wins;
    };

    const normalizeClassWins = (statePayload) => {
        const normalizedWins = createZeroClassWins();
        if (!winStatColumns.length) return normalizedWins;
        const classStats = pickValue(statePayload, 'classStats', 'ClassStats');
        if (!Array.isArray(classStats)) return normalizedWins;

        for (const item of classStats) {
            const rawClass = normalizeClassKey(pickValue(item, 'deckClass', 'DeckClass'));
            if (!rawClass || !(rawClass in normalizedWins)) continue;
            const wins = Math.max(0, toSafeInt(pickValue(item, 'totalWins', 'TotalWins'), 0));
            normalizedWins[rawClass] += wins;
        }
        return normalizedWins;
    };

    const setClassWinsValue = (winsByClass) => {
        if (!winStatColumns.length) return;
        const fillToneClasses = [
            'tone-forestcraft',
            'tone-swordcraft',
            'tone-runecraft',
            'tone-dragoncraft',
            'tone-abysscraft',
            'tone-havencraft',
            'tone-portalcraft'
        ];
        const normalizedWins = createZeroClassWins();
        if (winsByClass && typeof winsByClass === 'object') {
            for (const key of Object.keys(normalizedWins)) {
                const wins = toSafeInt(winsByClass[key], 0);
                normalizedWins[key] = Math.max(0, wins);
            }
        }

        let maxWins = 0;
        for (const item of winStatColumns) {
            const wins = normalizedWins[item.key] ?? 0;
            maxWins = Math.max(maxWins, wins);
            if (item.valueEl) {
                item.valueEl.textContent = String(wins);
            }
            if (item.fillEl) {
                item.fillEl.style.height = '0%';
                item.fillEl.classList.remove('active', ...fillToneClasses);
            }
        }

        if (maxWins <= 0) return;

        const minimumBarHeightPercent = 6;
        for (const item of winStatColumns) {
            const wins = normalizedWins[item.key] ?? 0;
            if (wins <= 0 || !item.fillEl) continue;
            const ratio = wins / maxWins;
            const barHeight = Math.max(minimumBarHeightPercent, Math.round(ratio * 100));
            item.fillEl.style.height = `${Math.min(barHeight, 100)}%`;
            item.fillEl.classList.add('active');
            const toneClass = `tone-${item.key}`;
            if (fillToneClasses.includes(toneClass)) {
                item.fillEl.classList.add(toneClass);
            }
        }
    };

    const setCoinsValue = (value) => {
        if (!coinsEl) return;
        coinsEl.textContent = value;
    };

    let coinsGainAnimationTimer = null;
    const triggerCoinsGainAnimation = (gain) => {
        if (!coinsItemEl || !Number.isFinite(gain) || gain <= 0) {
            return;
        }

        coinsItemEl.dataset.coinGain = `+${gain}`;
        coinsItemEl.classList.remove('is-gain-pulse');
        void coinsItemEl.offsetWidth;
        coinsItemEl.classList.add('is-gain-pulse');

        if (coinsGainAnimationTimer) {
            window.clearTimeout(coinsGainAnimationTimer);
        }
        coinsGainAnimationTimer = window.setTimeout(() => {
            coinsItemEl.classList.remove('is-gain-pulse');
            delete coinsItemEl.dataset.coinGain;
            coinsGainAnimationTimer = null;
        }, 1500);
    };

    const applyCoinsUpdate = (userId, coins) => {
        if (!coinsEl) return;
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId || session.userId !== userId) {
            return;
        }

        const previousCoins = economyCache.userId === userId && Number.isFinite(economyCache.coins)
            ? economyCache.coins
            : null;
        economyCache.userId = userId;
        economyCache.coins = coins;
        economyCache.fetchedAt = Date.now();
        setCoinsValue(String(coins));
        if (previousCoins != null && coins > previousCoins) {
            triggerCoinsGainAnimation(coins - previousCoins);
        }
    };

    const setTotalWinsValue = (value) => {
        if (!totalWinsEl) return;
        totalWinsEl.textContent = value;
    };

    const refreshEconomy = async (force = false) => {
        if (!coinsEl) return;
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId) {
            setCoinsValue('0');
            return;
        }

        const now = Date.now();
        if (!force && economyCache.userId === session.userId && now - economyCache.fetchedAt < 15000) {
            if (economyCache.coins != null) {
                setCoinsValue(String(economyCache.coins));
                return;
            }
        }

        if (economyRequest) {
            await economyRequest;
            return;
        }

        setCoinsValue('...');
        economyRequest = (async () => {
            try {
                const coinsPayload = await apiGetJson(`/api/economy/coins/${session.userId}`);
                const coins = Math.max(0, toSafeInt(coinsPayload, 0));
                economyCache.userId = session.userId;
                economyCache.coins = coins;
                economyCache.fetchedAt = Date.now();
                setCoinsValue(String(coins));
            } catch (err) {
                setCoinsValue('0');
            } finally {
                economyRequest = null;
            }
        })();

        await economyRequest;
    };

    const refreshWinStats = async (force = false) => {
        if (!totalWinsEl && !winStatColumns.length) return;
        await ensureWinStatsEnvironmentsLoaded();
        const session = getUserSession();
        const activeEnvironment = resolveWinStatsEnvironment(activeWinStatsEnvironment);
        if (session.mode !== 'user' || !session.userId) {
                setTotalWinsValue('--');
                setClassWinsValue(null);
            return;
        }

        const now = Date.now();
        if (!force &&
            winStatsCache.userId === session.userId &&
            winStatsCache.environment === activeEnvironment &&
            now - winStatsCache.fetchedAt < 15000) {
            if (winStatsCache.totalWins != null || winStatsCache.classWins != null) {
                setTotalWinsValue(winStatsCache.totalWins != null ? String(winStatsCache.totalWins) : '--');
                setClassWinsValue(winStatsCache.classWins);
                return;
            }
        }

        if (winStatsRequest) {
            await winStatsRequest;
            if (!force &&
                winStatsCache.userId === session.userId &&
                winStatsCache.environment === activeEnvironment) {
                return;
            }
        }

        setTotalWinsValue('...');
        winStatsRequest = (async () => {
            try {
                const query = new URLSearchParams();
                query.set('environment', activeEnvironment);
                query.set('deckRule', FIXED_WIN_STATS_DECK_RULE);
                const state = await apiGetJson(`/api/economy/state/${session.userId}?${query.toString()}`);
                const classWins = normalizeClassWins(state);
                const classWinsTotal = Object.values(classWins).reduce((sum, item) => sum + toSafeInt(item, 0), 0);
                // Keep total wins aligned with current environment/rule filters.
                const totalWins = classWinsTotal;

                winStatsCache.userId = session.userId;
                winStatsCache.environment = activeEnvironment;
                winStatsCache.totalWins = totalWins;
                winStatsCache.classWins = classWins;
                winStatsCache.fetchedAt = Date.now();
                setTotalWinsValue(String(totalWins));
                setClassWinsValue(classWins);
            } catch (err) {
                setTotalWinsValue('--');
                setClassWinsValue(null);
            } finally {
                winStatsRequest = null;
            }
        })();

        await winStatsRequest;
    };

    winStatsEnvironmentToggleBtn?.addEventListener('click', () => {
        const nextEnvironment = getNextWinStatsEnvironment(activeWinStatsEnvironment);
        setWinStatsEnvironment(nextEnvironment, true);
    });
    syncWinStatsEnvironmentToggle();
    ensureWinStatsEnvironmentsLoaded().catch(() => { });

    const applySession = () => {
        const session = getUserSession();
        const isUser = session.mode === "user";
        // toggle button is now an icon, no text content update needed
        nameEl.textContent = session.displayName || (isUser ? "用户" : "游客");
        roleEl.textContent = formatRoleDisplay(session.role, session.permissionLevel);
        card.classList.toggle('is-user', isUser);
        resetIdRow(session.userId || session.guestId || "-");
        const avatarValue = session.avatar || (isUser ? "U" : "G");
        const hasAvatarImage = isAvatarUrl(avatarValue);
        if (hasAvatarImage && avatarImgEl) {
            avatarImgEl.src = avatarValue;
            avatarImgEl.alt = `${session.displayName || "User"} avatar`;
            avatarEl.classList.add('has-image');
        } else {
            avatarEl.classList.remove('has-image');
            if (avatarImgEl) {
                avatarImgEl.removeAttribute('src');
            }
        }
        avatarTextEl.textContent = hasAvatarImage
            ? (session.displayName || "U").trim().charAt(0).toUpperCase()
            : avatarValue;
        if (authSection) {
            authSection.classList.toggle('is-user', isUser);
        }
        if (isUser) {
            if (usernameInput) usernameInput.value = session.displayName || '';
            if (passwordInput) passwordInput.value = '';
        }

        refreshEconomy(true).catch(() => { });
        refreshWinStats(true).catch(() => { });
    };

    const setOpen = (open) => {
        card.classList.toggle('active', open); // Changed from 'open' to 'active' to match CSS
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
        if (open && aboutCard) {
            aboutCard.classList.remove('active');
            aboutCard.setAttribute('aria-hidden', 'true');
        }
        if (open && dailyQuestCard) {
            dailyQuestCard.classList.remove('active');
            dailyQuestCard.setAttribute('aria-hidden', 'true');
            document.getElementById('daily-quest-toggle')?.setAttribute('aria-expanded', 'false');
        }
        if (!open) {
            resetIdRow(idEl?.dataset.fullId || idEl?.textContent || "-");
        } else {
            refreshEconomy(false).catch(() => { });
            refreshWinStats(false).catch(() => { });
        }
        syncTopNavHoverLock();
    };

    applySession();

    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => {
        applySession();
    });
    document.addEventListener(QUEST_REWARD_CLAIMED_EVENT, (event) => {
        const detail = event?.detail;
        const userId = String(detail?.userId ?? '').trim();
        const coins = Number.parseInt(String(detail?.coins ?? ''), 10);
        if (!userId || !Number.isFinite(coins) || coins < 0) {
            return;
        }
        applyCoinsUpdate(userId, coins);
    });

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        applySession();
        const isOpen = card.classList.contains('active');
        setOpen(!isOpen);
    });

    devConsoleToggle?.addEventListener('click', () => {
        setOpen(false);
    });

    idInline?.addEventListener('click', (event) => {
        event.stopPropagation();
        toggleIdRow();
    });

    idInline?.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            toggleIdRow();
        }
    });

    document.addEventListener('click', (event) => {
        if (!card.classList.contains('active')) return;
        const target = event.target;
        if (target instanceof Element && (card.contains(target) || toggle.contains(target))) {
            return;
        }
        setOpen(false);
    });

    const openAvatarCropModal = (session) => {
        if (document.querySelector('.avatar-crop-modal')) return;

        const modal = document.createElement('div');
        modal.className = 'avatar-crop-modal';
        modal.innerHTML = `
            <div class="avatar-crop-card">
                <div class="avatar-crop-header">
                    <div class="avatar-crop-title">上传头像</div>
                    <button type="button" class="avatar-crop-close">×</button>
                </div>
                <div class="avatar-crop-stage">
                    <img class="avatar-crop-image" alt="Avatar preview">
                    <div class="avatar-crop-box">
                        <span class="avatar-crop-handle"></span>
                    </div>
                    <div class="avatar-crop-placeholder">
                        <div class="avatar-crop-placeholder-icon">UPLOAD</div>
                        <div class="avatar-crop-placeholder-text">点击选择头像图片</div>
                    </div>
                </div>
                <input class="avatar-crop-file" type="file" accept="image/*">
                <div class="avatar-crop-actions">
                    <button type="button" class="btn avatar-crop-cancel">取消</button>
                    <button type="button" class="btn btn-primary avatar-crop-upload">上传</button>
                </div>
                <div class="avatar-crop-status"></div>
            </div>
        `;

        document.body.appendChild(modal);

        const closeBtn = modal.querySelector('.avatar-crop-close');
        const cancelBtn = modal.querySelector('.avatar-crop-cancel');
        const uploadBtn = modal.querySelector('.avatar-crop-upload');
        const input = modal.querySelector('.avatar-crop-file');
        const status = modal.querySelector('.avatar-crop-status');
        const stage = modal.querySelector('.avatar-crop-stage');
        const imageEl = modal.querySelector('.avatar-crop-image');
        const cropBox = modal.querySelector('.avatar-crop-box');
        const resizeHandle = modal.querySelector('.avatar-crop-handle');

        const state = {
            objectUrl: null,
            imageRect: null,
            naturalWidth: 0,
            naturalHeight: 0,
            crop: { x: 0, y: 0, size: 0 },
            dragging: false,
            resizing: false,
            startX: 0,
            startY: 0,
            startCropX: 0,
            startCropY: 0,
            startSize: 0
        };

        const setModalStatus = (text, isError = false) => {
            if (!status) return;
            status.textContent = text || '';
            status.style.color = isError ? 'rgba(255, 120, 120, 0.9)' : 'rgba(255, 255, 255, 0.7)';
        };

        const cleanup = () => {
            if (state.objectUrl) {
                URL.revokeObjectURL(state.objectUrl);
            }
            modal.remove();
        };

        const closeModal = () => {
            cleanup();
        };

        const updateImageRect = () => {
            if (!stage || !imageEl) return;
            const stageRect = stage.getBoundingClientRect();
            const imgRect = imageEl.getBoundingClientRect();
            state.imageRect = {
                x: imgRect.left - stageRect.left,
                y: imgRect.top - stageRect.top,
                width: imgRect.width,
                height: imgRect.height
            };
        };

        const clampCrop = () => {
            if (!state.imageRect) return;
            const minSize = 80;
            const maxSize = Math.min(state.imageRect.width, state.imageRect.height);
            state.crop.size = Math.min(Math.max(state.crop.size, minSize), maxSize);
            const maxX = state.imageRect.x + state.imageRect.width - state.crop.size;
            const maxY = state.imageRect.y + state.imageRect.height - state.crop.size;
            state.crop.x = Math.min(Math.max(state.crop.x, state.imageRect.x), maxX);
            state.crop.y = Math.min(Math.max(state.crop.y, state.imageRect.y), maxY);
        };

        const renderCrop = () => {
            if (!cropBox) return;
            cropBox.style.left = `${state.crop.x}px`;
            cropBox.style.top = `${state.crop.y}px`;
            cropBox.style.width = `${state.crop.size}px`;
            cropBox.style.height = `${state.crop.size}px`;
        };

        const initCropBox = () => {
            if (!state.imageRect) return;
            const size = Math.min(state.imageRect.width, state.imageRect.height) * 0.7;
            state.crop.size = Math.max(80, size);
            state.crop.x = state.imageRect.x + (state.imageRect.width - state.crop.size) / 2;
            state.crop.y = state.imageRect.y + (state.imageRect.height - state.crop.size) / 2;
            clampCrop();
            renderCrop();
        };

        closeBtn?.addEventListener('click', closeModal);
        cancelBtn?.addEventListener('click', closeModal);
        modal.addEventListener('click', (event) => {
            if (event.target === modal) {
                closeModal();
            }
        });

        input?.addEventListener('change', () => {
            const file = input.files?.[0];
            if (!file) return;
            if (!file.type.startsWith('image/')) {
                setModalStatus('不支持的图片类型。', true);
                return;
            }
            stage?.classList.remove('has-image');
            if (cropBox) {
                cropBox.style.display = 'none';
            }
            if (state.objectUrl) {
                URL.revokeObjectURL(state.objectUrl);
            }
            const url = URL.createObjectURL(file);
            state.objectUrl = url;
            if (!imageEl) return;
            imageEl.onload = () => {
                state.naturalWidth = imageEl.naturalWidth;
                state.naturalHeight = imageEl.naturalHeight;
                requestAnimationFrame(() => {
                    updateImageRect();
                    initCropBox();
                    setModalStatus('');
                    stage?.classList.add('has-image');
                });
            };
            imageEl.onerror = () => {
                setModalStatus('图片加载失败。', true);
            };
            imageEl.src = url;
        });

        imageEl?.addEventListener('click', () => {
            if (!stage?.classList.contains('has-image')) {
                input?.click();
            }
        });

        cropBox?.addEventListener('pointerdown', (event) => {
            if (event.target === resizeHandle) return;
            event.preventDefault();
            state.dragging = true;
            state.startX = event.clientX;
            state.startY = event.clientY;
            state.startCropX = state.crop.x;
            state.startCropY = state.crop.y;
            cropBox.setPointerCapture(event.pointerId);
            cropBox.classList.add('dragging');
        });

        resizeHandle?.addEventListener('pointerdown', (event) => {
            event.preventDefault();
            event.stopPropagation();
            state.resizing = true;
            state.startX = event.clientX;
            state.startY = event.clientY;
            state.startSize = state.crop.size;
            resizeHandle.setPointerCapture(event.pointerId);
            cropBox?.classList.add('resizing');
        });

        const onPointerMove = (event) => {
            if (!state.imageRect) return;
            if (state.dragging) {
                const dx = event.clientX - state.startX;
                const dy = event.clientY - state.startY;
                state.crop.x = state.startCropX + dx;
                state.crop.y = state.startCropY + dy;
                clampCrop();
                renderCrop();
            } else if (state.resizing) {
                const dx = event.clientX - state.startX;
                const dy = event.clientY - state.startY;
                const delta = Math.max(dx, dy);
                state.crop.size = state.startSize + delta;
                clampCrop();
                renderCrop();
            }
        };

        const stopPointer = (event) => {
            if (state.dragging && cropBox) {
                state.dragging = false;
                cropBox.releasePointerCapture(event.pointerId);
                cropBox.classList.remove('dragging');
            }
            if (state.resizing && resizeHandle) {
                state.resizing = false;
                resizeHandle.releasePointerCapture(event.pointerId);
                cropBox?.classList.remove('resizing');
            }
        };

        stage?.addEventListener('pointermove', onPointerMove);
        stage?.addEventListener('pointerup', stopPointer);
        stage?.addEventListener('pointerleave', stopPointer);
        stage?.addEventListener('pointercancel', stopPointer);

        uploadBtn?.addEventListener('click', async () => {
            if (!imageEl?.src || !state.imageRect || state.crop.size <= 0) {
                setModalStatus('请先选择图片。', true);
                return;
            }
            setModalStatus('上传中...');
            uploadBtn.disabled = true;
            try {
                const scaleX = state.naturalWidth / state.imageRect.width;
                const scaleY = state.naturalHeight / state.imageRect.height;
                const scale = Math.min(scaleX, scaleY);
                const rawX = (state.crop.x - state.imageRect.x) * scale;
                const rawY = (state.crop.y - state.imageRect.y) * scale;
                const rawSize = state.crop.size * scale;
                const sourceSize = Math.min(rawSize, state.naturalWidth, state.naturalHeight);
                const sourceX = Math.max(0, Math.min(state.naturalWidth - sourceSize, rawX));
                const sourceY = Math.max(0, Math.min(state.naturalHeight - sourceSize, rawY));

                const outputSize = 320;
                const canvas = document.createElement('canvas');
                canvas.width = outputSize;
                canvas.height = outputSize;
                const ctx = canvas.getContext('2d');
                if (!ctx) {
                    throw new Error('头像生成失败。');
                }
                ctx.drawImage(
                    imageEl,
                    sourceX,
                    sourceY,
                    sourceSize,
                    sourceSize,
                    0,
                    0,
                    outputSize,
                    outputSize
                );

                const blob = await new Promise((resolve) => {
                    canvas.toBlob(resolve, 'image/png');
                });
                if (!blob) {
                    throw new Error('头像生成失败。');
                }
                const formData = new FormData();
                formData.append('file', blob, 'avatar.png');
                const response = await fetch(`/api/users/${encodeURIComponent(session.userId)}/avatar`, {
                    method: 'POST',
                    body: formData
                });
                let payload = null;
                try {
                    payload = await response.json();
                } catch (err) {
                    // ignore parse errors
                }
                if (!response.ok) {
                    const message = payload?.message || payload?.Message || response.statusText;
                    throw new Error(message || `HTTP ${response.status}`);
                }
                const avatarUrl = payload?.avatarUrl || payload?.AvatarUrl;
                const cacheBusted = avatarUrl ? `${avatarUrl}?t=${Date.now()}` : '';
                setUserSession({
                    userId: session.userId,
                    username: session.displayName,
                    avatar: cacheBusted || session.avatar,
                    role: session.role,
                    permissionLevel: session.permissionLevel
                });
                applySession();
                setModalStatus('上传完成。');
                setTimeout(closeModal, 400);
            } catch (err) {
                setModalStatus(`上传失败：${err.message}`, true);
            } finally {
                uploadBtn.disabled = false;
            }
        });

        if (cropBox) {
            cropBox.style.display = 'none';
        }
        if (imageEl) {
            imageEl.addEventListener('load', () => {
                if (cropBox) cropBox.style.display = 'block';
                stage?.classList.add('has-image');
            }, { once: true });
        }
    };

    avatarEl.addEventListener('click', (event) => {
        event.stopPropagation();
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId) {
            setStatus('请先登录。', true);
            return;
        }
        openAvatarCropModal(session);
    });

    if (registerBtn && loginBtn && logoutBtn && usernameInput && passwordInput) {
        registerBtn.addEventListener('click', async () => {
            const username = usernameInput.value.trim();
            const password = passwordInput.value;
            if (!username || !password) {
                setStatus('请输入用户名和密码。', true);
                return;
            }
            setAuthBusy(true);
            setStatus('注册中...');
            try {
                const session = getUserSession();
                if (session.mode === 'user') {
                    setStatus('请先退出当前账号。', true);
                    return;
                }
                const payload = await apiPostJson('/api/auth/register', {
                    username,
                    password,
                    guestId: session.guestId || null
                });
                setUserSession({
                    userId: payload.userId,
                    username: payload.username,
                    avatar: payload.avatarUrl,
                    role: payload.role,
                    permissionLevel: payload.permissionLevel
                });
                passwordInput.value = '';
                applySession();
                const migrated = payload.migratedDecks || 0;
                const failed = payload.failedDecks || 0;
                if (failed > 0) {
                    setStatus(`注册成功，迁移卡组 ${migrated}，失败 ${failed}。`, true);
                } else if (migrated > 0) {
                    setStatus(`注册成功，已迁移卡组 ${migrated}。`);
                } else {
                    setStatus('注册成功。');
                }
                document.dispatchEvent(new Event('deckListUpdated'));
            } catch (err) {
                setStatus(`注册失败：${err.message}`, true);
            } finally {
                setAuthBusy(false);
            }
        });

        loginBtn.addEventListener('click', async () => {
            const username = usernameInput.value.trim();
            const password = passwordInput.value;
            if (!username || !password) {
                setStatus('请输入用户名和密码。', true);
                return;
            }
            setAuthBusy(true);
            setStatus('登录中...');
            try {
                const session = getUserSession();
                if (session.mode === 'user') {
                    setStatus('请先退出当前账号。', true);
                    return;
                }
                const payload = await apiPostJson('/api/auth/login', { username, password });
                setUserSession({
                    userId: payload.userId,
                    username: payload.username,
                    avatar: payload.avatarUrl,
                    role: payload.role,
                    permissionLevel: payload.permissionLevel
                });
                passwordInput.value = '';
                applySession();
                setStatus('登录成功。');
                document.dispatchEvent(new Event('deckListUpdated'));
            } catch (err) {
                setStatus(`登录失败：${err.message}`, true);
            } finally {
                setAuthBusy(false);
            }
        });

        logoutBtn.addEventListener('click', () => {
            clearUserSession();
            if (passwordInput) passwordInput.value = '';
            applySession();
            setStatus('已退出。');
            document.dispatchEvent(new Event('deckListUpdated'));
        });
    }
}
