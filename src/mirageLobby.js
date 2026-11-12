// src/mirageLobby.js - 幻境战棋 8人大厅与准备室前端控制器
import * as signalR from '@microsoft/signalr';
import { getUserSession, generateGuid } from './userSession.js';
import { showAlert, confirmDialog } from './siteDialog.js';

const MIRAGE_API_BASE = '/api/mirage/rooms';

let hostContext = null; // { navigateTo, setMirage8PHandshake, getMirage8PHandshake, clearHandshake, tryJoinClassicOrTakeTwoByCode }
let mountedContainer = null;
let activeRoom = null;
let roomList = [];
let pollTimerId = null;
let lobbyHubConnection = null;
let isBusy = false;
let transitioningToGame = false;

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function getActorIdentity() {
    const session = getUserSession();
    const playerId = session?.userId || session?.guestId || generateGuid();
    const displayName = String(session?.displayName || session?.username || '').trim() || '旅人';
    const avatarUrl = String(session?.avatar || '').trim();
    return { playerId, displayName, avatarUrl };
}

async function requestJson(url, options = {}) {
    const response = await fetch(url, {
        headers: {
            'Content-Type': 'application/json',
            ...(options.headers || {})
        },
        ...options
    });
    let payload = null;
    try {
        payload = await response.json();
    } catch {
        // ignore empty body
    }
    if (!response.ok) {
        const msg = payload?.message || payload?.Message || payload?.error || response.statusText;
        const err = new Error(msg || `HTTP ${response.status}`);
        err.status = response.status;
        err.payload = payload;
        throw err;
    }
    return payload;
}

export function configureMirageLobbyHost(callbacks) {
    hostContext = callbacks;
}

export function isMirageLobbyTransitioningToGame() {
    return transitioningToGame;
}

export async function initializeMirageLobbyPage(containerId = 'mirage-lobby-container') {
    mountedContainer = document.getElementById(containerId);
    if (!mountedContainer) return;
    transitioningToGame = false;

    const cached = hostContext?.getMirage8PHandshake?.();

    // Fast path: room was just created or joined from Multiplayer panel
    if (activeRoom && activeRoom.phase === 'Waiting' && activeRoom.roomId) {
        const playerId = activeRoom.callerPlayerId || cached?.playerId;
        const token = activeRoom.callerSeatToken || cached?.token;
        if (playerId && token) {
            syncWaitingHandshake(activeRoom);
            renderWaitingRoomView();
            startWaitingRoomSync(activeRoom.roomId, playerId, token);
            return;
        }
    }

    // Restore path: page reload or reconnect with cached 8P handshake
    if (cached?.kind === 'mirage-8p' && cached.roomId && cached.playerId && cached.token && cached.role !== 'spectator') {
        try {
            const restored = await requestJson(
                `${MIRAGE_API_BASE}/${encodeURIComponent(cached.roomId)}?playerId=${encodeURIComponent(cached.playerId)}&token=${encodeURIComponent(cached.token)}`
            );
            if (restored && restored.phase === 'Waiting') {
                activeRoom = {
                    ...restored,
                    callerPlayerId: restored.callerPlayerId ?? cached.playerId,
                    callerSeatToken: restored.callerSeatToken ?? cached.token
                };
                syncWaitingHandshake(activeRoom);
                renderWaitingRoomView();
                startWaitingRoomSync(restored.roomId, cached.playerId, cached.token);
                return;
            }
            if (restored && restored.phase !== 'Closed' && restored.phase !== 'Finished' && restored.callerSeatGameId && !restored.callerIsEliminated) {
                await enterStartedMirageGame(restored);
                return;
            }
            hostContext?.clearHandshake?.();
        } catch {
            hostContext?.clearHandshake?.();
        }
    }

    activeRoom = null;
    await hostContext?.navigateTo?.('multiplayer');
}

export async function cleanupMirageLobbyPage() {
    stopPolling();
    await stopLobbyHubConnection();
    if (!transitioningToGame && activeRoom && activeRoom.phase === 'Waiting') {
        const hs = hostContext?.getMirage8PHandshake?.();
        if (hs?.kind === 'mirage-8p' && hs.roomId === activeRoom.roomId && hs.playerId && hs.token) {
            try {
                await requestJson(`${MIRAGE_API_BASE}/${encodeURIComponent(hs.roomId)}/leave`, {
                    method: 'POST',
                    body: JSON.stringify({ playerId: hs.playerId, token: hs.token })
                });
            } catch {
                // ignore best-effort leave
            }
            hostContext?.clearHandshake?.();
        }
    }
    activeRoom = null;
    mountedContainer = null;
}

export async function createMirage8PRoomFromMultiplayer({ isPublic = true, recruitTimeoutSeconds = 75 } = {}) {
    const identity = getActorIdentity();
    const created = await requestJson(MIRAGE_API_BASE, {
        method: 'POST',
        body: JSON.stringify({
            hostPlayerId: identity.playerId,
            displayName: identity.displayName,
            avatarUrl: identity.avatarUrl,
            isPublic: isPublic !== false,
            recruitTimeoutSeconds: Number(recruitTimeoutSeconds) || 75,
            quickStartSoloAi: false
        })
    });
    syncWaitingHandshake(created);
    activeRoom = created;
    await hostContext?.navigateTo?.('mirage-lobby');
    return created;
}

export async function joinMirage8PByRoomIdFromExternal(roomId) {
    const identity = getActorIdentity();
    const room = await requestJson(`${MIRAGE_API_BASE}/${encodeURIComponent(String(roomId || '').trim())}/join`, {
        method: 'POST',
        body: JSON.stringify({
            joinCode: '',
            playerId: identity.playerId,
            displayName: identity.displayName,
            avatarUrl: identity.avatarUrl
        })
    });
    syncWaitingHandshake(room);
    activeRoom = room;
    await hostContext?.navigateTo?.('mirage-lobby');
    return room;
}

export async function joinMirage8PByCodeFromExternal(joinCode) {
    const identity = getActorIdentity();
    const room = await requestJson(`${MIRAGE_API_BASE}/join-by-code`, {
        method: 'POST',
        body: JSON.stringify({
            joinCode: String(joinCode || '').trim(),
            playerId: identity.playerId,
            displayName: identity.displayName,
            avatarUrl: identity.avatarUrl
        })
    });
    syncWaitingHandshake(room);
    activeRoom = room;
    await hostContext?.navigateTo?.('mirage-lobby');
}

function syncWaitingHandshake(roomDto) {
    if (!roomDto?.roomId || !roomDto?.callerPlayerId || !roomDto?.callerSeatToken) return;
    hostContext?.setMirage8PHandshake?.({
        kind: 'mirage-8p',
        roomId: roomDto.roomId,
        gameId: roomDto.callerSeatGameId || roomDto.roomId,
        seatIndex: roomDto.callerSeatIndex ?? 0,
        seatGameId: roomDto.callerSeatGameId || null,
        playerId: roomDto.callerPlayerId,
        token: roomDto.callerSeatToken,
        joinCode: roomDto.joinCode || '',
        role: roomDto.callerRole || 'guest',
        isMirage: true,
        mode: 'Mirage',
        viewingPlayerId: roomDto.callerPlayerId
    });
}

async function enterStartedMirageGame(roomDto) {
    const prev = hostContext?.getMirage8PHandshake?.();
    const playerId = roomDto.callerPlayerId || prev?.playerId;
    const token = roomDto.callerSeatToken || prev?.token;
    const seatGameId = roomDto.callerSeatGameId || prev?.seatGameId;
    if (!seatGameId || !playerId || !token) {
        const refreshed = await requestJson(
            `${MIRAGE_API_BASE}/${encodeURIComponent(roomDto.roomId)}?playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`
        );
        return enterStartedMirageGame(refreshed);
    }

    hostContext?.setMirage8PHandshake?.({
        kind: 'mirage-8p',
        roomId: roomDto.roomId,
        gameId: seatGameId,
        seatIndex: roomDto.callerSeatIndex ?? prev?.seatIndex ?? 0,
        seatGameId,
        playerId,
        token,
        joinCode: roomDto.joinCode || prev?.joinCode || '',
        role: roomDto.callerRole || prev?.role || 'player',
        isMirage: true,
        mode: 'Mirage',
        viewingPlayerId: playerId
    });

    transitioningToGame = true;
    stopPolling();
    await stopLobbyHubConnection();
    await hostContext?.navigateTo?.('game');
}

export async function enterSpectateMirageRoom(roomId) {
    hostContext?.setMirage8PHandshake?.({
        kind: 'mirage-8p',
        roomId,
        gameId: roomId,
        seatIndex: null,
        seatGameId: null,
        playerId: null,
        token: null,
        role: 'spectator',
        isMirage: true,
        mode: 'Mirage',
        viewingPlayerId: null
    });
    transitioningToGame = true;
    stopPolling();
    await stopLobbyHubConnection();
    await hostContext?.navigateTo?.('game');
}

function stopPolling() {
    if (pollTimerId) {
        clearInterval(pollTimerId);
        pollTimerId = null;
    }
}

function startBrowserPolling() {
    stopPolling();
    pollTimerId = setInterval(() => {
        if (!activeRoom && mountedContainer) {
            void refreshRoomListQuietly();
        }
    }, 3500);
}

async function stopLobbyHubConnection() {
    const conn = lobbyHubConnection;
    lobbyHubConnection = null;
    if (conn && conn.state !== signalR.HubConnectionState.Disconnected) {
        try {
            await conn.stop();
        } catch {
            // ignore
        }
    }
}

function startWaitingRoomSync(roomId, playerId, token) {
    stopPolling();
    void stopLobbyHubConnection().then(async () => {
        if (!activeRoom || activeRoom.roomId !== roomId) return;
        const url = `/gamehub?mirageRoomId=${encodeURIComponent(roomId)}&lobbyOnly=1&playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`;
        const conn = new signalR.HubConnectionBuilder()
            .withUrl(url)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        conn.on('ReceiveMirageLobbyUpdated', (updatedDto) => {
            if (!activeRoom || updatedDto?.roomId !== roomId) return;
            void handleIncomingRoomUpdate(updatedDto, playerId, token);
        });

        conn.on('ReceiveError', (msg) => {
            const text = String(msg ?? '');
            if (text.startsWith('MIRAGE_ROOM_KICKED:') || text.startsWith('ROOM_CLOSED:')) {
                const detail = text.replace(/^(MIRAGE_ROOM_KICKED:|ROOM_CLOSED:)/, '').trim();
                hostContext?.clearHandshake?.();
                activeRoom = null;
                stopPolling();
                void stopLobbyHubConnection();
                showAlert(detail || '已离开幻境房间。');
                void hostContext?.navigateTo?.('multiplayer');
            }
        });

        try {
            await conn.start();
            lobbyHubConnection = conn;
        } catch {
            // Polling fallback remains active below
        }
    });

    pollTimerId = setInterval(async () => {
        if (!activeRoom || activeRoom.roomId !== roomId || transitioningToGame) return;
        try {
            const latest = await requestJson(
                `${MIRAGE_API_BASE}/${encodeURIComponent(roomId)}?playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`
            );
            await handleIncomingRoomUpdate(latest, playerId, token);
        } catch (err) {
            if (err?.status === 403 || err?.status === 404 || err?.status === 409) {
                hostContext?.clearHandshake?.();
                activeRoom = null;
                stopPolling();
                await stopLobbyHubConnection();
                showAlert(err.message || '房间已失效或已被移出。');
                await hostContext?.navigateTo?.('multiplayer');
            }
        }
    }, 2200);
}

async function handleIncomingRoomUpdate(updatedDto, playerId, token) {
    if (!updatedDto) return;
    if (updatedDto.phase !== 'Waiting' && updatedDto.phase !== 'Closed') {
        const withCreds = updatedDto.callerSeatGameId
            ? updatedDto
            : await requestJson(
                `${MIRAGE_API_BASE}/${encodeURIComponent(updatedDto.roomId)}?playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`
            );
        await enterStartedMirageGame(withCreds);
        return;
    }

    // Preserve caller credentials when receiving public room broadcast
    const merged = {
        ...updatedDto,
        callerPlayerId: updatedDto.callerPlayerId ?? playerId,
        callerSeatToken: updatedDto.callerSeatToken ?? token,
        callerRole: updatedDto.hostPlayerId && String(updatedDto.hostPlayerId).toLowerCase() === String(playerId).toLowerCase()
            ? 'host'
            : 'guest'
    };
    activeRoom = merged;
    syncWaitingHandshake(merged);
    renderMirageLobby();
}

async function refreshRoomListQuietly() {
    try {
        const res = await requestJson(MIRAGE_API_BASE);
        roomList = Array.isArray(res?.items ?? res?.Items) ? (res.items ?? res.Items) : [];
        if (!activeRoom && mountedContainer) {
            renderRoomListOnly();
        }
    } catch {
        // ignore transient polling failure
    }
}

function setStatusMessage(text, isError = false) {
    const el = mountedContainer?.querySelector('#mlobby-status-output');
    if (!el) return;
    el.textContent = text || '';
    el.classList.toggle('is-error', Boolean(isError));
}

function renderRoomListOnly() {
    const listEl = mountedContainer?.querySelector('#mlobby-room-list');
    if (!listEl) return;
    if (!roomList.length) {
        listEl.innerHTML = `
            <div class="mlobby-room-empty">
                <div>当前暂无公开的 8 人幻境房间</div>
                <div>可在右侧创建房间邀请好友，或点击顶部「单人速开（1v7 AI）」立即开战！</div>
            </div>
        `;
        return;
    }

    listEl.innerHTML = roomList.map(r => {
        const roomId = escapeHtml(r.roomId ?? r.RoomId);
        const hostName = escapeHtml(r.hostDisplayName ?? r.HostDisplayName ?? '房主');
        const humanCount = Number(r.humanCount ?? r.HumanCount ?? 1);
        const maxSeats = Number(r.maxSeats ?? r.MaxSeats ?? 8);
        const aliveCount = Number(r.aliveCount ?? r.AliveCount ?? maxSeats);
        const roundNum = Number(r.roundNumber ?? r.RoundNumber ?? 0);
        const timeout = Number(r.recruitTimeoutSeconds ?? r.RecruitTimeoutSeconds ?? 75);
        const canJoin = Boolean(r.canJoin ?? r.CanJoin);
        const canSpectate = Boolean(r.canSpectate ?? r.CanSpectate);
        const statusLabel = escapeHtml(r.statusLabel ?? r.StatusLabel ?? '等待中');

        const badgeCls = canJoin ? 'is-green' : (canSpectate ? 'is-amber' : '');
        const metaLine = canJoin
            ? `已入座 ${humanCount} / ${maxSeats} 人（空位开局由 AI 补齐） · 招募限时 ${timeout}s`
            : `第 ${Math.max(1, roundNum)} 轮 · 存活 ${aliveCount} / ${maxSeats} 人 · 全公开手牌/商店观战`;

        return `
            <div class="mlobby-room-row">
                <div class="mlobby-room-info">
                    <div class="mlobby-room-title">
                        <span>${hostName} 的星界酒馆</span>
                        <span class="mlobby-badge ${badgeCls}">${statusLabel}</span>
                    </div>
                    <div class="mlobby-room-meta">${metaLine}</div>
                </div>
                <div>
                    ${canJoin
                        ? `<button type="button" class="mlobby-btn is-primary" data-mlobby-join-room="${roomId}">加入房间</button>`
                        : canSpectate
                            ? `<button type="button" class="mlobby-btn" data-mlobby-spectate-room="${roomId}">实时观战</button>`
                            : `<button type="button" class="mlobby-btn" disabled>${statusLabel}</button>`}
                </div>
            </div>
        `;
    }).join('');
}

function renderMirageLobby() {
    if (!mountedContainer) return;

    if (activeRoom && activeRoom.phase === 'Waiting') {
        renderWaitingRoomView();
    } else {
        renderBrowserView();
    }
}

function renderBrowserView() {
    mountedContainer.innerHTML = `
        <div class="mlobby-shell">
            <div class="mlobby-topbar">
                <div class="mlobby-brand">
                    <span class="mlobby-kicker">MIRAGE PROTOCOL · 8-PLAYER ARENA</span>
                    <h2 class="mlobby-heading">星界幻境</h2>
                </div>
                <div class="mlobby-top-actions">
                    <button type="button" class="mlobby-btn is-gold" id="mlobby-quick-solo-btn">单人速开 · 1v7 AI</button>
                    <button type="button" class="mlobby-btn" data-route="mirage-card-editor">幻境卡牌工坊</button>
                </div>
            </div>

            <div class="mlobby-browser-grid">
                <section class="mlobby-card">
                    <div class="mlobby-card-header">
                        <h3 class="mlobby-card-title">公开房间与观战列表</h3>
                        <button type="button" class="mlobby-btn" id="mlobby-refresh-list-btn">刷新列表</button>
                    </div>
                    <p class="mlobby-card-sub">支持 1～8 名真人同局竞技，人数不足时房主可随时开局，剩余空位自动由幻境 AI 填补。</p>
                    <div id="mlobby-room-list" class="mlobby-room-list"></div>
                </section>

                <div class="mlobby-side-stack">
                    <section class="mlobby-card">
                        <h3 class="mlobby-card-title" style="margin-bottom:8px;">创建 8 人幻境房间</h3>
                        <p class="mlobby-card-sub">无需构筑卡组，使用独立幻境酒馆共享卡池招募升星、三连合成与自动交战。</p>
                        <div class="mlobby-form-row">
                            <label for="mlobby-timeout-select">每轮招募时长</label>
                            <select id="mlobby-timeout-select" class="mlobby-select">
                                <option value="60">60 秒（快节奏）</option>
                                <option value="75" selected>75 秒（标准推荐）</option>
                                <option value="90">90 秒（充裕思考）</option>
                                <option value="120">120 秒（教学/慢速）</option>
                            </select>
                        </div>
                        <label class="mlobby-checkbox-line">
                            <input type="checkbox" id="mlobby-public-check" checked>
                            <span>公开房间（允许大厅一键加入）</span>
                        </label>
                        <div>
                            <button type="button" class="mlobby-btn is-primary" id="mlobby-create-btn" style="width:100%;">创建 8 人房间</button>
                        </div>
                    </section>

                    <section class="mlobby-card">
                        <h3 class="mlobby-card-title" style="margin-bottom:8px;">凭 6 位邀请码加入</h3>
                        <p class="mlobby-card-sub">输入房主分享的 6 位邀请码，直接进入私密或公开 8 人准备室。</p>
                        <div class="mlobby-form-row">
                            <input type="text" id="mlobby-join-code-input" class="mlobby-input mlobby-code-input" maxlength="6" placeholder="例如 A8K3P9" autocomplete="off">
                        </div>
                        <button type="button" class="mlobby-btn is-primary" id="mlobby-join-code-btn" style="width:100%;">通过邀请码加入</button>
                        <div id="mlobby-status-output" class="mlobby-status-msg" aria-live="polite"></div>
                    </section>
                </div>
            </div>
        </div>
    `;

    renderRoomListOnly();
    bindBrowserEvents();
}

function renderWaitingRoomView() {
    const room = activeRoom;
    const hs = hostContext?.getMirage8PHandshake?.();
    const myPlayerId = String(room.callerPlayerId || hs?.playerId || '').toLowerCase();
    const isHost = String(room.hostPlayerId || '').toLowerCase() === myPlayerId;
    const seats = Array.isArray(room.seats) ? room.seats : [];
    const humanCount = Number(room.humanCount ?? seats.filter(s => s.occupancy === 'Human').length);
    const aiFillCount = Math.max(0, 8 - humanCount);
    const mySeat = seats.find(s => String(s.playerId || '').toLowerCase() === myPlayerId);
    const myReady = Boolean(mySeat?.isReady);
    const canStart = Boolean(room.canHostStart);

    const seatsHtml = seats.map(seat => {
        const isEmpty = seat.occupancy === 'Empty';
        const isSelf = !isEmpty && String(seat.playerId || '').toLowerCase() === myPlayerId;
        const seatHost = Boolean(seat.isHost);
        const seatReady = Boolean(seat.isReady || seatHost);
        const seatIndexPadded = String(seat.seatIndex + 1).padStart(2, '0');
        const seatNumLabel = `SEAT ${seatIndexPadded}`;

        if (isEmpty) {
            return `
                <div class="mlobby-seat-card is-empty">
                    <div class="mlobby-seat-top">
                        <div class="mlobby-seat-avatar is-slot-index">${seatIndexPadded}</div>
                        <div class="mlobby-seat-meta">
                            <div class="mlobby-seat-num">${seatNumLabel} · STANDBY</div>
                            <div class="mlobby-seat-name">等待接入</div>
                        </div>
                    </div>
                    <div class="mlobby-seat-bottom">
                        <span class="mlobby-badge is-muted">AI 替补席位</span>
                    </div>
                </div>
            `;
        }

        const rawAvatarUrl = String(seat.avatarUrl || '').trim();
        const avatarImgHtml = rawAvatarUrl
            ? `<img class="mlobby-seat-avatar-img" src="${escapeHtml(rawAvatarUrl)}" alt="" onerror="this.remove()">`
            : '';
        const canKick = isHost && !seatHost && seat.playerId;

        return `
            <div class="mlobby-seat-card ${isSelf ? 'is-self' : ''} ${seatHost ? 'is-host' : ''}">
                <div class="mlobby-seat-top">
                    <div class="mlobby-seat-avatar">${seatIndexPadded}${avatarImgHtml}</div>
                    <div class="mlobby-seat-meta">
                        <div class="mlobby-seat-num">${seatNumLabel}${isSelf ? ' · YOU' : ''}</div>
                        <div class="mlobby-seat-name">${escapeHtml(seat.displayName || '玩家')}</div>
                    </div>
                </div>
                <div class="mlobby-seat-bottom">
                    ${seatHost
                        ? `<span class="mlobby-badge is-amber">HOST · 已就绪</span>`
                        : seatReady
                            ? `<span class="mlobby-badge is-green">READY · 已准备</span>`
                            : `<span class="mlobby-badge is-amber">WAITING · 未准备</span>`}
                    ${canKick
                        ? `<button type="button" class="mlobby-kick-btn" data-mlobby-kick="${escapeHtml(seat.playerId)}">移出</button>`
                        : ''}
                </div>
            </div>
        `;
    }).join('');

    mountedContainer.innerHTML = `
        <div class="mlobby-shell">
            <div class="mlobby-waiting-banner">
                <div>
                    <div class="mlobby-kicker">${room.isPublic ? 'PUBLIC LOBBY' : 'PRIVATE LOBBY'} · RECRUIT ${room.recruitTimeoutSeconds}S</div>
                    <h2 class="mlobby-heading" style="margin-top:4px;">准备室 <span class="mlobby-heading-count">${humanCount} / 8</span></h2>
                </div>
                <button type="button" class="mlobby-code-box" id="mlobby-copy-code-btn" title="点击复制 6 位邀请码">
                    <span class="mlobby-code-label">JOIN CODE</span>
                    <span class="mlobby-code-val">${escapeHtml(room.joinCode || '------')}</span>
                    <span class="mlobby-code-copy">COPY</span>
                </button>
            </div>

            <section class="mlobby-card">
                <div class="mlobby-seats-grid">
                    ${seatsHtml}
                </div>

                <div class="mlobby-waiting-footer">
                    <div id="mlobby-status-output" class="mlobby-status-msg ${room.startBlockReason ? 'is-error' : ''}">
                        ${escapeHtml(room.startBlockReason || (aiFillCount > 0
                            ? `已就位 ${humanCount} 名玩家；房主可随时开局，其余 ${aiFillCount} 个席位由幻境 AI 自动接管。`
                            : '8 名真人选手已全部就位。'))}
                    </div>
                    <div class="mlobby-footer-actions">
                        ${isHost
                            ? `<button type="button" class="mlobby-btn is-primary" id="mlobby-start-match-btn" ${canStart ? '' : 'disabled'}>
                                   开始对局${aiFillCount > 0 ? `（+${aiFillCount} AI）` : ''}
                               </button>`
                            : `<button type="button" class="mlobby-btn ${myReady ? '' : 'is-primary'}" id="mlobby-toggle-ready-btn" data-next-ready="${!myReady}">
                                   ${myReady ? '取消准备' : '准备就绪'}
                               </button>`}
                        <button type="button" class="mlobby-btn is-danger" id="mlobby-leave-room-btn">
                            ${isHost && humanCount > 1 ? '离开房间' : (isHost ? '解散房间' : '退出房间')}
                        </button>
                    </div>
                </div>
            </section>
        </div>
    `;

    bindWaitingRoomEvents();
}

function bindBrowserEvents() {
    if (!mountedContainer) return;

    mountedContainer.querySelector('#mlobby-refresh-list-btn')?.addEventListener('click', () => {
        void refreshRoomListQuietly();
    });

    mountedContainer.querySelector('#mlobby-quick-solo-btn')?.addEventListener('click', async () => {
        if (isBusy) return;
        isBusy = true;
        try {
            setStatusMessage('正在开启单人 1v7 AI 幻境对局...');
            const identity = getActorIdentity();
            const created = await requestJson(MIRAGE_API_BASE, {
                method: 'POST',
                body: JSON.stringify({
                    hostPlayerId: identity.playerId,
                    displayName: identity.displayName,
                    avatarUrl: identity.avatarUrl,
                    isPublic: false,
                    recruitTimeoutSeconds: 75,
                    quickStartSoloAi: true
                })
            });
            await enterStartedMirageGame(created);
        } catch (err) {
            setStatusMessage(err.message || '开启单人幻境失败。', true);
        } finally {
            isBusy = false;
        }
    });

    mountedContainer.querySelector('#mlobby-create-btn')?.addEventListener('click', async () => {
        if (isBusy) return;
        isBusy = true;
        try {
            const timeoutSec = Number(mountedContainer.querySelector('#mlobby-timeout-select')?.value || 75);
            const isPublic = mountedContainer.querySelector('#mlobby-public-check')?.checked !== false;
            const identity = getActorIdentity();
            setStatusMessage('正在创建 8 人幻境房间...');
            const created = await requestJson(MIRAGE_API_BASE, {
                method: 'POST',
                body: JSON.stringify({
                    hostPlayerId: identity.playerId,
                    displayName: identity.displayName,
                    avatarUrl: identity.avatarUrl,
                    isPublic,
                    recruitTimeoutSeconds: timeoutSec,
                    quickStartSoloAi: false
                })
            });
            activeRoom = created;
            syncWaitingHandshake(created);
            renderMirageLobby();
            startWaitingRoomSync(created.roomId, created.callerPlayerId, created.callerSeatToken);
        } catch (err) {
            setStatusMessage(err.message || '创建房间失败。', true);
        } finally {
            isBusy = false;
        }
    });

    const joinByCodeHandler = async () => {
        if (isBusy) return;
        const codeInput = mountedContainer?.querySelector('#mlobby-join-code-input');
        const code = String(codeInput?.value || '').trim().toUpperCase();
        if (!code) {
            setStatusMessage('请输入 6 位邀请码。', true);
            return;
        }
        isBusy = true;
        try {
            setStatusMessage('正在查询并加入房间...');
            const resolved = await requestJson(`${MIRAGE_API_BASE}/resolve-code/${encodeURIComponent(code)}`);
            if (resolved?.found && resolved.kind && resolved.kind !== 'Mirage8P') {
                if (typeof hostContext?.tryJoinClassicOrTakeTwoByCode === 'function') {
                    const confirmSwitch = await confirmDialog(
                        `邀请码 ${code} 属于${resolved.kind === 'TakeTwo' ? '双选轮抽' : '经典构筑'}房间，是否前往多人对战面板加入？`
                    );
                    if (confirmSwitch) {
                        await hostContext.tryJoinClassicOrTakeTwoByCode(code);
                    }
                    return;
                }
            }

            const identity = getActorIdentity();
            const joined = await requestJson(`${MIRAGE_API_BASE}/join-by-code`, {
                method: 'POST',
                body: JSON.stringify({
                    joinCode: code,
                    playerId: identity.playerId,
                    displayName: identity.displayName,
                    avatarUrl: identity.avatarUrl
                })
            });
            activeRoom = joined;
            syncWaitingHandshake(joined);
            renderMirageLobby();
            startWaitingRoomSync(joined.roomId, joined.callerPlayerId, joined.callerSeatToken);
        } catch (err) {
            setStatusMessage(err.message || '加入房间失败。', true);
        } finally {
            isBusy = false;
        }
    };

    mountedContainer.querySelector('#mlobby-join-code-btn')?.addEventListener('click', joinByCodeHandler);
    mountedContainer.querySelector('#mlobby-join-code-input')?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            void joinByCodeHandler();
        }
    });

    mountedContainer.querySelector('#mlobby-room-list')?.addEventListener('click', async (e) => {
        const joinBtn = e.target?.closest?.('[data-mlobby-join-room]');
        if (joinBtn && !isBusy) {
            const roomId = joinBtn.dataset.mlobbyJoinRoom;
            isBusy = true;
            try {
                const identity = getActorIdentity();
                const joined = await requestJson(`${MIRAGE_API_BASE}/${encodeURIComponent(roomId)}/join`, {
                    method: 'POST',
                    body: JSON.stringify({
                        joinCode: '',
                        playerId: identity.playerId,
                        displayName: identity.displayName,
                        avatarUrl: identity.avatarUrl
                    })
                });
                activeRoom = joined;
                syncWaitingHandshake(joined);
                renderMirageLobby();
                startWaitingRoomSync(joined.roomId, joined.callerPlayerId, joined.callerSeatToken);
            } catch (err) {
                showAlert(err.message || '加入房间失败。');
                void refreshRoomListQuietly();
            } finally {
                isBusy = false;
            }
            return;
        }

        const specBtn = e.target?.closest?.('[data-mlobby-spectate-room]');
        if (specBtn && !isBusy) {
            const roomId = specBtn.dataset.mlobbySpectateRoom;
            await enterSpectateMirageRoom(roomId);
        }
    });
}

function bindWaitingRoomEvents() {
    if (!mountedContainer || !activeRoom) return;
    const hs = hostContext?.getMirage8PHandshake?.();
    const playerId = activeRoom.callerPlayerId || hs?.playerId;
    const token = activeRoom.callerSeatToken || hs?.token;
    const roomId = activeRoom.roomId;

    mountedContainer.querySelector('#mlobby-copy-code-btn')?.addEventListener('click', async () => {
        const code = activeRoom?.joinCode || '';
        if (!code) return;
        try {
            await navigator.clipboard.writeText(code);
            setStatusMessage(`邀请码 ${code} 已复制到剪贴板！`);
        } catch {
            showAlert(`房间邀请码：${code}`);
        }
    });

    mountedContainer.querySelector('#mlobby-toggle-ready-btn')?.addEventListener('click', async (e) => {
        if (isBusy) return;
        isBusy = true;
        const nextReady = e.currentTarget.dataset.nextReady === 'true';
        try {
            const updated = await requestJson(`${MIRAGE_API_BASE}/${encodeURIComponent(roomId)}/ready`, {
                method: 'POST',
                body: JSON.stringify({ playerId, token, ready: nextReady })
            });
            activeRoom = updated;
            syncWaitingHandshake(updated);
            renderMirageLobby();
        } catch (err) {
            setStatusMessage(err.message || '切换准备状态失败。', true);
        } finally {
            isBusy = false;
        }
    });

    mountedContainer.querySelector('#mlobby-start-match-btn')?.addEventListener('click', async () => {
        if (isBusy) return;
        isBusy = true;
        try {
            setStatusMessage('正在补齐 AI 席位并初始化 8 人星界酒馆...');
            const started = await requestJson(`${MIRAGE_API_BASE}/${encodeURIComponent(roomId)}/start`, {
                method: 'POST',
                body: JSON.stringify({ playerId, token })
            });
            await enterStartedMirageGame(started);
        } catch (err) {
            setStatusMessage(err.message || '开局失败。', true);
        } finally {
            isBusy = false;
        }
    });

    mountedContainer.querySelector('#mlobby-leave-room-btn')?.addEventListener('click', async () => {
        if (isBusy) return;
        isBusy = true;
        try {
            stopPolling();
            await stopLobbyHubConnection();
            await requestJson(`${MIRAGE_API_BASE}/${encodeURIComponent(roomId)}/leave`, {
                method: 'POST',
                body: JSON.stringify({ playerId, token })
            });
        } catch {
            // ignore if already closed
        } finally {
            hostContext?.clearHandshake?.();
            activeRoom = null;
            isBusy = false;
            await hostContext?.navigateTo?.('multiplayer');
        }
    });

    mountedContainer.querySelectorAll('[data-mlobby-kick]').forEach(btn => {
        btn.addEventListener('click', async () => {
            if (isBusy) return;
            const targetPlayerId = btn.dataset.mlobbyKick;
            if (!targetPlayerId) return;
            isBusy = true;
            try {
                const updated = await requestJson(`${MIRAGE_API_BASE}/${encodeURIComponent(roomId)}/kick`, {
                    method: 'POST',
                    body: JSON.stringify({ playerId, token, targetPlayerId })
                });
                activeRoom = updated;
                syncWaitingHandshake(updated);
                renderMirageLobby();
            } catch (err) {
                setStatusMessage(err.message || '移出玩家失败。', true);
            } finally {
                isBusy = false;
            }
        });
    });
}
