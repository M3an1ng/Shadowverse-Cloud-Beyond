import { GameStore } from '../store';
import { RuntimeCard } from '../types';
import { evaluateCondition } from './conditionEvaluator';
import { splitEffect } from './dslParser';

/**
 * VBA Migration: GetTimePoint
 * Handles effect timing and condition checking.
 * 
 * Logic:
 * 1. Parses the effect string (handling nested {} blocks).
 * 2. Checks conditions (If|...).
 * 3. Handles special keywords like Mode, Select, etc. (To be implemented fully).
 * 4. Returns true if the effect should proceed, false otherwise.
 */

export const processEffect = (effectStr: string, store: GameStore, sourceCard: RuntimeCard, triggerContext?: any): boolean => {
    // 1. Handle "Remove|All" (Silence)
    // In VBA: If InStr(Str, "Remove|All") > 0 Then GetTimePoint = False
    // We assume silence is handled before calling this, or we check card status here.
    // For now, we proceed.

    // 2. Split Effect (handling nested braces)
    // The VBA uses a complex loop to extract {} blocks. 
    // Our `splitEffect` in dslParser should handle the top-level splitting by '||'.
    // However, GetTimePoint also handles nested replacement.
    
    // For MVP, we'll rely on the pre-parsed structure or simple splitting if raw string.
    const effectParts = effectStr.split('||'); // Simplified split for now

    for (let part of effectParts) {
        // 3. Handle "Change" logic (Change|True/False based on context)
        if (part.startsWith('Change|')) {
            // Logic to modify subsequent parts based on condition
            // E.g. Change|If|Overflow -> if overflow is true, execute next part, else skip?
            // VBA: If True -> Process next part; If False -> Process previous part (or skip?)
            // This is complex control flow.
            
            const changeCondition = part.substring(7); // Remove "Change|"
            let conditionMet = false;

            if (changeCondition === 'SuperEvolve') {
                conditionMet = triggerContext === 'SuperEvolve';
            } else if (changeCondition === 'Enhance') {
                conditionMet = triggerContext === 'Enhance';
            } else {
                conditionMet = evaluateCondition(changeCondition, store, sourceCard);
            }

            // If condition met, we continue to next parts. If not, we might need to skip?
            // VBA logic implies branching. For MVP, we'll assume linear execution
            // where "Change|False" might stop execution or skip the immediate next block.
            if (!conditionMet) {
                store.addLog(`TRIGGER: Condition Failed [${changeCondition}]`, 'info');
                return false;
            }
        }

        // 4. Handle "If" conditions
        if (part.startsWith('If|')) {
            if (!evaluateCondition(part, store, sourceCard)) {
                store.addLog(`TRIGGER: Condition Failed [${part}]`, 'info');
                return false; // Stop processing this effect chain
            }
            store.addLog(`TRIGGER: Condition Met [${part}]`, 'info');
        }

        // 5. Handle "Mode" (Choice)
        if (part.startsWith('Mode')) {
            // Requires UI interaction.
            // We need to signal the UI to show a choice dialog.
            // This might be async.
            console.warn('Mode selection not implemented yet:', part);
            // For now, pick first option or fail
            return false; 
        }

        // 6. Handle "Select" (Targeting)
        if (part.includes('Select&')) {
             // Requires UI interaction or auto-selection.
             // We need to signal the UI to enter targeting mode.
             console.warn('Target selection not implemented yet:', part);
             // For now, fail to avoid executing untargeted actions
             return false;
        }
    }

    return true;
};

/**
 * Checks if a card's effect matches the current trigger.
 * Corresponds to the matching logic in GetAction.
 */
export const checkTrigger = (effectStr: string, triggerType: string, triggerDetail?: string): boolean => {
    // Effect format: "When|EnterField|..." or "Fanfare|..."
    
    // 1. Standard Keywords
    if (triggerType === 'Fanfare' && effectStr.startsWith('Fanfare')) return true;
    if (triggerType === 'LastWords' && effectStr.startsWith('LastWords')) return true;
    if (triggerType === 'TurnStart' && effectStr.startsWith('TurnStart')) return true;
    if (triggerType === 'TurnEnd' && effectStr.startsWith('TurnEnd')) return true;

    // 2. "When" Triggers
    // Format: When|TriggerType|...
    if (effectStr.startsWith('When|')) {
        const parts = effectStr.split('|');
        if (parts[1] === triggerType) {
            // Further checks? e.g. specific card ID in detail
            return true;
        }
    }

    return false;
};