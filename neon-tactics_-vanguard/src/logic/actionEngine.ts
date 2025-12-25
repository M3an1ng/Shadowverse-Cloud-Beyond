
import { RuntimeCard, CardType } from '../types';
import { splitEffect } from './dslParser';
import { parseTargetString, resolveTargets } from './targetSelector';
import { calculateValue } from './valueCalculator';
import { evaluateCondition } from './conditionEvaluator';
import type { GameStore } from '../store';

/**
 * VBA Migration: Action Engine
 * Executes DSL command strings.
 */

export const executeEffectString = (effectStr: string, sourceCard: RuntimeCard, store: GameStore) => {
    // console.log(`[ENGINE] Executing: "${effectStr}" Source: ${sourceCard.name} (${sourceCard.instanceId})`);
    const effectParts = splitEffect(effectStr);

    // Check for "If" condition at start of block or inside
    // The structure is usually: Trigger||Action1||Action2
    // If Action is "If|...||ActionX", splitEffect handles ||.
    // But "If|...||Action" might be one block in splitEffect if nested?
    // No, splitEffect splits by ||. 
    // So "If|Cond||Draw|1" -> ["If|Cond", "Draw|1"].
    // This implies sequential execution where "If" sets a flag?
    // In VBA `GetTimePoint.bas`, `GetIf` sets a flag `IFFlag`.

    let skipRest = false;

    for (const part of effectParts) {
        if (skipRest) break;

        const subParts = part.split('|');
        const verb = subParts[0];

        if (verb === 'If') {
            const result = evaluateCondition(part, store, sourceCard);
            // console.log(`[ENGINE] Condition "${part}" -> ${result}`);
            if (!result) {
                skipRest = true; // Basic implementation: If false, skip remaining actions in this chain
            }
            // If true, continue
            // Handle Necromancy consumption if it was a condition? 
            // In Shadowverse, Necromancy is usually "If Necromancy(N) -> Consume N and do X".
            // For now, we assume passive check or handle consumption in specific action.
            if (result) {
                // Check for resource consumption embedded in condition (Necromancy/EarthRite)
                // This is tricky. Let's handle consumption explicitly if we can.
                for (const cond of subParts.slice(1)) {
                    if (cond.startsWith('Necromancy&')) {
                        const cost = parseInt(cond.split('&')[1]);
                        store.incrementShadows(store.currentTurn, -cost);
                        store.addLog(`Necromancy: -${cost} Shadows`, 'info');
                    }
                }
            }
        } else {
            executeCommand(part, sourceCard, store);
        }
    }
};

const executeCommand = (commandStr: string, sourceCard: RuntimeCard, store: GameStore) => {
    // console.log(`[ENGINE] Command: "${commandStr}"`);
    const parts = commandStr.split('|');
    const verb = parts[0];

    if (verb === 'Draw') {
        const valStr = parts[1] || '1';
        const count = calculateValue(valStr, store, sourceCard);
        store.drawCard(store.currentTurn, count);
        store.addLog(`EFFECT: Draw ${count}`, 'info');
    }

    else if (verb === 'Damage') {
        const targetStr = parts[1];
        const valStr = parts[2] || '0';
        const value = calculateValue(valStr, store, sourceCard);

        if (targetStr.includes('Enemy-Leader')) {
            const targetPlayer = store.currentTurn === 'player' ? 'opponent' : 'player';
            store.damageLeader(targetPlayer, value);
        }
        else if (targetStr.includes('Enemy-Follower')) {
            const criteria = parseTargetString(targetStr);
            // Use resolveTargets
            const targets = resolveTargets(store, criteria);

            targets.forEach(ctx => {
                if (ctx.zone === 'Field') {
                    store.damageFollower(ctx.owner, ctx.index, value);
                }
            });
        }
    }

    else if (verb === 'Restore') {
        const targetStr = parts[1];
        const valStr = parts[2] || '0';
        const value = calculateValue(valStr, store, sourceCard);

        if (targetStr.includes('Allied-Leader')) {
            store.healLeader(store.currentTurn, value);
            store.addLog(`EFFECT: Restore ${value} to Leader`, 'info');
        }
    }

    else if (verb === 'Summon') {
        const cardId = parts[1];
        const valStr = parts[2] || '1';
        const count = calculateValue(valStr, store, sourceCard);

        for (let i = 0; i < count; i++) {
            store.summonToken(store.currentTurn, cardId);
        }
    }

    else if (verb === 'PP') {
        const valStr = parts[1] || '0';
        const value = calculateValue(valStr, store, sourceCard);
        store.modifyPP(store.currentTurn, value);
        store.addLog(`EFFECT: PP +${value}`, 'info');
    }

    else if (verb === 'MaxPP') {
        const valStr = parts[1] || '0';
        const value = calculateValue(valStr, store, sourceCard);
        console.log(`[ENGINE] MaxPP Triggered. Value: ${value} (Raw: ${valStr})`);
        store.modifyMaxPP(store.currentTurn, value);
        // In Shadowverse, gaining an empty play point orb usually means MaxPP+1.
        // It does NOT recover PP unless specified.
        // However, if the player is NOT at 10 PP, gaining an empty orb effectively increases available PP for next turn.
        // If the card says "Gain an empty play point orb", it means MaxPP++.
        // If it says "Gain a play point orb", it usually means MaxPP++ AND PP++.
        // Dragon Oracle text: "Gain an empty play point orb." -> MaxPP only.
        // But wait, if I have 2/2 PP and use Oracle (2pp), I go to 0/2. Effect -> 0/3.
        // Next turn 3/3.
        // If I use Oracle at 10 PP, I draw a card.

        // The issue reported is "PP Max not increasing".
        // modifyMaxPP implementation in store:
        // modifyMaxPP: (playerId, amount) => {
        //   set(state => {
        //       const p = playerId === 'player' ? state.player : state.opponent;
        //       const update: Partial<GameStore> = {};
        //       update[playerId] = { ...p, maxPp: Math.min(p.maxPp + amount, MAX_PP_LIMIT) };
        //       return update;
        //   });
        // },
        // This looks correct. Maybe the UI isn't updating?
        // Or maybe the value calculation is returning 0?
        // valStr is usually "1". calculateValue("1") -> 1.

        store.addLog(`EFFECT: MaxPP +${value}`, 'info');
    }

    else {
        console.warn(`[ENGINE] Unknown verb: ${verb} in "${commandStr}"`);
    }
};