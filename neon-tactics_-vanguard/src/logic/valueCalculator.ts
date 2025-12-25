import { GameStore } from '../store';
import { RuntimeCard } from '../types';
import { parseTargetString, resolveTargets } from './targetSelector';

/**
 * VBA Migration: GetX
 * Parses value expressions like "X=Cards|Allied-Field-Follower" or constants.
 */
export const calculateValue = (valStr: string, store: GameStore, sourceCard: RuntimeCard): number => {
    // 1. Simple Number
    if (!isNaN(Number(valStr))) {
        return Number(valStr);
    }

    // 2. X=... Syntax
    if (valStr.startsWith('X=') || valStr.startsWith('Y=')) {
        const expr = valStr.substring(2);
        
        if (expr.startsWith('Cards|')) {
            // Count cards matching criteria
            const targetStr = expr.replace('Cards|', '');
            const criteria = parseTargetString(targetStr);
            const targets = resolveTargets(store, criteria);
            return targets.length;
        }
        
        if (expr.startsWith('Attack|')) {
             const targetStr = expr.replace('Attack|', '');
             if (targetStr === 'It' || targetStr === 'ThisCard') {
                 return sourceCard.currentAttack;
             }
             // Sum attack of targets
             const criteria = parseTargetString(targetStr);
             const targets = resolveTargets(store, criteria);
             return targets.reduce((sum, t) => sum + t.card.currentAttack, 0);
        }

        if (expr.startsWith('Defense|')) {
            const targetStr = expr.replace('Defense|', '');
            if (targetStr === 'It' || targetStr === 'ThisCard') {
                return sourceCard.currentDefense;
            }
            // Sum defense of targets
            const criteria = parseTargetString(targetStr);
            const targets = resolveTargets(store, criteria);
            return targets.reduce((sum, t) => sum + t.card.currentDefense, 0);
       }

       if (expr.startsWith('Cost|')) {
            const targetStr = expr.replace('Cost|', '');
            if (targetStr === 'It' || targetStr === 'ThisCard') {
                return sourceCard.currentCost;
            }
            // Sum cost of targets
            const criteria = parseTargetString(targetStr);
            const targets = resolveTargets(store, criteria);
            return targets.reduce((sum, t) => sum + t.card.currentCost, 0);
       }
        
        // Other VBA vars: Shadows, Turn, etc.
    }

    // 3. Direct Variables
    if (valStr === 'Shadows') {
        return store[store.currentTurn].shadows;
    }
    if (valStr === 'MaxPP') {
        return store[store.currentTurn].maxPp;
    }
    if (valStr === 'Turn') {
        return store.turnCount;
    }
    if (valStr === 'Combo') {
        return store[store.currentTurn].cardsPlayedThisTurn;
    }
    if (valStr === 'Rally') {
        return store[store.currentTurn].rally;
    }
    if (valStr === 'HandCount') {
        return store[store.currentTurn].hand.length;
    }
    if (valStr === 'EnemyHandCount') {
        const enemyId = store.currentTurn === 'player' ? 'opponent' : 'player';
        return store[enemyId].hand.length;
    }

    return 0;
};
