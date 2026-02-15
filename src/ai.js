// src/ai.js - AI 对手逻辑
//
// 相关文档:
// - AI决策系统: 文档/05-游戏系统/05-AI决策系统.md
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
//

// getCardDefinition is obsolete. Card instances now contain all necessary data.
// 导入依赖
// MODIFIED: Added getPlayerState, getCurrentPlayerId, findCardInstanceByUid, executeAttack
// MODIFIED: Added endTurnRequest, removed handleMulligan/executeAttack
import { playCard, getPlayerState, getCurrentPlayerId, findCardInstanceByUid, sendGameAction, evolveCard, submitMulligan, endTurnRequest } from './gameManager.js';
// import { endTurn } from './turnManager.js'; // Obsolete: Removed import of local endTurn

// TODO: Move constants like MAX_FIELD_SIZE to a dedicated constants.js file
const MAX_FIELD_SIZE = 5; // Define locally for now

function hasKeywordIgnoreCase(card, keywordName) {
    if (!card || !keywordName) return false;
    const keywords = card.keywords ?? card.Keywords ?? [];
    if (!Array.isArray(keywords)) return false;
    const normalizedTarget = String(keywordName).trim().toLowerCase();
    return keywords.some(keyword => String(keyword ?? '').trim().toLowerCase() === normalizedTarget);
}


// --- 主 AI 逻辑入口 ---
// 这个函数应该在轮到 AI 时被 turnManager 调用
export function runAILogic(currentGameState) { // 接收当前游戏状态
    if (!currentGameState || !currentGameState.players) {
        console.error("AI Error: Missing game state or players for decision making.");
        setTimeout(endTurnRequest, 500); // Failsafe: Request server to end turn
        return;
    }

    // 【修复】动态查找AI玩家，而不是硬编码'player2'
    const aiPlayer = currentGameState.players.find(p => p && p.isAI);
    if (!aiPlayer) {
        console.error("AI Error: Could not find any player marked as AI in the current game state.");
        setTimeout(endTurnRequest, 500); // Failsafe: Request server to end turn
        return;
    }

    // 确保现在是AI的回合
    if (currentGameState.currentPlayerId !== aiPlayer.id) {
        // This case should not happen if called correctly, but as a safeguard:
        // We don't end the turn here, as it's not our turn to end.
        return;
    }

    // --- 逐步执行 AI 动作 ---
    try {
        // 1. 出牌决策
        // Pass gameState for context if needed by future, more complex decisions
        aiDecideAndPlayCard(aiPlayer, currentGameState);

        // 2. 进化决策 (V0.1)
        aiDecideAndEvolve(aiPlayer, currentGameState); // Call the new evolve logic function

        // 3. 攻击决策
        aiDecideAndAttack(aiPlayer, currentGameState); // Call the attack logic function

        // 4. 结束回合 (总是在最后执行)
        // Add a short delay before ending turn to simulate thinking/allow UI updates
        setTimeout(endTurnRequest, 1000); // Request server to end turn

    } catch (error) {
        console.error("AI Error during logic execution:", error);
        // Failsafe: 即使出错，也要确保能结束回合，避免卡死
        setTimeout(endTurnRequest, 500); // Failsafe: Request server to end turn
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
        return; // 战场已满，无法出随从
    }

    // 筛选可出的随从
    const playableFollowers = aiPlayer.hand.filter(card =>
        card &&
        card.type === 'Follower' &&
        card.cost <= aiPlayer.pp
    );

    if (playableFollowers.length === 0) {
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
        try {
            // playCard is imported and assumed to handle the logic based on card UID and current game state correctly
            playCard(cardToPlay.uid);
            // No need to render here, playCard handles it.
        } catch(error) {
             console.error(`AI PlayCard Error: Failed to execute playCard for ${cardToPlay.name} (UID: ${cardToPlay.uid}):`, error);
        }
    } else {
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

    // 1. 检查全局进化权限和 EP
    if (!aiPlayer.canEvolveThisTurn) {
        return false;
    }
    if (aiPlayer.ep < 1) { // 假设进化固定消耗 1 EP
        return false;
    }

    let targetFollower = null;

    // 2. 遍历战场寻找第一个符合条件的随从
    for (const cardInstance of aiPlayer.field) {
        // 确保 cardInstance 有效
        if (!cardInstance) continue;


        // 3. 检查资格
        if (!cardInstance.isEvolved) {
            // Check the canEvolve property directly on the instance, which is hydrated from the DTO.
            if (cardInstance.canEvolve === true) {
                // 4. 选择目标 (第一个符合条件的)
                targetFollower = cardInstance;
                break; // 找到目标，停止搜索
            } else {
            }
        } else {
        }
    }

    // 5. 如果找到目标，执行进化
    if (targetFollower) {
        try {
            evolveCard(targetFollower.uid); // 使用导入的函数
            // evolveCard 内部会处理 EP 消耗和 canEvolveThisTurn 标志的设置
            return true; // 表示尝试了进化
        } catch (error) {
            console.error(`AI [${aiPlayer.id}]: Error calling evolveCard for ${targetFollower.name} (UID: ${targetFollower.uid}):`, error);
            return false; // 调用失败，返回 false
        }
    } else {
        // 6. 未找到目标
        return false;
    }
}
// --- 新增: AI 攻击决策函数 (V0.1) ---
function aiDecideAndAttack(aiPlayer, currentGameState) {

    const opponentId = currentGameState.players.find(p => p.id !== aiPlayer.id)?.id;
    if (!opponentId) {
        console.error("AI Attack Error: Cannot find opponent ID.");
        return;
    }
    // Note: getPlayerState needs to be called potentially multiple times if state changes mid-attack sequence
    // For V0.1, we'll fetch opponent state once per attacker for simplicity.

    // 1. Find AI attackers that haven't attacked yet this turn (canAttack is true)
    const availableAttackers = aiPlayer.field.filter(card => card && card.canAttack);
    if (availableAttackers.length === 0) {
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
        const opponentFollowers = currentOpponentPlayer.field.filter(
            f => f
                && (f.type === 'Follower' || f.type === 'Follower_Evolved')
                && !hasKeywordIgnoreCase(f, 'Ambush')
                && !hasKeywordIgnoreCase(f, 'Pressure')
        );
        const opponentLeader = currentOpponentPlayer.leaderCard; // Leader instance directly from player state

        // 2. Determine valid targets considering Ward
        const hasWard = opponentFollowers.some(f => hasKeywordIgnoreCase(f, 'Ward'));
        let validTargets = [];

        if (hasWard) {
            validTargets = opponentFollowers.filter(f => hasKeywordIgnoreCase(f, 'Ward'));
        } else {
            validTargets = [...opponentFollowers]; // Add all followers
            if (opponentLeader && opponentLeader.currentHealth > 0) { // Ensure leader isn't already defeated
                 validTargets.push(opponentLeader);
            }
        }

        if (validTargets.length === 0) {
            continue; // Skip to the next attacker
        }

        // 3. Sort targets by priority: Ward > Lowest HP Follower > Leader
        validTargets.sort((a, b) => {
            const aIsWard = hasKeywordIgnoreCase(a, 'Ward');
            const bIsWard = hasKeywordIgnoreCase(b, 'Ward');
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

        // 4. Select the highest priority target
        const target = validTargets[0];

        // 5. Execute attack
        // Ensure the attacker can still attack (might have been destroyed by a previous effect this turn?)
        const currentAttackerState = findCardInstanceByUid(attacker.uid);
        if (!currentAttackerState || !currentAttackerState.canAttack) {
            continue; // Skip attack if attacker became invalid
        }

        try {
            const attackActionData = {
                actionType: "Attack",
                attackerUid: currentAttackerState.uid,
                targetUid: target.uid
            };
            sendGameAction(attackActionData); // Use imported function from gameManager
            // Server will handle attack execution and update canAttack status via events.
            // No immediate state change here.
        } catch (error) {
             console.error(`AI: Error executing attack for ${currentAttackerState.name} on ${target.name}:`, error);
        }
        // Loop continues to the next available attacker from the initial list.
    }

}

// function aiDecideAndEvolve(aiPlayer, gameStateRef) { ... } // Placeholder removed


// --- AI Mulligan Logic ---
export function performAIMulligan(aiPlayerState) {
    if (!aiPlayerState || !aiPlayerState.hand) {
        console.error("AI Mulligan Error: Invalid AI player state provided.");
        // Call handleMulligan with empty array to signal completion without replacement
        // Call submitMulligan with empty array to signal completion without replacement (via SignalR)
        submitMulligan([]); // Use imported submitMulligan
        return;
    }


    // Simple Strategy: Replace all cards with cost > 3
    const costThreshold = 3;
    const uidsToReplace = aiPlayerState.hand
        .filter(card => card && card.cost > costThreshold)
        .map(card => card.uid);


    // Call the handleMulligan function from gameManager
    try {
        submitMulligan(uidsToReplace); // Use imported submitMulligan
    } catch (error) {
        console.error(`AI [${aiPlayerState.id}]: Error calling handleMulligan:`, error);
        // Failsafe: Still signal completion even if call fails
        submitMulligan([]); // Use imported submitMulligan as failsafe
    }
}
