// src/ai.js - AI 对手逻辑

import { getCardDefinition } from './cardManager.js'; // 获取卡牌定义
// 导入依赖
// MODIFIED: Added getPlayerState, getCurrentPlayerId, findCardInstanceByUid, executeAttack
import { playCard, handleMulligan, getPlayerState, getCurrentPlayerId, findCardInstanceByUid, executeAttack, evolveCard } from './gameManager.js'; // 添加 evolveCard
import { endTurn } from './turnManager.js'; // 结束回合

// TODO: Move constants like MAX_FIELD_SIZE to a dedicated constants.js file
const MAX_FIELD_SIZE = 5; // Define locally for now

console.log("ai.js loaded");

// --- 主 AI 逻辑入口 ---
// 这个函数应该在轮到 AI 时被 turnManager 调用
export function runAILogic(currentGameState) { // 接收当前游戏状态
    console.log("AI Turn: Running AI logic...");
    if (!currentGameState || !currentGameState.players) {
        console.error("AI Error: Missing game state or players for decision making.");
        setTimeout(endTurn, 500); // Failsafe: End turn if state is bad
        return;
    }

    // Ensure player 'player2' is the AI and exists
    const aiPlayer = currentGameState.players.find(p => p && p.id === 'player2');
    if (!aiPlayer || !aiPlayer.isAI) { // Also check the isAI flag
         console.error("AI Error: Could not find AI player (player2) state or it's not marked as AI.");
         setTimeout(endTurn, 500); // Failsafe
         return;
    }

    // --- 逐步执行 AI 动作 ---
    try {
        // 1. 出牌决策
        console.log("AI: Deciding card to play...");
        // Pass gameState for context if needed by future, more complex decisions
        aiDecideAndPlayCard(aiPlayer, currentGameState);

        // 2. 进化决策 (V0.1)
        console.log("AI: Deciding evolves...");
        aiDecideAndEvolve(aiPlayer, currentGameState); // Call the new evolve logic function

        // 3. 攻击决策
        console.log("AI: Deciding attacks...");
        aiDecideAndAttack(aiPlayer, currentGameState); // Call the attack logic function

        // 4. 结束回合 (总是在最后执行)
        console.log("AI: Actions processing finished. Ending turn shortly.");
        // Add a short delay before ending turn to simulate thinking/allow UI updates
        setTimeout(endTurn, 1000); // Call imported endTurn

    } catch (error) {
        console.error("AI Error during logic execution:", error);
        // Failsafe: 即使出错，也要确保能结束回合，避免卡死
        setTimeout(endTurn, 500); // Call imported endTurn
    }
}

// --- AI 辅助决策函数 ---

/**
 * 简单 AI 出牌逻辑：
 * - 找出所有费用足够、战场有空间可出的【随从】牌。
 * - 在这些牌中，选择费用最高的一张打出。
 * - 如果没有可出的随从，则不出牌。
 */
function aiDecideAndPlayCard(aiPlayer, gameStateRef) { // Keep gameStateRef for future use
    if (!aiPlayer || !aiPlayer.hand || !aiPlayer.field) {
        console.error("AI PlayCard Error: Invalid AI player state.");
        return;
    }

    // 检查战场空间
    if (aiPlayer.field.length >= MAX_FIELD_SIZE) {
        console.log(`AI PlayCard: Field is full (${aiPlayer.field.length}/${MAX_FIELD_SIZE}), cannot play follower.`);
        return; // 战场已满，无法出随从
    }

    // 筛选可出的随从
    const playableFollowers = aiPlayer.hand.filter(card =>
        card &&
        card.type === 'Follower' &&
        card.cost <= aiPlayer.pp
    );

    if (playableFollowers.length === 0) {
        console.log("AI PlayCard: No playable followers found in hand (or not enough PP).");
        return; // 没有可出的随从
    }

    // 找到费用最高的随从
    let cardToPlay = null;
    let maxCost = -1;

    for (const card of playableFollowers) {
        if (card.cost > maxCost) {
            maxCost = card.cost;
            cardToPlay = card;
        }
    }

    // 打出找到的牌
    if (cardToPlay) {
        console.log(`AI PlayCard: Choosing to play highest cost follower: ${cardToPlay.name} (Cost: ${cardToPlay.cost}, UID: ${cardToPlay.uid})`);
        try {
            // playCard is imported and assumed to handle the logic based on card UID and current game state correctly
            playCard(cardToPlay.uid);
            console.log(`AI PlayCard: Successfully initiated play for ${cardToPlay.name}.`);
            // No need to render here, playCard handles it.
        } catch(error) {
             console.error(`AI PlayCard Error: Failed to execute playCard for ${cardToPlay.name} (UID: ${cardToPlay.uid}):`, error);
        }
    } else {
        console.log("AI PlayCard: No suitable follower found to play after filtering.");
    }
}

// --- 新增: AI 进化决策函数 (V0.1) ---
/**
 * AI 决策并执行进化操作 (V0.1)
 * @param {object} aiPlayer - AI 玩家状态对象
 * @param {object} currentGameState - 当前游戏状态对象 (用于未来扩展)
 * @returns {boolean} 如果成功执行了进化则返回 true, 否则返回 false
 */
function aiDecideAndEvolve(aiPlayer, currentGameState) { // 注意: 签名已调整以保持文件内一致性
    console.log(`AI [${aiPlayer.id}]: Deciding evolution... EP: ${aiPlayer.ep}, Can Evolve This Turn: ${aiPlayer.canEvolveThisTurn}`);

    // 1. 检查全局进化权限和 EP
    if (!aiPlayer.canEvolveThisTurn) {
        console.log(`AI [${aiPlayer.id}]: Cannot evolve this turn (Flag is false).`);
        return false;
    }
    if (aiPlayer.ep < 1) { // 假设进化固定消耗 1 EP
        console.log(`AI [${aiPlayer.id}]: Cannot evolve this turn (Not enough EP: ${aiPlayer.ep}).`);
        return false;
    }

    let targetFollower = null;

    // 2. 遍历战场寻找第一个符合条件的随从
    for (const cardInstance of aiPlayer.field) {
        // 确保 cardInstance 有效
        if (!cardInstance) continue;

        console.log(`AI [${aiPlayer.id}]: Evaluating follower ${cardInstance.name} (UID: ${cardInstance.uid}) for evolution.`);

        // 3. 检查资格
        if (!cardInstance.isEvolved) {
            console.log(`  - ${cardInstance.name}: Not evolved. Checking definition...`);
            const definition = getCardDefinition(cardInstance.id); // 使用导入的函数

            if (definition && definition.can_evolve === true) {
                console.log(`  - ${cardInstance.name}: Definition allows evolution.`);
                // 4. 选择目标 (第一个符合条件的)
                targetFollower = cardInstance;
                console.log(`AI [${aiPlayer.id}]: Found eligible evolution target: ${targetFollower.name} (UID: ${targetFollower.uid})`);
                break; // 找到目标，停止搜索
            } else {
                 console.log(`  - ${cardInstance.name}: Definition does not allow evolution or definition not found.`);
            }
        } else {
             console.log(`  - ${cardInstance.name}: Already evolved.`);
        }
    }

    // 5. 如果找到目标，执行进化
    if (targetFollower) {
        console.log(`AI [${aiPlayer.id}]: Choosing to evolve ${targetFollower.name} (UID: ${targetFollower.uid}).`);
        try {
            evolveCard(targetFollower.uid); // 使用导入的函数
            console.log(`AI [${aiPlayer.id}]: Successfully initiated evolution for ${targetFollower.name}.`);
            // evolveCard 内部会处理 EP 消耗和 canEvolveThisTurn 标志的设置
            return true; // 表示尝试了进化
        } catch (error) {
            console.error(`AI [${aiPlayer.id}]: Error calling evolveCard for ${targetFollower.name} (UID: ${targetFollower.uid}):`, error);
            return false; // 调用失败，返回 false
        }
    } else {
        // 6. 未找到目标
        console.log(`AI [${aiPlayer.id}]: No eligible follower found on the field to evolve.`);
        return false;
    }
}
// --- 新增: AI 攻击决策函数 (V0.1) ---
function aiDecideAndAttack(aiPlayer, currentGameState) {
    console.log(`AI: ${aiPlayer.id} deciding attacks...`);

    const opponentId = currentGameState.players.find(p => p.id !== aiPlayer.id)?.id;
    if (!opponentId) {
        console.error("AI Attack Error: Cannot find opponent ID.");
        return;
    }
    // Note: getPlayerState needs to be called potentially multiple times if state changes mid-attack sequence
    // For V0.1, we'll fetch opponent state once per attacker for simplicity.

    // 1. Find AI attackers that haven't attacked yet this turn (canAttack is true)
    const availableAttackers = aiPlayer.field.filter(card => card && card.canAttack);
    console.log(`AI: Found ${availableAttackers.length} available attackers:`, availableAttackers.map(a => a.name));
    if (availableAttackers.length === 0) {
        console.log("AI: No available attackers to make decisions for.");
        return;
    }

    // Iterate through each available attacker to make its decision
    for (const attacker of availableAttackers) {
         // Inside the loop, we get the *current* opponent state to account for previous attacks
         const currentOpponentPlayer = getPlayerState(opponentId); // Use imported function
         if (!currentOpponentPlayer) {
            console.error(`AI Attack Error: Opponent state vanished mid-loop for ID: ${opponentId}. Skipping attacker ${attacker.name}.`);
            continue; // Skip this attacker
        }
        const opponentFollowers = currentOpponentPlayer.field.filter(f => f && (f.type === 'Follower' || f.type === 'Follower_Evolved'));
        const opponentLeader = currentOpponentPlayer.leaderCard; // Leader instance directly from player state

        // 2. Determine valid targets considering Ward
        const hasWard = opponentFollowers.some(f => f.keywords?.includes('Ward'));
        let validTargets = [];

        if (hasWard) {
            validTargets = opponentFollowers.filter(f => f.keywords?.includes('Ward'));
            console.log(`AI Attacker ${attacker.name}: Opponent has Ward. Valid targets (${validTargets.length}):`, validTargets.map(t => `${t.name}(${t.currentHealth})`));
        } else {
            validTargets = [...opponentFollowers]; // Add all followers
            if (opponentLeader && opponentLeader.currentHealth > 0) { // Ensure leader isn't already defeated
                 validTargets.push(opponentLeader);
            }
            console.log(`AI Attacker ${attacker.name}: Opponent has no Ward. Valid targets (${validTargets.length}):`, validTargets.map(t => `${t.name}(${t.currentHealth})`));
        }

        if (validTargets.length === 0) {
            console.log(`AI Attacker ${attacker.name}: No valid targets found.`);
            continue; // Skip to the next attacker
        }

        // 3. Sort targets by priority: Ward > Lowest HP Follower > Leader
        validTargets.sort((a, b) => {
            const aIsWard = a.keywords?.includes('Ward');
            const bIsWard = b.keywords?.includes('Ward');
            const aIsFollower = (a.type === 'Follower' || a.type === 'Follower_Evolved');
            const bIsFollower = (b.type === 'Follower' || b.type === 'Follower_Evolved');

            if (aIsWard && !bIsWard) return -1; // Ward first
            if (!aIsWard && bIsWard) return 1;

            if (aIsFollower && !bIsFollower) return -1; // Followers before Leader (if no Ward)
            if (!aIsFollower && bIsFollower) return 1;

            if (aIsFollower && bIsFollower) { // Both followers
                 if (a.currentHealth !== b.currentHealth) {
                    return a.currentHealth - b.currentHealth; // Lowest HP first
                 } else {
                     // Tie-break: Highest attack target first (more threatening), lower cost attacker first might be better? Let's stick to highest attack.
                     return (b.currentAttack || 0) - (a.currentAttack || 0);
                 }
            }
            // Fallback/Default case (e.g., leader vs leader, though impossible)
            return 0;
        });
         console.log(`AI Attacker ${attacker.name}: Sorted valid targets:`, validTargets.map(t => `${t.name}(${t.currentHealth})`));

        // 4. Select the highest priority target
        const target = validTargets[0];
         console.log(`AI Attacker ${attacker.name}(${attacker.currentAttack}/${attacker.currentHealth}) chooses highest priority target: ${target.name}(${target.currentAttack || 'N/A'}/${target.currentHealth})`);

        // 5. Execute attack
        // Ensure the attacker can still attack (might have been destroyed by a previous effect this turn?)
        const currentAttackerState = findCardInstanceByUid(attacker.uid);
        if (!currentAttackerState || !currentAttackerState.canAttack) {
            console.log(`AI: Attacker ${attacker.name} is no longer valid or cannot attack before its turn in the loop.`);
            continue; // Skip attack if attacker became invalid
        }

        try {
             console.log(`AI: Calling executeAttack with Attacker UID: ${currentAttackerState.uid}, Target UID: ${target.uid}`);
            executeAttack(currentAttackerState.uid, target.uid); // Use imported function from gameManager
             console.log(`AI: Attack initiated successfully by ${currentAttackerState.name} on ${target.name}.`);
            // executeAttack is responsible for setting canAttack = false on the attacker instance in gameState
        } catch (error) {
             console.error(`AI: Error executing attack for ${currentAttackerState.name} on ${target.name}:`, error);
        }
        // Loop continues to the next available attacker from the initial list.
    }

    console.log(`AI: ${aiPlayer.id} finished attack decisions for this turn's logic run.`);
}

// function aiDecideAndEvolve(aiPlayer, gameStateRef) { ... } // Placeholder removed


// --- AI Mulligan Logic ---
export function performAIMulligan(aiPlayerState) {
    if (!aiPlayerState || !aiPlayerState.hand) {
        console.error("AI Mulligan Error: Invalid AI player state provided.");
        // Call handleMulligan with empty array to signal completion without replacement
        handleMulligan(aiPlayerState?.id || 'player2', []);
        return;
    }

    console.log(`AI [${aiPlayerState.id}]: Performing mulligan decision... Hand:`, aiPlayerState.hand.map(c => `${c.name}(${c.cost})`));

    // Simple Strategy: Replace all cards with cost > 3
    const costThreshold = 3;
    const uidsToReplace = aiPlayerState.hand
        .filter(card => card && card.cost > costThreshold)
        .map(card => card.uid);

    console.log(`AI [${aiPlayerState.id}]: Decided to replace ${uidsToReplace.length} card(s) with cost > ${costThreshold}. UIDs:`, uidsToReplace);

    // Call the handleMulligan function from gameManager
    try {
        handleMulligan(aiPlayerState.id, uidsToReplace);
        console.log(`AI [${aiPlayerState.id}]: Mulligan request submitted.`);
    } catch (error) {
        console.error(`AI [${aiPlayerState.id}]: Error calling handleMulligan:`, error);
        // Failsafe: Still signal completion even if call fails
        handleMulligan(aiPlayerState.id, []);
    }
}