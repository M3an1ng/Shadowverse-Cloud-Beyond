
import { create } from 'zustand';
import { v4 as uuidv4 } from 'uuid';
import { GameState, PlayerState, RuntimeCard, CardModel, CardType, ClassType } from './types';
import { INITIAL_PP, MAX_BOARD_SIZE, MAX_HAND_SIZE, MAX_PP_LIMIT, TEST_DECK_IDS } from './constants';
import { executeEffectString } from './logic/actionEngine';

// --- Store Definition ---

const createInitialPlayer = (id: 'player' | 'opponent'): PlayerState => ({
    playerId: id,
    hp: 20,
    maxHp: 20,
    pp: INITIAL_PP,
    maxPp: INITIAL_PP,
    ep: id === 'player' ? 2 : 3,
    shadows: 0,
    rally: 0,
    cardsPlayedThisTurn: 0,
    hand: [],
    board: Array(MAX_BOARD_SIZE).fill(null),
    deck: [],
    deckCount: 0,
    class: ClassType.NEUTRAL
});

export interface GameStore extends GameState {
    cardMap: Record<string, CardModel>; // Dynamic DB
    fullLogs: any[];

    // Actions
    loadGameData: (data: Record<string, CardModel>) => void;
    initGame: () => void;
    endTurn: () => void;
    playCard: (cardIndex: number) => void;
    attack: (attackerIndex: number, targetIndex: number | 'leader') => void;
    evolve: (cardIndex: number) => void;
    addLog: (msg: string, type?: 'info' | 'combat' | 'turn' | 'error') => void;
    downloadLogs: () => void;

    // Engine Actions (Low Level)
    drawCard: (playerId: 'player' | 'opponent', count: number) => void;
    damageLeader: (playerId: 'player' | 'opponent', amount: number) => void;
    healLeader: (playerId: 'player' | 'opponent', amount: number) => void;
    damageFollower: (playerId: 'player' | 'opponent', slotIndex: number, amount: number) => void;
    summonToken: (playerId: 'player' | 'opponent', cardId: string) => void;
    modifyPP: (playerId: 'player' | 'opponent', amount: number) => void;
    modifyMaxPP: (playerId: 'player' | 'opponent', amount: number) => void;
    incrementShadows: (playerId: 'player' | 'opponent', amount: number) => void;

    // Debug
    debugSetPlayerState: (playerId: 'player' | 'opponent', updates: Partial<PlayerState>) => void;
}

export const useGameStore = create<GameStore>((set, get) => ({
    cardMap: {}, // Starts empty, filled by loader or constants
    turnCount: 1,
    currentTurn: 'player',
    player: createInitialPlayer('player'),
    opponent: createInitialPlayer('opponent'),
    winner: null,
    logs: [],
    fullLogs: [], // Persistent full history

    loadGameData: (data) => {
        set({ cardMap: data });
        get().addLog(`SYS: DB_LOADED [${Object.keys(data).length} ENTRIES]`, 'info');
        get().initGame(); // Restart game with new data
    },

    initGame: () => {
        const { cardMap } = get();
        const player = createInitialPlayer('player');
        const opponent = createInitialPlayer('opponent');

        // 1. Validate Test Deck IDs against loaded map
        const validTestDeck = TEST_DECK_IDS.filter(id => cardMap[id]);

        // 2. Construct Deck (Repeat test deck to fill 40 cards if valid, else random)
        let deckIds: string[] = [];

        if (validTestDeck.length > 0) {
            // Repeat test deck to reach ~40 cards (Standard Shadowverse deck size)
            while (deckIds.length < 40) {
                deckIds = [...deckIds, ...validTestDeck];
            }
            deckIds = deckIds.slice(0, 40);
        } else {
            // Fallback to random if no test deck or map empty
            const availableIds = Object.keys(cardMap);
            if (availableIds.length > 0) {
                deckIds = Array.from({ length: 40 }, () => availableIds[Math.floor(Math.random() * availableIds.length)]);
            }
        }

        // Ensure deck has at least 40 cards if possible (Double check for safety)
        if (deckIds.length < 40 && validTestDeck.length > 0) {
            while (deckIds.length < 40) {
                deckIds = [...deckIds, ...validTestDeck];
            }
            deckIds = deckIds.slice(0, 40);
        }

        player.deck = [...deckIds];
        player.deckCount = deckIds.length;

        // Opponent gets same deck for testing balance
        opponent.deck = [...deckIds];
        opponent.deckCount = deckIds.length;

        set({
            turnCount: 1,
            currentTurn: 'player',
            winner: null,
            player,
            opponent,
            logs: [{ id: uuidv4(), message: `SYS_INIT: DECK_SIZE=${deckIds.length}`, type: 'info' }]
        });
        get().addLog(`Init Deck Size: ${deckIds.length} (Valid Test IDs: ${validTestDeck.length})`, 'info');

        // Initial Draw
        get().drawCard('player', 3);
        get().drawCard('opponent', 3);

        // Start Turn 1
        set(state => ({
            player: { ...state.player, pp: 1, maxPp: 1 }
        }));
    },

    addLog: (message, type = 'info') => {
        const entry = { id: uuidv4(), message, type, timestamp: Date.now() };

        // Console Output for Web Debugging
        const logPrefix = `[GAME] [${type.toUpperCase()}]`;
        if (type === 'error') {
            console.error(logPrefix, message);
        } else if (type === 'combat') {
            console.log(`%c${logPrefix} ${message}`, 'color: orange; font-weight: bold;');
        } else {
            console.log(logPrefix, message);
        }

        set((state) => ({
            logs: [entry, ...state.logs].slice(0, 50),
            fullLogs: [...state.fullLogs, entry]
        }));
    },

    downloadLogs: () => {
        const { fullLogs } = get();
        const header = "Timestamp,Type,Message\n";
        const rows = fullLogs.map(log => {
            const time = new Date(log.timestamp || Date.now()).toISOString();
            // Escape quotes in message
            const msg = `"${log.message.replace(/"/g, '""')}"`;
            return `${time},${log.type},${msg}`;
        }).join("\n");

        const csvContent = header + rows;
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.setAttribute("href", url);
        link.setAttribute("download", `game_logs_${new Date().toISOString().replace(/[:.]/g, '-')}.csv`);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },

    debugSetPlayerState: (playerId, updates) => {
        set(state => {
            const update: Partial<GameStore> = {};
            update[playerId] = { ...state[playerId], ...updates };
            return update;
        });
    },

    // --- Engine Actions ---

    drawCard: (playerId, count) => {
        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const newHand = [...p.hand];
            const newDeck = [...p.deck];

            for (let i = 0; i < count; i++) {
                if (newHand.length >= MAX_HAND_SIZE) {
                    get().addLog(`BURN: Hand Full`, 'error');
                    newDeck.shift();
                    continue;
                }
                const drawId = newDeck.shift();
                if (drawId && state.cardMap[drawId]) {
                    const model = state.cardMap[drawId];
                    const runtimeCard: RuntimeCard = {
                        ...model,
                        instanceId: uuidv4(),
                        currentAttack: model.attack,
                        currentDefense: model.defense,
                        currentCost: model.cost,
                        isEvolved: false,
                        canAttack: false,
                        hasStorm: model.effects.some(e => e.includes('Storm')),
                        hasRush: model.effects.some(e => e.includes('Rush')),
                        hasWard: model.effects.some(e => e.includes('Ward')),
                        isEngaged: false
                    };
                    newHand.push(runtimeCard);
                }
            }

            // Explicit return to satisfy partial type
            const update: Partial<GameStore> = {};
            update[playerId] = { ...p, hand: newHand, deck: newDeck, deckCount: newDeck.length };
            return update;
        });
    },

    damageLeader: (playerId, amount) => {
        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const newHp = p.hp - amount;

            const update: Partial<GameStore> = {};
            update[playerId] = { ...p, hp: newHp };

            if (newHp <= 0) {
                update.winner = playerId === 'player' ? 'opponent' : 'player';
            }
            return update;
        });
        get().addLog(`DMG: ${playerId.toUpperCase()} Leader -${amount}`, 'combat');
    },

    healLeader: (playerId, amount) => {
        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const newHp = Math.min(p.hp + amount, p.maxHp);
            const update: Partial<GameStore> = {};
            update[playerId] = { ...p, hp: newHp };
            return update;
        });
        get().addLog(`HEAL: ${playerId.toUpperCase()} Leader +${amount}`, 'info');
    },

    damageFollower: (playerId, slotIndex, amount) => {
        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const board = [...p.board];
            const card = board[slotIndex];

            if (card) {
                card.currentDefense -= amount;
                if (card.currentDefense <= 0) {
                    board[slotIndex] = null;
                    p.shadows++;
                    get().addLog(`DESTROY: ${card.name}`, 'combat');
                }
            }
            const update: Partial<GameStore> = {};
            update[playerId] = { ...p, board };
            return update;
        });
    },

    summonToken: (playerId, cardId) => {
        const { cardMap } = get();
        const model = cardMap[cardId];
        if (!model) {
            console.warn(`Cannot summon unknown token: ${cardId}`);
            return;
        }

        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const board = [...p.board];
            const emptyIdx = board.findIndex(c => c === null);

            if (emptyIdx === -1) {
                get().addLog("WARN: Board Full, Token Banished", 'error');
                return {};
            }

            const token: RuntimeCard = {
                ...model,
                instanceId: uuidv4(),
                currentAttack: model.attack,
                currentDefense: model.defense,
                currentCost: model.cost,
                isEvolved: false,
                canAttack: model.effects.some(e => e.includes('Storm')) || model.effects.some(e => e.includes('Rush')),
                hasStorm: model.effects.some(e => e.includes('Storm')),
                hasRush: model.effects.some(e => e.includes('Rush')),
                hasWard: model.effects.some(e => e.includes('Ward')),
                isEngaged: false
            };

            board[emptyIdx] = token;

            const update: Partial<GameStore> = {};
            // Rally check
            const newRally = model.type === CardType.FOLLOWER ? p.rally + 1 : p.rally;
            update[playerId] = { ...p, board, rally: newRally };
            return update;
        });
        get().addLog(`SUMMON: ${model.name}`, 'info');
    },

    modifyPP: (playerId, amount) => {
        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const update: Partial<GameStore> = {};
            update[playerId] = { ...p, pp: Math.min(p.pp + amount, p.maxPp) };
            return update;
        });
    },

    modifyMaxPP: (playerId, amount) => {
        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const oldMax = p.maxPp;
            const newMax = Math.min(p.maxPp + amount, MAX_PP_LIMIT);

            // Also recover PP if gaining empty orb (Shadowverse standard behavior for "Gain empty play point orb")
            // If the card says "Gain empty play point orb", it usually implies MaxPP+1.
            // If it says "Gain play point orb", it implies MaxPP+1 AND PP+1.
            // For now, we stick to strict interpretation: MaxPP only affects MaxPP.
            // But we should log it.

            const update: Partial<GameStore> = {};
            update[playerId] = { ...p, maxPp: newMax };

            console.log(`[STORE] modifyMaxPP: ${playerId} ${oldMax} -> ${newMax} (Amount: ${amount})`);

            return update;
        });
    },

    incrementShadows: (playerId, amount) => {
        set(state => {
            const p = playerId === 'player' ? state.player : state.opponent;
            const update: Partial<GameStore> = {};
            update[playerId] = { ...p, shadows: p.shadows + amount };
            return update;
        });
    },

    // --- Gameplay Actions ---

    playCard: (cardIndex) => {
        const { currentTurn, player, opponent, addLog } = get();
        const activePlayer = currentTurn === 'player' ? player : opponent;
        const card = activePlayer.hand[cardIndex];

        if (!card) return;
        if (activePlayer.pp < card.currentCost) {
            addLog("ERR: INSUFFICIENT_PP", 'error');
            return;
        }

        if (card.type !== CardType.SPELL) {
            // Follower / Amulet
            const emptySlotIndex = activePlayer.board.findIndex(slot => slot === null);
            if (emptySlotIndex === -1) {
                addLog("ERR: BOARD_FULL", 'error');
                return;
            }

            // Pay Cost
            const newPp = activePlayer.pp - card.currentCost;

            const newHand = [...activePlayer.hand];
            newHand.splice(cardIndex, 1);

            const newBoard = [...activePlayer.board];
            const boardCard = { ...card, canAttack: card.hasStorm || card.hasRush };
            newBoard[emptySlotIndex] = boardCard;

            // Combo & Rally Update
            const newCombo = activePlayer.cardsPlayedThisTurn + 1;
            const newRally = card.type === CardType.FOLLOWER ? activePlayer.rally + 1 : activePlayer.rally;

            const update: Partial<PlayerState> = {
                pp: newPp,
                hand: newHand,
                board: newBoard,
                cardsPlayedThisTurn: newCombo,
                rally: newRally
            };

            if (currentTurn === 'player') {
                set({ player: { ...player, ...update } });
            } else {
                set({ opponent: { ...opponent, ...update } });
            }

            addLog(`PLAY: [${card.name}]`, 'combat');

            // EXECUTE FANFARE
            const fanfareLines = card.effects.filter(e => e.startsWith('Fanfare'));
            fanfareLines.forEach(line => {
                executeEffectString(line.replace('Fanfare||', ''), boardCard, get());
            });

        } else {
            // Spell Logic
            const newPp = activePlayer.pp - card.currentCost;
            const newHand = [...activePlayer.hand];
            newHand.splice(cardIndex, 1);

            const newShadows = activePlayer.shadows + 1;
            const newCombo = activePlayer.cardsPlayedThisTurn + 1;

            const update: Partial<PlayerState> = {
                pp: newPp,
                hand: newHand,
                shadows: newShadows,
                cardsPlayedThisTurn: newCombo
            };

            if (currentTurn === 'player') {
                set({ player: { ...player, ...update } });
            } else {
                set({ opponent: { ...opponent, ...update } });
            }
            addLog(`CAST: [${card.name}]`, 'combat');

            // EXECUTE SPELL EFFECTS
            const playLines = card.effects.filter(e => e.startsWith('Play'));
            playLines.forEach(line => {
                executeEffectString(line.replace('Play||', ''), card, get());
            });
        }
    },

    attack: (attackerIndex, targetIndex) => {
        const { currentTurn, player, opponent, addLog } = get();
        const attackerState = currentTurn === 'player' ? player : opponent;
        const defenderState = currentTurn === 'player' ? opponent : player;

        const attacker = attackerState.board[attackerIndex];
        if (!attacker || !attacker.canAttack) return;

        if (targetIndex === 'leader') {
            // Ward check
            if (defenderState.board.some(c => c && c.hasWard)) {
                addLog("ERR: WARD_BLOCK", 'error');
                return;
            }
            get().damageLeader(defenderState.playerId, attacker.currentAttack);
            attacker.canAttack = false;
        } else if (typeof targetIndex === 'number') {
            const defender = defenderState.board[targetIndex];
            if (!defender) return;

            // Ward check
            if (defenderState.board.some(c => c && c.hasWard) && !defender.hasWard) {
                addLog("ERR: WARD_BLOCK", 'error');
                return;
            }

            // Combat
            get().damageFollower(defenderState.playerId, targetIndex, attacker.currentAttack);
            get().damageFollower(attackerState.playerId, attackerIndex, defender.currentAttack);

            attacker.canAttack = false;
            addLog(`CLASH: ${attacker.name} -> ${defender.name}`, 'combat');
        }
    },

    evolve: (cardIndex) => {
        const { currentTurn, player, opponent, addLog } = get();
        const activePlayer = currentTurn === 'player' ? player : opponent;
        const card = activePlayer.board[cardIndex];

        if (!card || card.isEvolved || activePlayer.ep <= 0) return;

        card.isEvolved = true;
        card.currentAttack += 2;
        card.currentDefense += 2;
        card.hasRush = true;
        card.canAttack = true;

        const newEp = activePlayer.ep - 1;

        if (currentTurn === 'player') {
            set({ player: { ...player, ep: newEp } });
        } else {
            set({ opponent: { ...opponent, ep: newEp } });
        }
        addLog(`EVOLVE: ${card.name}`, 'info');

        // Execute Evolve Effects
        const evolveLines = card.effects.filter(e => e.startsWith('Evolve'));
        evolveLines.forEach(line => {
            executeEffectString(line.replace('Evolve||', ''), card, get());
        });
    },

    endTurn: () => {
        const { currentTurn, player, opponent, turnCount, addLog } = get();

        const nextTurn = currentTurn === 'player' ? 'opponent' : 'player';
        const nextPlayerState = nextTurn === 'player' ? player : opponent;
        const nextMaxPp = Math.min(nextPlayerState.maxPp + 1, MAX_PP_LIMIT);

        // Reset turn-based counters for the ending player
        // (Not strictly necessary if we only check them during that player's turn, but good for cleanup)

        // Update Next Player (Start Turn)
        const updatedNextPlayer = {
            ...nextPlayerState,
            maxPp: nextMaxPp,
            pp: nextMaxPp,
            cardsPlayedThisTurn: 0, // Reset Combo
            board: nextPlayerState.board.map(c => c ? ({ ...c, canAttack: true }) : null)
        };

        // Create clean update object
        const update: Partial<GameStore> = {};

        if (nextTurn === 'player') {
            update.currentTurn = 'player';
            update.turnCount = turnCount + 1;
            update.player = updatedNextPlayer;
        } else {
            update.currentTurn = 'opponent';
            update.opponent = updatedNextPlayer;
        }

        set(update);

        // Draw for next player
        get().drawCard(nextTurn, 1);

        addLog(`TURN: ${nextTurn.toUpperCase()}`, 'turn');
    }
}));
