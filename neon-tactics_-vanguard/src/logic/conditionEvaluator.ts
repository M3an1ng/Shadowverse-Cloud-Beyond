import { GameStore } from '../store';
import { RuntimeCard } from '../types';
import { calculateValue } from './valueCalculator';

/**
 * VBA Migration: GetIf
 * Evaluates conditions like "If|Overflow", "If|Combo&3".
 */
export const evaluateCondition = (condStr: string, store: GameStore, sourceCard: RuntimeCard): boolean => {
    const parts = condStr.split('|');
    // Format: If|Cond1|Cond2... (All must be true, usually)

    // We iterate from index 1 because index 0 is "If"
    for(let i = 1; i < parts.length; i++) {
        const cond = parts[i];
        
        if (cond === 'Overflow') {
            if (store[store.currentTurn].maxPp < 7) return false;
        }
        else if (cond === 'Vengeance') {
            if (store[store.currentTurn].hp > 10) return false;
        }
        else if (cond === 'Resonance') {
            if (store[store.currentTurn].deckCount % 2 !== 0) return false;
        }
        else if (cond === 'Avarice') {
            // Assuming we track cards drawn this turn somewhere, or infer it?
            // For now, placeholder or check store if property exists
            // if (store[store.currentTurn].cardsDrawnThisTurn < 2) return false;
        }
        else if (cond === 'Wrath') {
             // if (store[store.currentTurn].damageTakenThisTurn < 7) return false; // Actually it's 7 times in your turn
        }
        else if (cond.startsWith('Combo&')) {
            const req = parseInt(cond.split('&')[1]);
            if (store[store.currentTurn].cardsPlayedThisTurn < req) return false;
        }
        else if (cond.startsWith('Rally&')) {
            const req = parseInt(cond.split('&')[1]);
            if (store[store.currentTurn].rally < req) return false;
        }
        else if (cond.startsWith('Necromancy&')) {
            const req = parseInt(cond.split('&')[1]);
            if (store[store.currentTurn].shadows < req) return false;
             // Note: Necromancy usually CONSUMES shadows. 
             // In VBA, 'GetIf' just checks, 'GetAction' consumes? 
             // Actually Necromancy is an Effect Action in VBA usually handled in GetTimePoint->Action.
             // But sometimes used as Condition.
        }
        else if (cond.startsWith('{') && cond.includes('}')) {
             // Comparison: {Value}>N
             // Example: {X=Cards|Allied-Field}>2
             const operatorMatch = cond.match(/([<>=]+)/);
             if (operatorMatch) {
                 const operator = operatorMatch[1];
                 const [leftExpr, rightValStr] = cond.split(operator);
                 
                 // Remove braces from left expression
                 const cleanLeft = leftExpr.replace('{', '').replace('}', '');
                 
                 const leftVal = calculateValue(cleanLeft, store, sourceCard);
                 const rightVal = parseInt(rightValStr); // Usually a constant number
                 
                 if (operator === '>') {
                     if (!(leftVal > rightVal)) return false;
                 } else if (operator === '<') {
                     if (!(leftVal < rightVal)) return false;
                 } else if (operator === '=') {
                     if (!(leftVal === rightVal)) return false;
                 } else if (operator === '>=') {
                     if (!(leftVal >= rightVal)) return false;
                 } else if (operator === '<=') {
                     if (!(leftVal <= rightVal)) return false;
                 }
             }
        }
    }
    
    return true;
};
