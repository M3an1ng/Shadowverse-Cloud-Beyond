import { getUserSession, USER_SESSION_CHANGED_EVENT } from './userSession.js';
import { QUEST_PROGRESS_UPDATED_EVENT, QUEST_REWARD_CLAIMED_EVENT } from './questFeedback.js';

export function setupDailyQuestUI({ apiGetJson, apiPostJson, syncTopNavHoverLock }) {
    const widget = document.getElementById('daily-quest-widget');
    const toggle = document.getElementById('daily-quest-toggle');
    const badge = document.getElementById('daily-quest-badge');
    const toast = document.getElementById('daily-quest-toast');
    const card = document.getElementById('daily-quest-card');
    const refreshBtn = document.getElementById('daily-quest-refresh');
    const summaryEl = document.getElementById('daily-quest-summary');
    const listEl = document.getElementById('daily-quest-list');

    if (!widget || !toggle || !badge || !card || !refreshBtn || !summaryEl || !listEl) return;

    let stateRequest = null;
    let toastTimer = null;

    const pickValue = (source, camelKey, pascalKey, fallback = null) => {
        if (!source || typeof source !== 'object') return fallback;
        if (source[camelKey] !== undefined && source[camelKey] !== null) return source[camelKey];
        if (source[pascalKey] !== undefined && source[pascalKey] !== null) return source[pascalKey];
        return fallback;
    };

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const toSafeInt = (value, fallback = 0) => {
        const parsed = Number.parseInt(String(value ?? ''), 10);
        return Number.isFinite(parsed) ? parsed : fallback;
    };

    const isQuestClaimable = (quest) =>
        !!pickValue(quest, 'isCompleted', 'IsCompleted', false) &&
        !pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false) &&
        !pickValue(quest, 'isExpired', 'IsExpired', false);

    const setBadge = (claimableCount) => {
        const count = Math.max(0, toSafeInt(claimableCount, 0));
        badge.textContent = count > 9 ? '9+' : String(count);
        badge.classList.toggle('is-visible', count > 0);
        toggle.setAttribute('aria-label', count > 0 ? `日常任务，${count} 个奖励可领取` : '日常任务');
    };

    const showToast = (message) => {
        if (!toast || !message) return;
        toast.textContent = message;
        toast.classList.add('is-visible');
        if (toastTimer) {
            window.clearTimeout(toastTimer);
        }
        toastTimer = window.setTimeout(() => {
            toast.classList.remove('is-visible');
            toastTimer = null;
        }, 1500);
    };

    const pulseToggle = (completed) => {
        const className = completed ? 'is-complete-pulse' : 'is-progress-pulse';
        toggle.classList.remove('is-progress-pulse', 'is-complete-pulse');
        void toggle.offsetWidth;
        toggle.classList.add(className);
        window.setTimeout(() => {
            toggle.classList.remove(className);
        }, completed ? 760 : 560);
    };

    const setSummary = (html, isError = false) => {
        summaryEl.innerHTML = html;
        summaryEl.classList.toggle('daily-quest-error-text', isError);
    };

    const renderEmpty = (message, isError = false) => {
        listEl.innerHTML = `<div class="${isError ? 'daily-quest-error' : 'daily-quest-empty'}">${escapeHtml(message)}</div>`;
    };

    const getQuestStatus = (quest) => {
        if (pickValue(quest, 'isExpired', 'IsExpired', false)) return '已过期';
        if (pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false)) return '已领取';
        if (pickValue(quest, 'isCompleted', 'IsCompleted', false)) return '已完成';
        return '';
    };

    const getQuestIconPath = (quest) => {
        const typeText = `${pickValue(quest, 'typeDisplayName', 'TypeDisplayName', '')} ${pickValue(quest, 'name', 'Name', '')}`;
        if (typeText.includes('评论')) {
            return 'M4 5.5A2.5 2.5 0 0 1 6.5 3h11A2.5 2.5 0 0 1 20 5.5v7A2.5 2.5 0 0 1 17.5 15H9l-5 4V5.5z';
        }
        if (typeText.includes('登录')) {
            return 'M10 3h9v18h-9v-2h7V5h-7V3zm1.4 5.6L14.8 12l-3.4 3.4L10 14l1-1H4v-2h7l-1-1 1.4-1.4z';
        }
        if (typeText.includes('观战')) {
            return 'M12 5c5 0 8.5 4.2 9.6 7-1.1 2.8-4.6 7-9.6 7s-8.5-4.2-9.6-7C3.5 9.2 7 5 12 5zm0 3.5A3.5 3.5 0 1 0 12 15a3.5 3.5 0 0 0 0-7z';
        }
        if (typeText.includes('胜') || typeText.includes('对战')) {
            return 'M7 4h10v3h3v3a5 5 0 0 1-5 5h-.2A5.5 5.5 0 0 1 13 17.7V20h3v2H8v-2h3v-2.3A5.5 5.5 0 0 1 9.2 15H9a5 5 0 0 1-5-5V7h3V4zm10 5V7h-1v5.6A3 3 0 0 0 17 9zM7 7H6v2a3 3 0 0 0 1 2.2V7z';
        }
        return 'M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2zm1 4h8V5H8v2zm1 4.7 1.3 1.3 3.7-3.7 1.4 1.4-5.1 5.1L7.6 13l1.4-1.3z';
    };

    const renderQuestRow = (quest) => {
        const id = String(pickValue(quest, 'id', 'Id', '') || '');
        const name = String(pickValue(quest, 'name', 'Name', '任务') || '任务');
        const description = String(pickValue(quest, 'description', 'Description', '') || '');
        const currentProgress = Math.max(0, toSafeInt(pickValue(quest, 'currentProgress', 'CurrentProgress', 0), 0));
        const targetValue = Math.max(0, toSafeInt(pickValue(quest, 'targetValue', 'TargetValue', 0), 0));
        const coinReward = Math.max(0, toSafeInt(pickValue(quest, 'coinReward', 'CoinReward', 0), 0));
        const percent = targetValue > 0 ? Math.min(100, Math.round((currentProgress / targetValue) * 100)) : 0;
        const canClaim = isQuestClaimable(quest);
        const isClaimed = !!pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false);
        const status = getQuestStatus(quest);

        return `
            <div class="daily-quest-row ${canClaim ? 'is-claimable' : ''} ${isClaimed ? 'is-claimed' : ''}">
                <div class="daily-quest-icon" aria-hidden="true">
                    <svg viewBox="0 0 24 24"><path d="${getQuestIconPath(quest)}"></path></svg>
                </div>
                <div class="daily-quest-main">
                    <div class="daily-quest-row-head">
                        <div class="daily-quest-name" title="${escapeHtml(name)}">${escapeHtml(name)}</div>
                        <div class="daily-quest-reward">+${coinReward} 金币</div>
                    </div>
                    <div class="daily-quest-desc">${escapeHtml(description || status || '活动任务')}</div>
                    <div class="daily-quest-progress-line">
                        <div class="daily-quest-progress-track" role="progressbar" aria-valuemin="0" aria-valuemax="${targetValue}" aria-valuenow="${currentProgress}">
                            <div class="daily-quest-progress-fill" style="width: ${percent}%"></div>
                        </div>
                        <div class="daily-quest-progress-text">${currentProgress}/${targetValue}</div>
                    </div>
                </div>
                ${canClaim
                    ? `<button class="meta-btn daily-quest-claim" type="button" data-quest-id="${escapeHtml(id)}">领取</button>`
                    : `<div class="daily-quest-status">${escapeHtml(status)}</div>`}
            </div>
        `;
    };

    const renderQuestGroup = (title, quests) => {
        if (!quests.length) return '';
        return `
            <div class="daily-quest-group">
                <div class="daily-quest-group-title">${escapeHtml(title)}</div>
                ${quests.map(renderQuestRow).join('')}
            </div>
        `;
    };

    const renderQuests = (quests, claimableCount) => {
        const list = Array.isArray(quests) ? quests : [];
        if (list.length === 0) {
            renderEmpty('暂无可显示任务');
            return;
        }

        const claimable = list.filter(isQuestClaimable);
        const claimed = list.filter(quest => pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false));
        const inProgress = list.filter(quest => !isQuestClaimable(quest) && !pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false));
        const completedCount = list.filter(quest => pickValue(quest, 'isCompleted', 'IsCompleted', false)).length;
        const resetAt = list
            .map(quest => new Date(pickValue(quest, 'expiresAt', 'ExpiresAt', '')))
            .filter(date => !Number.isNaN(date.getTime()))
            .sort((a, b) => a.getTime() - b.getTime())[0];
        const resetText = resetAt
            ? resetAt.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
            : '--:--';

        setSummary(`今日 ${completedCount}/${list.length} · <strong>可领取 ${Math.max(0, toSafeInt(claimableCount, claimable.length))}</strong> · 重置 ${resetText}`);
        listEl.innerHTML = [
            renderQuestGroup('可领取', claimable),
            renderQuestGroup('进行中', inProgress),
            renderQuestGroup('已领取', claimed)
        ].join('');
    };

    const refresh = async (force = false) => {
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId) {
            setBadge(0);
            setSummary('登录后查看任务进度');
            renderEmpty('请先登录账号');
            return;
        }

        if (stateRequest && !force) {
            await stateRequest;
            return;
        }

        refreshBtn.disabled = true;
        setSummary('加载中...');
        stateRequest = (async () => {
            try {
                const state = await apiGetJson(`/api/economy/state/${session.userId}`);
                const quests = Array.isArray(state?.quests) ? state.quests : [];
                const claimableCount = toSafeInt(pickValue(state, 'claimableQuestCount', 'ClaimableQuestCount', 0), quests.filter(isQuestClaimable).length);
                setBadge(claimableCount);
                renderQuests(quests, claimableCount);
            } catch (err) {
                setBadge(0);
                setSummary('任务加载失败', true);
                renderEmpty(`加载失败：${err?.message || String(err)}`, true);
            } finally {
                refreshBtn.disabled = false;
                stateRequest = null;
            }
        })();

        await stateRequest;
    };

    const setOpen = (open) => {
        card.classList.toggle('active', open);
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) {
            document.querySelectorAll('.top-nav-bar .user-card-popup.active').forEach((item) => {
                if (item !== card) {
                    item.classList.remove('active');
                    item.setAttribute('aria-hidden', 'true');
                }
            });
            refresh(false).catch(() => { });
        }
        syncTopNavHoverLock();
    };

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        setOpen(!card.classList.contains('active'));
    });

    refreshBtn.addEventListener('click', () => {
        refresh(true).catch(() => { });
    });

    listEl.addEventListener('click', async (event) => {
        const target = event.target;
        const btn = target instanceof Element ? target.closest('button[data-quest-id]') : null;
        const questId = btn?.getAttribute('data-quest-id');
        const session = getUserSession();
        if (!btn || !questId || session.mode !== 'user' || !session.userId) return;

        btn.disabled = true;
        btn.textContent = '领取中';
        try {
            const payload = await apiPostJson(`/api/economy/quests/${session.userId}/${questId}/claim`, {});
            const currentCoins = toSafeInt(pickValue(payload, 'currentCoins', 'CurrentCoins', null), -1);
            const coinReward = Math.max(0, toSafeInt(pickValue(payload, 'coinReward', 'CoinReward', 0), 0));
            if (currentCoins >= 0) {
                document.dispatchEvent(new CustomEvent(QUEST_REWARD_CLAIMED_EVENT, {
                    detail: {
                        userId: session.userId,
                        coins: currentCoins,
                        gain: coinReward
                    }
                }));
            }
            showToast(`已领取 +${coinReward} 金币`);
        } catch (err) {
            showToast(`领取失败：${err?.message || String(err)}`);
        } finally {
            await refresh(true);
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

    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => {
        refresh(true).catch(() => { });
    });

    document.addEventListener(QUEST_PROGRESS_UPDATED_EVENT, (event) => {
        const detail = event?.detail;
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId || String(detail?.userId ?? '') !== session.userId) {
            return;
        }

        const progress = detail?.questProgress;
        const completed = Array.isArray(progress?.completedQuestIds) && progress.completedQuestIds.length > 0;
        const updated = Array.isArray(progress?.updatedQuestIds) && progress.updatedQuestIds.length > 0;
        if (!completed && !updated) {
            return;
        }

        pulseToggle(completed);
        showToast(completed ? '任务完成，可领取奖励' : '任务进度 +1');
        refresh(true).catch(() => { });
    });

    refresh(false).catch(() => { });
}
