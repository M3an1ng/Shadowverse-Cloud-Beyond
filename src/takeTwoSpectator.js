import { compareSpectatorRooms } from './matchRoomBrowser.js';
import { historyRequest } from './cardHistory.js';
import { catalogCards } from './cardGallery.js';
import { cardId, classes, createFeatureModal } from './cardStyles.js';
import { draftCurve, professionEmblem, showDraftDeckPreview } from './miniDraftPresentation.js';
import './styles/take-two-spectator.css';

const api = (path, options) => historyRequest(`/api/take-two${path}`, options);
const key = id => String(id || '').replaceAll('-', '').toLowerCase();
const environmentLabel = value => ({ rotation: '指定', unlimited: '无限', open: '开放', as2p: 'AS2P' }[value] || '开放');

export function mergeTakeTwoRooms(nativeRooms, drafts) {
  const result = nativeRooms.map(room => ({ ...room }));
  for (const draft of drafts) {
    if (draft.closed) continue;
    const native = draft.matchRoomId && result.find(room => key(room.roomId) === key(draft.matchRoomId));
    if (native) {
      native.isTakeTwo = true;
      native.draftEnvironment = draft.environment;
      continue;
    }
    // A started match must use the native spectator flow, not a duplicate draft row.
    if (draft.matchRoomId) continue;
    result.push({
      roomId: draft.roomId,
      draftRoomId: draft.roomId,
      isTakeTwo: true,
      draftEnvironment: draft.environment,
      status: draft.status,
      statusLabel: draft.status === 'Waiting' ? '等待中' : '选牌中',
      hasGuest: draft.players.length === 2,
      canSpectate: draft.status !== 'Waiting' && draft.players.length === 2,
    });
  }
  return result.sort(compareSpectatorRooms);
}

export async function listSpectatableRooms(listMatches, normalize) {
  const [native, drafts] = await Promise.all([listMatches(), api('/rooms')]);
  return mergeTakeTwoRooms(normalize(native), drafts);
}

export async function findTakeTwoSpectatorRoom(id) {
  const drafts = await api('/rooms');
  return drafts.find(room => key(room.roomId) === key(id)) || null;
}

export async function showTakeTwoSpectator(roomId, { onMatch = () => {} } = {}) {
  const modal = createFeatureModal('双选观战', { wide: true });
  modal.overlay.classList.add('take-two-spectator');
  const status = document.createElement('p');
  status.className = 'draft-spectator-status';
  status.textContent = '正在读取房间状态…';
  const content = document.createElement('div');
  content.className = 'draft-spectator-players';
  modal.body.append(status, content);
  let lastSnapshot = '', timer;
  const abort = new AbortController();
  const observer = new MutationObserver(() => {
    if (!modal.overlay.isConnected) {
      clearTimeout(timer);
      abort.abort();
      observer.disconnect();
    }
  });
  observer.observe(document.body, { childList: true });
  try {
    const cards = await catalogCards();
    if (!modal.overlay.isConnected) return;
    const byId = new Map(cards.map(card => [cardId(card), card]));
    const refresh = async () => {
      if (!modal.overlay.isConnected) return;
      try {
        const room = await api(`/rooms/${encodeURIComponent(roomId)}/spectate`, { signal: abort.signal });
        if (!modal.overlay.isConnected) return;
        if (room.closed) { status.textContent = '房间已结束'; return; }
        if (room.status === 'Waiting' || room.players.length < 2) { status.textContent = '等待对手加入时无法观战'; content.replaceChildren(); return; }
        if (room.matchRoomId) {
          modal.close();
          onMatch(room.matchRoomId);
          return;
        }
        status.textContent = `${environmentLabel(room.environment)} | 双选对战 · ${room.status === 'Waiting' ? '等待对手加入' : '双方正在选牌'}`;
        const snapshot = JSON.stringify([room.environment, room.players]);
        if (snapshot !== lastSnapshot) {
          lastSnapshot = snapshot;
          const panels = room.players.map((player, index) => {
            const panel = document.createElement('section');
            panel.className = 'draft-spectator-player';
            const heading = document.createElement('h3');
            const profession = classes.find(([id]) => id === player.class)?.[1] || '尚未选择职业';
            const progress = player.ready ? '已准备' : player.round > 14 ? '选牌完成' : player.round ? `第 ${player.round} / 14 轮` : '选择职业';
            heading.textContent = player.username || '玩家';
            const round = document.createElement('div');
            round.className = 'draft-spectator-round';
            round.textContent = progress;
            const job = document.createElement('div');
            job.className = 'draft-spectator-profession';
            if (player.class) job.append(professionEmblem(player.class));
            job.append(document.createTextNode(profession));
            const preview = document.createElement('button');
            preview.type = 'button';
            preview.textContent = `查看当前牌组 · ${player.cards.length} / 30`;
            preview.onclick = () => showDraftDeckPreview(player.cards, byId, { profession: player.class, environment: room.environment });
            panel.append(heading, round, job, draftCurve(player.cards, byId), preview);
            return panel;
          });
          if (room.players.length < 2) {
            const waiting = document.createElement('p');
            waiting.className = 'draft-spectator-player';
            waiting.textContent = '等待另一位玩家加入';
            panels.push(waiting);
          }
          content.replaceChildren(...panels);
        }
      } catch (error) {
        if (!modal.overlay.isConnected) return;
        status.textContent = `暂时无法刷新房间：${error.message || error}`;
      }
      if (modal.overlay.isConnected) timer = setTimeout(refresh, 1200);
    };
    await refresh();
  } catch (error) {
    if (modal.overlay.isConnected) status.textContent = `载入卡库失败：${error.message || error}`;
  }
  return modal;
}
