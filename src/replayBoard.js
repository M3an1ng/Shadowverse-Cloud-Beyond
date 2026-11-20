import { initializeSpectator, updateFullGameState, handleAbilityTriggered, handleCardMoved, handleHealthChanged,
    handleStatChange, handleUnitEvolved } from './gameManager.js';
import { clearBattlePresentation, notifyCardPlayed, notifyShieldRemoved, playAttackLunge, playUnitEvolutionEffect,
    showDamageNumber, showHealNumber, showFusionAnimation, showRevealOverlay, showTurnStartBanner } from './ui.js';

function present({ method, payload: p, cast }, reduced) {
    switch (method) {
        case 'DamageDealt': return showDamageNumber(p.targetUid, p.damageAmount);
        case 'HealApplied': return showHealNumber(p.targetUid, p.healAmount);
        case 'KeywordRemoved':
            if (p.keyword?.toLowerCase() === 'shield') notifyShieldRemoved(p.cardInstanceId);
            return;
    }
    if (reduced) return;
    switch (method) {
        case 'AttackDeclared': return playAttackLunge(p.attackerUid, p.targetUid);
        case 'CardPlayed': return notifyCardPlayed(p.playedCardInstance, p.sourcePlayerId, p.paidCost, p.playMode);
        case 'CardMoved':
            // Seeking past CardPlayed skips its hand position and cast mode. Restore them before the landing.
            if (cast) notifyCardPlayed(cast.playedCardInstance, cast.sourcePlayerId, cast.paidCost, cast.playMode, true);
            return handleCardMoved(p);
        case 'UnitEvolved': return handleUnitEvolved(p);
        case 'HealthChanged': return handleHealthChanged(p);
        case 'StatChange': return handleStatChange(p);
        case 'AbilityTriggered': return handleAbilityTriggered(p);
        case 'CardRevealed': return showRevealOverlay(p.revealedCard, { playerId: p.revealingPlayerId, handIndex: p.handIndex });
        case 'Fusion': return showFusionAnimation(p.sourcePlayerId, p.fusedCardInstanceIds);
        case 'TurnStarted': return showTurnStartBanner(p.sourcePlayerId);
    }
}

// Some older recordings only contain committed events for part of an action.
function presentMissingEffect(step, effects, reduced) {
    const has = method => effects.some(effect => effect.method === method);
    if (step.kind === 'Damage' && step.amount != null && !has('DamageDealt')) return showDamageNumber(step.targetId, step.amount);
    if (step.kind === 'Heal' && !has('HealApplied')) return showHealNumber(step.targetId, step.amount);
    if (reduced) return;
    if (step.kind === 'Attack' && !has('AttackDeclared')) return playAttackLunge(step.cardId, step.targetId);
    if ((step.kind === 'Evolve' || step.kind === 'SuperEvolve') && !has('UnitEvolved'))
        return playUnitEvolutionEffect(step.cardId, step.kind === 'SuperEvolve');
    if (step.kind === 'TurnStart' && !has('TurnStarted')) showTurnStartBanner(step.owner);
}

export function mountReplayBoard() {
    const frame = window.frameElement;
    if (!frame?.hasAttribute('data-replay-board')) return;
    document.body.classList.add('replay-board', 'portrait-guard-dismissed');
    document.getElementById('game-page').classList.add('portrait-guard-dismissed');
    const motion = window.matchMedia('(prefers-reduced-motion: reduce)');
    let gameId, generation = 0, pendingState, cancelPending;

    function hydrate(snapshot) {
        if (!updateFullGameState(snapshot, { resetViewEpoch: true })) throw new Error('无法显示这局的战场。');
    }

    async function render(state, step, viewer, forward) {
        const current = ++generation;
        cancelPending?.();
        cancelPending = null;
        if (gameId !== state.gameId) {
            gameId = state.gameId;
            initializeSpectator(gameId, { viewingPlayerId: viewer });
        }
        if (!forward || pendingState) {
            document.body.classList.add('replay-seeking');
            clearBattlePresentation();
            // Fast single-stepping can interrupt a card in flight. Start the next action on its settled board.
            if (forward && pendingState) hydrate(pendingState);
            pendingState = null;
        }
        const snapshot = { ...state, viewingPlayerId: viewer, turnStartedAtUtc: null, turnEndsAtUtc: null };
        if (forward) {
            document.body.classList.toggle('replay-seeking', motion.matches);
            pendingState = snapshot;
            const cancelled = new Promise(resolve => { cancelPending = resolve; });
            const effects = step.effects ?? [];
            for (const effect of effects) {
                const barrier = present(effect, motion.matches);
                if (barrier) await Promise.race([barrier, cancelled]);
                if (current !== generation) return;
            }
            const barrier = presentMissingEffect(step, effects, motion.matches);
            if (barrier) await Promise.race([barrier, cancelled]);
            if (current !== generation) return;
        }
        pendingState = null;
        cancelPending = null;
        hydrate(snapshot);
    }
    window.addEventListener('pagehide', () => {
        generation++;
        cancelPending?.();
        clearBattlePresentation();
    }, { once: true });
    frame.dispatchEvent(new CustomEvent('replay-board-ready', { detail: render }));
}
