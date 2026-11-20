const zones = ['hand', 'deck', 'field', 'graveyard', 'extra', 'emblem'];
const labels = {
    MulliganSubmitted: '确认换牌', TurnStart: '回合开始', TurnEnd: '回合结束', PlayCard: '使用卡牌',
    Summon: '召唤', Attack: '攻击', Damage: '造成伤害', Heal: '回复生命', Destroy: '破坏', Vanish: '消失',
    Evolve: '进化', SuperEvolve: '超进化', Draw: '抽牌', Fusion: '融合', Act: '启动', Transform: '变身',
    Discard: '舍弃', Return: '返回手牌', GameEnd: '对局结束', PerformPpBoost: '使用额外能量点',
    CardFused: '融合', CardRevealed: '展示卡牌', Surrender: '认输', waiting_for_choice: '等待选择', ended: '对局结束', initial: '对局开始'
};

// Keep one root per step. Only branches touched by a recorded change get copied.
function applyChange(previous, change, depth = 0) {
    const key = change.path[depth];
    const copy = { ...previous };
    if (depth + 1 < change.path.length) {
        if (!Object.hasOwn(previous, key)) throw new Error('回放中的战场数据不完整。');
        Object.defineProperty(copy, key, {
            value: applyChange(previous[key], change, depth + 1), enumerable: true, writable: true, configurable: true
        });
    } else if (change.operation === 'remove') {
        delete copy[key];
    } else if (change.operation === 'set') {
        Object.defineProperty(copy, key, { value: change.value, enumerable: true, writable: true, configurable: true });
    } else {
        throw new Error('无法识别这局回放的数据格式。');
    }
    return copy;
}

function boardFromDocument(doc) {
    function card(id) {
        const original = doc.cards[id];
        if (!original) throw new Error('回放缺少卡片数据。');
        return { ...original, fusedCards: (doc.fusions[id] || []).map(card) };
    }
    const players = doc.playerOrder.map(id => {
        const areas = doc.zones[id];
        const player = { ...doc.players[id], leaderCard: areas.leaderCard ? card(areas.leaderCard) : null };
        for (const zone of zones) player[zone] = areas[zone].map(card);
        return player;
    });
    return { ...doc.game, players };
}

export function createReplayTimeline(data) {
    const states = [];
    const steps = [];
    const turns = [];
    const playedCards = new Map();
    const legacy = Array.isArray(data.frames);
    function linkPlayedCard(effect) {
        const p = effect.payload;
        if (effect.method === 'CardPlayed') {
            const uid = p.playedCardInstance?.instanceId ?? p.playedCardInstance?.InstanceId;
            if (uid) playedCards.set(uid, p);
        } else if (effect.method === 'CardMoved' && playedCards.has(p.cardUid)) {
            const cast = playedCards.get(p.cardUid);
            const zone = String(p.toZone ?? '').toLowerCase();
            const type = cast.playedCardInstance.type ?? cast.playedCardInstance.Type;
            if (zone === 'field' || (zone === 'playing' && String(type).toLowerCase() === 'spell')) {
                playedCards.delete(p.cardUid);
                return { ...effect, cast };
            }
            if (zone !== 'playing' && zone !== 'staging') playedCards.delete(p.cardUid);
        }
        return effect;
    }
    function add(state, step, players, owner, startsTurn) {
        const active = players.find(player => player.playerId === owner);
        const turn = active?.turnNumber ?? 0;
        const playerName = active?.displayName && active.displayName !== 'PLAYER' ? active.displayName : '';
        const title = turn > 0 ? `第 ${turn} 回合 · ${active?.isFirst ? '先手' : '后手'}${playerName ? ' ' + playerName : ''}` : '起手调度';
        if (!steps.length || startsTurn) turns.push({ index: steps.length, title });
        const name = state.cards?.[step.cardId]?.name || '';
        const label = labels[step.kind] || '操作结算';
        states.push(state);
        steps.push({ ...step, effects: (step.effects ?? []).map(linkPlayedCard),
            label: name ? `${label} · ${name}` : label, turnIndex: turns.length - 1 });
    }
    if (legacy) {
        let previousTurn;
        for (const { state, boundary, effects } of data.frames) {
            const turn = state.players.reduce((sum, p) => sum + p.turnNumber, 0);
            add(state, { kind: boundary, effects }, state.players, state.currentPlayerId, previousTurn !== turn);
            previousTurn = turn;
        }
    } else {
        let state = data.opening;
        add(state, { kind: 'initial' }, Object.values(state.players), state.game.currentPlayerId, false);
        for (const { changes, ...step } of data.steps) {
            for (const change of changes) state = applyChange(state, change);
            add(state, step, Object.values(state.players), step.owner, step.kind === 'TurnStart');
        }
    }
    if (steps.length === 0) throw new Error('这局没有可播放的记录。');
    return { steps, turns, boardAt: index => legacy ? states[index] : boardFromDocument(states[index]) };
}
