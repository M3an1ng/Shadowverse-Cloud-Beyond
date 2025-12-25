
import { create } from 'zustand';
import { v4 as uuidv4 } from 'uuid';
import { GameState, PlayerState, RuntimeCard, CardModel, CardType, ClassType } from './types';
import { ALL_CARDS_MAP, INITIAL_PP, MAX_BOARD_SIZE, MAX_HAND_SIZE, MAX_PP_LIMIT, TEST_DECK_IDS } from './constants';

// --- Parser Helpers (Simplified for UI Demo) ---
// In a full implementation, this would be the massive DSL parser from the VBA code.
const parseKeywords = (card: RuntimeCard) => {
  const combinedEffects = card.effects.join(' ');
  if (combinedEffects.includes('Storm')) card.hasStorm = true;
  if (combinedEffects.includes('Rush')) card.hasRush = true;
  if (combinedEffects.includes('Ward')) card.hasWard = true;
  
  // Initial Playable Logic
  if (card.hasStorm || card.hasRush) card.canAttack = true;
};

const createRuntimeCard = (cardId: string): RuntimeCard => {
  const model = ALL_CARDS_MAP[cardId];
  if (!model) throw new Error(`Card ID ${cardId} not found`);

  const card: RuntimeCard = {
    ...model,
    instanceId: uuidv4(),
    currentAttack: model.attack,
    currentDefense: model.defense,
    currentCost: model.cost,
    isEvolved: false,
    canAttack: false,
    hasStorm: false,
    hasRush: false,
    hasWard: false,
    isEngaged: false
  };
  
  parseKeywords(card);
  return card;
};

const createInitialPlayer = (id: 'player' | 'opponent'): PlayerState => ({
  playerId: id,
  hp: 20,
  maxHp: 20,
  pp: INITIAL_PP,
  maxPp: INITIAL_PP,
  ep: id === 'player' ? 2 : 3, // 2nd player gets 3 EP usually
  shadows: 0,
  hand: [],
  board: Array(MAX_BOARD_SIZE).fill(null),
  deck: [...TEST_DECK_IDS], // Clone deck
  deckCount: TEST_DECK_IDS.length,
  class: ClassType.NEUTRAL // Placeholder
});

interface GameStore extends GameState {
  initGame: () => void;
  endTurn: () => void;
  playCard: (cardIndex: number) => void;
  attack: (attackerIndex: number, targetIndex: number | 'leader') => void;
  evolve: (cardIndex: number) => void;
  addLog: (msg: string, type?: 'info' | 'combat' | 'turn' | 'error') => void;
}

export const useGameStore = create<GameStore>((set, get) => ({
  turnCount: 1,
  currentTurn: 'player',
  player: createInitialPlayer('player'),
  opponent: createInitialPlayer('opponent'),
  winner: null,
  logs: [],

  initGame: () => {
    const { player, opponent } = get();
    // Draw 3 cards for initial hand
    const pHand = player.deck.splice(0, 3).map(createRuntimeCard);
    const oHand = opponent.deck.splice(0, 3).map(createRuntimeCard);
    
    set({
      turnCount: 1,
      currentTurn: 'player',
      winner: null,
      player: { ...player, hand: pHand, pp: 1, maxPp: 1, deckCount: player.deck.length },
      opponent: { ...opponent, hand: oHand, deckCount: opponent.deck.length },
      logs: [{ id: uuidv4(), message: "SYS_INIT: BATTLE_ROUTINE_STARTED", type: 'info' }]
    });
  },

  playCard: (cardIndex) => {
    const { currentTurn, player, opponent, addLog } = get();
    const activePlayer = currentTurn === 'player' ? player : opponent;
    const card = activePlayer.hand[cardIndex];

    if (!card) return;
    if (activePlayer.pp < card.currentCost) {
      addLog("ERR: INSUFFICIENT_PP", 'error');
      return;
    }

    // Check board space for followers/amulets
    if (card.type !== CardType.SPELL) {
      const emptySlotIndex = activePlayer.board.findIndex(slot => slot === null);
      if (emptySlotIndex === -1) {
        addLog("ERR: BOARD_FULL", 'error');
        return;
      }

      // Pay Cost
      const newPp = activePlayer.pp - card.currentCost;
      
      // Move from Hand to Board
      const newHand = [...activePlayer.hand];
      newHand.splice(cardIndex, 1);
      
      const newBoard = [...activePlayer.board];
      newBoard[emptySlotIndex] = card;

      // Update State
      if (currentTurn === 'player') {
        set({ player: { ...player, pp: newPp, hand: newHand, board: newBoard } });
      } else {
        set({ opponent: { ...opponent, pp: newPp, hand: newHand, board: newBoard } });
      }

      addLog(`EXEC: SUMMON [${card.name}]`, 'combat');
      
      // TODO: Execute Fanfare Effects Here (The hard part from VBA)
      
    } else {
      // Spell Logic
      const newPp = activePlayer.pp - card.currentCost;
      const newHand = [...activePlayer.hand];
      newHand.splice(cardIndex, 1);

      // Spells go to graveyard (Shadows +1)
      const newShadows = activePlayer.shadows + 1;

      if (currentTurn === 'player') {
        set({ player: { ...player, pp: newPp, hand: newHand, shadows: newShadows } });
      } else {
        set({ opponent: { ...opponent, pp: newPp, hand: newHand, shadows: newShadows } });
      }
      addLog(`EXEC: CAST [${card.name}]`, 'combat');
      
      // TODO: Execute Spell Effects Here
    }
  },

  attack: (attackerIndex, targetIndex) => {
    const { currentTurn, player, opponent, addLog } = get();
    const attackerState = currentTurn === 'player' ? player : opponent;
    const defenderState = currentTurn === 'player' ? opponent : player;
    
    const attacker = attackerState.board[attackerIndex];
    if (!attacker || !attacker.canAttack) return;

    // Attack Leader
    if (targetIndex === 'leader') {
        // Check for Wards on defender board
        const hasWard = defenderState.board.some(c => c && c.hasWard);
        if (hasWard) {
            addLog("ERR: BLOCKED_BY_WARD", 'error');
            return;
        }

        const damage = attacker.currentAttack;
        const newHp = defenderState.hp - damage;
        
        if (currentTurn === 'player') {
            set({ opponent: { ...opponent, hp: newHp } });
        } else {
            set({ player: { ...player, hp: newHp } });
        }
        
        addLog(`ATTACK: LEADER [-${damage} HP]`, 'combat');
        
        // Update attacker status
        attacker.canAttack = false; // Usually can only attack once
        if(!attacker.hasRush && !attacker.hasStorm) attacker.canAttack = false; 
        
        // Check Win
        if (newHp <= 0) {
            set({ winner: currentTurn });
            addLog(`VICTORY: ${currentTurn.toUpperCase()}`, 'info');
        }
    } 
    // Attack Follower
    else if (typeof targetIndex === 'number') {
        const defender = defenderState.board[targetIndex];
        if (!defender) return;

        // Check Ward Logic (Must attack Ward if exists)
        const hasWard = defenderState.board.some(c => c && c.hasWard);
        if (hasWard && !defender.hasWard) {
             addLog("ERR: MUST_ATTACK_WARD", 'error');
             return;
        }

        // Clash Damage
        const dmgToDefender = attacker.currentAttack;
        const dmgToAttacker = defender.currentAttack;

        defender.currentDefense -= dmgToDefender;
        attacker.currentDefense -= dmgToAttacker;

        addLog(`CLASH: ${attacker.name} vs ${defender.name}`, 'combat');

        // Resolve Deaths
        const newAttackerBoard = [...attackerState.board];
        const newDefenderBoard = [...defenderState.board];

        if (attacker.currentDefense <= 0) {
            newAttackerBoard[attackerIndex] = null;
            attackerState.shadows += 1;
            addLog(`DESTROYED: ${attacker.name}`, 'info');
        } else {
            attacker.canAttack = false;
        }

        if (defender.currentDefense <= 0) {
            newDefenderBoard[targetIndex] = null;
            defenderState.shadows += 1;
            addLog(`DESTROYED: ${defender.name}`, 'info');
        }

        if (currentTurn === 'player') {
            set({ player: { ...player, board: newAttackerBoard }, opponent: { ...opponent, board: newDefenderBoard } });
        } else {
            set({ opponent: { ...opponent, board: newAttackerBoard }, player: { ...player, board: newDefenderBoard } });
        }
    }
  },

  evolve: (cardIndex) => {
     // Simplified Evolution Logic (+2/+2 and Rush)
     const { currentTurn, player, opponent, addLog } = get();
     const activePlayer = currentTurn === 'player' ? player : opponent;
     const card = activePlayer.board[cardIndex];
     
     if (!card || card.isEvolved || activePlayer.ep <= 0) return;
     
     card.isEvolved = true;
     card.currentAttack += 2;
     card.currentDefense += 2;
     card.hasRush = true;
     card.canAttack = true; // Rush allows attacking followers immediately

     const newEp = activePlayer.ep - 1;
     
     if (currentTurn === 'player') {
         set({ player: { ...player, ep: newEp }});
     } else {
         set({ opponent: { ...opponent, ep: newEp }});
     }
     addLog(`EVOLVE: ${card.name}`, 'info');
  },

  endTurn: () => {
    const { currentTurn, player, opponent, turnCount, addLog } = get();
    
    // Switch Turn
    const nextTurn = currentTurn === 'player' ? 'opponent' : 'player';
    const nextPlayerState = nextTurn === 'player' ? player : opponent;
    
    // Max PP increase logic (simplified)
    const nextMaxPp = Math.min(nextPlayerState.maxPp + 1, MAX_PP_LIMIT);
    
    // Draw Card
    const newHand = [...nextPlayerState.hand];
    if (nextPlayerState.deck.length > 0 && newHand.length < MAX_HAND_SIZE) {
        const drawId = nextPlayerState.deck.shift(); // Simplify: remove from "deck" array (which is just IDs)
        if (drawId) newHand.push(createRuntimeCard(drawId));
    } else if (nextPlayerState.deck.length === 0) {
        // Reaper card logic would go here (Draw from empty deck = lose)
        set({ winner: currentTurn }); // Opponent wins if I deck out
        return;
    }

    // Refresh Board (Attacks reset)
    const newBoard = nextPlayerState.board.map(card => {
        if (card) card.canAttack = true; // Summoning sickness removed
        return card;
    });
    
    // State Update
    const updatedNextPlayer = {
        ...nextPlayerState,
        maxPp: nextMaxPp,
        pp: nextMaxPp, // Refill
        hand: newHand,
        board: newBoard
    };

    if (nextTurn === 'player') {
        set({ 
            currentTurn: 'player', 
            turnCount: turnCount + 1, 
            player: updatedNextPlayer 
        });
    } else {
        set({ 
            currentTurn: 'opponent', 
            opponent: updatedNextPlayer 
        });
        
        // Simple AI for Opponent (Plays random card)
        setTimeout(() => {
            const state = get();
            const aiHand = state.opponent.hand;
            // Find playable card
            const playableIndex = aiHand.findIndex(c => c.currentCost <= state.opponent.pp);
            if (playableIndex !== -1) {
                state.playCard(playableIndex);
            }
            // End AI turn
            setTimeout(() => state.endTurn(), 1000);
        }, 1000);
    }
    
    addLog(`TURN CHANGE: ${nextTurn.toUpperCase()}`, 'turn');
  },

  addLog: (message, type = 'info') => {
    set((state) => ({
      logs: [{ id: uuidv4(), message, type }, ...state.logs].slice(0, 20)
    }));
  }
}));
