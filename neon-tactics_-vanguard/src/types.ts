
export enum CardType {
  FOLLOWER = 'Follower',
  SPELL = 'Spell',
  AMULET = 'Amulet',
  LEADER = 'Leader',
  TOKEN = 'Token'
}

export enum ClassType {
  FORESTCRAFT = 'Forestcraft',
  SWORDCRAFT = 'Swordcraft',
  RUNECRAFT = 'Runecraft',
  DRAGONCRAFT = 'Dragoncraft',
  ABYSSCRAFT = 'Abysscraft',
  HAVENCRAFT = 'Havencraft',
  PORTALCRAFT = 'Portalcraft',
  NEUTRAL = 'Neutral'
}

export enum Rarity {
  BRONZE = 'Bronze',
  SILVER = 'Silver',
  GOLD = 'Gold',
  LEGENDARY = 'Legendary'
}

export interface CardModel {
  id: string;
  name: string;
  cardSet: string;
  class: ClassType | string;
  rarity: Rarity | string;
  type: CardType | string;
  trait: string;
  cost: number;
  attack: number; // Base attack
  defense: number; // Base defense
  countdown: number;
  effects: string[]; 
}

export interface RuntimeCard extends CardModel {
  instanceId: string; // Unique ID for this specific card instance in game
  currentAttack: number;
  currentDefense: number;
  currentCost: number;
  isEvolved: boolean;
  canAttack: boolean;
  hasStorm: boolean;
  hasRush: boolean;
  hasWard: boolean;
  isEngaged: boolean; // For Amulets
}

export interface PlayerState {
  playerId: 'player' | 'opponent';
  hp: number;
  maxHp: number;
  pp: number;     // Play Points (Mana)
  maxPp: number;
  ep: number;     // Evolution Points
  shadows: number; // Graveyard count
  rally: number;   // Total followers summoned
  cardsPlayedThisTurn: number; // Combo count
  hand: RuntimeCard[];
  board: (RuntimeCard | null)[]; // Fixed 5 slots
  deck: string[]; // Array of Card IDs
  deckCount: number;
  class: ClassType;
}

export interface GameState {
  turnCount: number;
  currentTurn: 'player' | 'opponent';
  player: PlayerState;
  opponent: PlayerState;
  winner: 'player' | 'opponent' | null;
  logs: LogEntry[];
}

export interface LogEntry {
  id: string;
  message: string;
  type: 'info' | 'combat' | 'turn' | 'error';
}

// --- DSL Parsing Types ---

export interface TargetCriteria {
    zone?: ('Field' | 'Hand' | 'Deck' | 'Graveyard')[];
    owner?: 'Allied' | 'Enemy' | 'Both';
    cardType?: CardType[];
    count?: number | 'All'; // How many to select
    random?: boolean;
    traitId?: string;
    cost?: number;
    // Add more criteria as we port GetGoal
}

export interface EffectNode {
    raw: string;
    trigger?: string; // Fanfare, LastWords, etc.
    actions: string[]; // The parts after the trigger, e.g. "Damage|..."
}
