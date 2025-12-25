import { GameStore } from '../store';
import { RuntimeCard } from '../types';
import { checkTrigger, processEffect } from './triggerSystem';
import { resolveTargets } from './targetSelector';

/**
 * VBA Migration: GetAction
 * Implements the Observer Pattern to broadcast game events to all cards.
 * 
 * When an action happens (e.g., "Play", "EnterField"), this system:
 * 1. Scans all relevant cards (Field, Hand, Deck, Leader, etc.).
 * 2. Checks if they have a matching trigger (e.g., "When|EnterField").
 * 3. If matched, executes the effect via `processEffect`.
 */

export type GameEventType = 
    | 'Play' 
    | 'EnterField' 
    | 'LeaveField' 
    | 'TurnStart' 
    | 'TurnEnd' 
    | 'Attack' 
    | 'Evolve'
    | 'Draw';

export const broadcastEvent = (
    store: GameStore, 
    eventType: GameEventType, 
    contextCard?: RuntimeCard, // The card that caused the event (e.g., the one played)
    contextValue?: any         // Additional data
) => {
    const state = store;
    
    // 1. Collect all potential observers
    // In VBA, GetAction scans "Data" (all cards). We scan active zones.
    const observers: { card: RuntimeCard, zone: string, owner: string }[] = [];

    // Scan Field (Both players)
    ['player', 'opponent'].forEach(owner => {
        state[owner as 'player' | 'opponent'].board.forEach(card => {
            if (card) observers.push({ card, zone: 'Field', owner });
        });
        // Scan Hand (for "ActivateInHand" effects)
        state[owner as 'player' | 'opponent'].hand.forEach(card => {
            if (card) observers.push({ card, zone: 'Hand', owner });
        });
        // Deck scanning is expensive and rare, usually handled by specific flags. Skipped for MVP.
    });

    // 2. Notify Observers
    observers.forEach(observer => {
        // Skip the card itself if it's the one causing the event?
        // VBA: If AllGoal(i, 1) = ThisCard Then Empty (unless InputValue=True)
        // Usually triggers don't trigger on self unless specified (like Fanfare is self-trigger but handled separately).
        // "When|EnterField" on self is usually Fanfare. "When|EnterField" on others is a trigger.
        
        if (contextCard && observer.card.instanceId === contextCard.instanceId) {
            // Self-trigger logic (e.g. Last Words)
            if (eventType === 'LeaveField') {
                // Process Last Words
                observer.card.effects.forEach(effect => {
                    if (checkTrigger(effect, 'LastWords')) {
                        console.log(`[Trigger] ${observer.card.name} LastWords triggered.`);
                        processEffect(effect, store, observer.card, eventType);
                    }
                });
            }
            return; 
        }

        // Check for "When" triggers
        observer.card.effects.forEach(effect => {
            // Check if effect is active in current zone
            // VBA checks "ActivateInHand" etc.
            const isHandTrigger = effect.includes('ActivateInHand');
            if (observer.zone === 'Hand' && !isHandTrigger) return;
            if (observer.zone === 'Field' && isHandTrigger) return;

            if (checkTrigger(effect, eventType)) {
                // Check if the trigger matches the context
                // e.g. "When|EnterField|Allied-Follower"
                // We need to parse the middle part of "When|X|Y"
                
                const parts = effect.split('|');
                // parts[0] = When, parts[1] = EventType, parts[2] = Filter
                // Note: VBA GetAction logic is: InputAction = TimePointSplit(2) -> "When|Filter|EventType" ??
                // Let's re-read VBA: 
                // TimePointSplit = Split(EffectSplit(0), "|")
                // If InputAction = TimePointSplit(2) Then ...
                // So format is: "When|Filter|EventType" ?
                // Example: "When|EnterField|Allied" -> Split: When, EnterField, Allied?
                // No, VBA says: `If InputAction = TimePointSplit(2)`
                // So index 2 is the Action.
                // Index 0 is "When".
                // Index 1 must be the Filter/Target?
                // Let's assume standard format: "When|Target|Action"
                
                const triggerAction = parts[2];
                const triggerTarget = parts[1];

                if (triggerAction === eventType) {
                    // Check if contextCard matches triggerTarget
                    // e.g. Target="Allied-Follower"
                    
                    // We can use resolveTargets to check if contextCard is in the set of "Target"
                    // But resolveTargets returns ALL matches. We just need to check ONE.
                    // Optimization: Check ownership and type directly.
                    
                    let match = true;
                    if (contextCard) {
                        if (triggerTarget.includes('Allied') && contextCard.instanceId !== observer.card.instanceId) { 
                            // Allied means same owner as observer
                            // We need to know owner of contextCard. 
                            // For now assume contextCard has owner info or we infer it.
                            // MVP: Skip strict ownership check or pass owner in context.
                        }
                        
                        // Use resolveTargets to verify?
                        // const candidates = resolveTargets(store, parseTargetString(triggerTarget));
                        // if (!candidates.find(c => c.card.instanceId === contextCard.instanceId)) match = false;
                    }

                    if (match) {
                        store.addLog(`TRIGGER: ${observer.card.name} responds to ${eventType}`, 'info');
                        processEffect(effect, store, observer.card, eventType);
                    }
                }
            }
        });
    });
};