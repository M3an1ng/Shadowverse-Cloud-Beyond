import { TargetCriteria, CardType, RuntimeCard, GameState } from '../types';

/**
 * VBA Migration: GetGoal (Parsing & Resolution)
 */

export const parseTargetString = (targetStr: string): TargetCriteria => {
    const criteria: TargetCriteria = {
        zone: [], 
        owner: 'Both',
        cardType: []
    };

    // 1. Parse Selection Method & Count
    if (targetStr.includes('Random&')) {
        criteria.random = true;
        const match = targetStr.match(/Random&(\d+)/);
        if (match) criteria.count = parseInt(match[1]);
    } else if (targetStr.includes('Select&')) {
        criteria.random = false;
        const match = targetStr.match(/Select&(\d+)/);
        if (match) criteria.count = parseInt(match[1]);
    } else if (targetStr.includes('All')) {
        criteria.count = 'All';
    } else {
        criteria.count = 1; 
    }

    // 2. Parse Owner
    if (targetStr.includes('Allied')) {
        criteria.owner = 'Allied';
    } else if (targetStr.includes('Enemy')) {
        criteria.owner = 'Enemy';
    }

    // 3. Parse Zone
    if (targetStr.includes('Hand')) criteria.zone?.push('Hand');
    if (targetStr.includes('Deck')) criteria.zone?.push('Deck');
    if (targetStr.includes('Field')) criteria.zone?.push('Field');
    
    if (!criteria.zone || criteria.zone.length === 0) {
        criteria.zone = ['Field']; 
    }

    // 4. Parse Card Type
    if (targetStr.includes('Follower')) criteria.cardType?.push(CardType.FOLLOWER);
    if (targetStr.includes('Amulet')) criteria.cardType?.push(CardType.AMULET);
    if (targetStr.includes('Leader')) criteria.cardType?.push(CardType.LEADER);
    if (targetStr.includes('Card')) {
        criteria.cardType?.push(CardType.FOLLOWER, CardType.SPELL, CardType.AMULET);
    }

    // 5. Parse Trait (Trait:8)
    const traitMatch = targetStr.match(/Trait:(\d+)/);
    if (traitMatch) {
        criteria.traitId = traitMatch[1];
    }

    // 6. Parse Cost (Cost:X)
    const costMatch = targetStr.match(/Cost:(\d+)/);
    if (costMatch) {
        criteria.cost = parseInt(costMatch[1]);
    }

    return criteria;
};

export interface TargetContext {
    card: RuntimeCard;
    index: number;
    owner: 'player' | 'opponent';
    zone: 'Hand' | 'Field' | 'Deck';
}

export const resolveTargets = (state: GameState, criteria: TargetCriteria): TargetContext[] => {
    const currentId = state.currentTurn;
    const enemyId = currentId === 'player' ? 'opponent' : 'player';
    
    let candidates: TargetContext[] = [];

    // Helper to collect from a zone
    const collectFromZone = (playerId: 'player' | 'opponent', zoneName: 'Hand' | 'Field' | 'Deck') => {
        const pState = state[playerId];
        if (zoneName === 'Field') {
            pState.board.forEach((card, idx) => {
                if (card) candidates.push({ card, index: idx, owner: playerId, zone: 'Field' });
            });
        } else if (zoneName === 'Hand') {
            pState.hand.forEach((card, idx) => {
                if (card) candidates.push({ card, index: idx, owner: playerId, zone: 'Hand' });
            });
        }
        // Deck usually just IDs in simple state, complicated to query fully without expanding
    };

    // 1. Gather Candidates
    const zones = criteria.zone && criteria.zone.length > 0 ? criteria.zone : ['Field'];
    
    zones.forEach(z => {
        if (z === 'Graveyard') return; // Not implemented fully yet
        
        if (criteria.owner === 'Allied' || criteria.owner === 'Both') {
            collectFromZone(currentId, z as any);
        }
        if (criteria.owner === 'Enemy' || criteria.owner === 'Both') {
            collectFromZone(enemyId, z as any);
        }
    });

    // 2. Filter
    candidates = candidates.filter(ctx => {
        // Filter Type
        if (criteria.cardType && criteria.cardType.length > 0) {
            if (!criteria.cardType.includes(ctx.card.type as CardType)) return false;
        }
        // Filter Trait (simplified string check, VBA uses IDs)
        // Note: Trait logic needs specific mapping if using Trait:ID
        if (criteria.traitId) {
            // Assuming trait is stored as string name in RuntimeCard for now, 
            // or we need a mapping from ID to Name if traitId is numeric.
            // For now, skipping strict ID check unless we have the map.
        }

        // Filter Cost
        if (criteria.cost !== undefined) {
            if (ctx.card.currentCost !== criteria.cost) return false;
        }
        
        return true;
    });

    // 3. Select
    if (criteria.count === 'All') {
        return candidates;
    }
    
    if (criteria.random) {
        // Fisher-Yates shuffle
        for (let i = candidates.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [candidates[i], candidates[j]] = [candidates[j], candidates[i]];
        }
        return candidates.slice(0, typeof criteria.count === 'number' ? criteria.count : 1);
    } 
    else {
        // MANUAL SELECTION (Not UI implemented yet)
        // For now, return empty or first valid to avoid crash if auto-resolving
        return candidates.slice(0, typeof criteria.count === 'number' ? criteria.count : 1);
    }
};