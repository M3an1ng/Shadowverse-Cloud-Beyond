// src/ui/battleCardMotion.js
// 对战界面卡牌动效原语引擎 (v2.2 — 测试台与实战页双端共享)
// 职责：提供与具体状态机和页面选择器解耦的快照采集、WAAPI 独立属性 FLIP、跨区轨迹交接、
//       全卡种无遮罩悬停展示（随从/护符/法术均先悬停再落位/生效）、接触式攻击冲撞与致死交接、
//       同波受击震动/浮字、进化爆发与语义化退场（破坏/消失/回手）。

import { alternateCastTimings, castCardPose, paintAlternateCastEffect } from './alternateCastEffect.js';

const STRIP_CLONE_CLASSES = [
    'is-selected',
    'is-targetable',
    'is-drag-over',
    'can-attack',
    'can-rush',
    'can-act',
    'is-playable',
    'is-hand-dragging-origin',
    'selected-attacker',
    'targetable',
    'valid-target',
    'targetable-spell',
    'targetable-restricted',
    'targeting-spell',
    'choice-selected',
    'choice-hand-hover',
    'attack-ready-all',
    'attack-ready-follower',
    'act-ready',
    'playable',
    'requires-target',
    'no-valid-targets',
    'can-play-without-target',
    'hand-card-floated',
    'hand-drag-origin',
    'hand-play-committed-source',
    'card-fade-enter',
    'hand-card-fade-enter-down',
    'card-fade-exit',
    'hand-card-fade-exit-up',
    'card-fade-move'
];

function normalizeMotionUid(uid) {
    return String(uid ?? '').trim().toLowerCase();
}

function safeAnimate(el, keyframes, options) {
    if (!el || typeof el.animate !== 'function') {
        return { finished: Promise.resolve(), cancel() {} };
    }
    return el.animate(keyframes, options);
}

export function prefersReducedMotion() {
    return typeof window !== 'undefined'
        && typeof window.matchMedia === 'function'
        && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

export function toStageRect(viewportRect, stageRect) {
    if (!viewportRect || !stageRect) return null;
    const left = viewportRect.left - stageRect.left;
    const top = viewportRect.top - stageRect.top;
    const width = viewportRect.width;
    const height = viewportRect.height;
    return {
        left,
        top,
        width,
        height,
        cx: left + width / 2,
        cy: top + height / 2
    };
}

export function createCleanCardClone(sourceEl, explicitWidth = null, explicitHeight = null) {
    if (!sourceEl) return null;
    const clone = sourceEl.cloneNode(true);
    clone.removeAttribute('id');
    clone.removeAttribute('data-select-uid');
    clone.removeAttribute('data-uid');
    clone.removeAttribute('data-render-key');
    clone.removeAttribute('data-attack-target');
    clone.querySelectorAll?.('[id]')?.forEach(node => node.removeAttribute('id'));
    clone.querySelectorAll?.(
        '.bui-card-action-tag, .bui-mirage-hover-ops, .mirage-field-controls, ' +
        '.use-button, .evolve-button, .super-evolve-button, .act-button, .fusion-button'
    )?.forEach(node => node.remove());

    STRIP_CLONE_CLASSES.forEach(cls => clone.classList.remove(cls));

    const w = explicitWidth || sourceEl.offsetWidth || 96;
    const h = explicitHeight || sourceEl.offsetHeight || 132;
    clone.style.position = 'relative';
    clone.style.left = '0';
    clone.style.top = '0';
    clone.style.width = `${w}px`;
    clone.style.height = `${h}px`;
    clone.style.margin = '0';
    clone.style.transform = 'none';
    clone.style.translate = 'none';
    clone.style.scale = 'none';
    clone.style.rotate = 'none';
    clone.style.transition = 'none';
    clone.style.pointerEvents = 'none';
    clone.style.visibility = 'visible';
    clone.style.opacity = '1';
    clone.style.setProperty('--fan-offset', '0');
    clone.style.setProperty('--fan-abs', '0');
    return clone;
}

export function computeContactVector(attackerRect, targetRect, overlapPx = 14) {
    const dx = targetRect.cx - attackerRect.cx;
    const dy = targetRect.cy - attackerRect.cy;
    const dist = Math.hypot(dx, dy);
    if (dist < 1) {
        return { ux: 0, uy: -1, contactX: 0, contactY: 0, slashX: targetRect.cx, slashY: targetRect.cy };
    }
    const ux = dx / dist;
    const uy = dy / dist;
    const rAttacker = Math.min(attackerRect.width, attackerRect.height) * 0.46;
    const rTarget = Math.min(targetRect.width, targetRect.height) * 0.44;
    const travelDist = Math.max(12, dist - (rAttacker + rTarget - overlapPx));
    return {
        ux,
        uy,
        contactX: ux * travelDist,
        contactY: uy * travelDist,
        slashX: targetRect.cx - ux * (rTarget * 0.28),
        slashY: targetRect.cy - uy * (rTarget * 0.28)
    };
}

const DEFAULT_TESTBED_ZONE_SPECS = [
    { zone: 'opp:field', selector: '.bui-field-zone.is-upper .card[data-select-uid]' },
    { zone: 'self:field', selector: '.bui-field-zone.is-lower .card[data-select-uid]' },
    { zone: 'self:hand', selector: '.bui-self-hand-bar .card[data-select-uid]' },
    { zone: 'emblem', selector: '.bui-emblem-w-grid .bui-crest-slot.is-filled[data-select-uid]' },
    { zone: 'leader', selector: '.bui-rpg-hud[data-select-uid]' }
];

export function createBattleMotionController({
    getStageElement,
    getTransientLayer,
    uidAttribute = 'data-select-uid',
    zoneSpecs = DEFAULT_TESTBED_ZONE_SPECS,
    resolveEntityElement = null,
    resolveOppHandElement = stageEl => stageEl?.querySelector('.bui-opp-hand-bar'),
    resolveSelfDeckElement = stageEl => stageEl?.querySelector('.bui-corner-pod.is-bottom-left .bui-var-item'),
    resolveOppDeckElement = stageEl => stageEl?.querySelector('.bui-corner-pod.is-top-right .bui-var-item')
}) {
    let generation = 1;
    const activeAnimations = new Set();
    const activeTimers = new Set();
    const activeAlternateCasts = new Set();
    const hiddenRealElements = new Map();
    const hiddenEntityUidCounts = new Map();
    const flipAnimationsByElement = new WeakMap();
    const activeLungesByUid = new Map();

    function trackAnimation(anim) {
        if (!anim) return null;
        activeAnimations.add(anim);
        const cleanup = () => activeAnimations.delete(anim);
        anim.finished?.then(cleanup, cleanup);
        return anim;
    }

    function scheduleTimer(fn, delayMs) {
        const token = generation;
        const id = window.setTimeout(() => {
            activeTimers.delete(id);
            if (token !== generation) return;
            fn();
        }, Math.max(0, delayMs));
        activeTimers.add(id);
        return id;
    }

    function queryBoardEntityElement(uid) {
        const stageEl = getStageElement?.();
        if (!stageEl || !uid) return null;
        if (typeof resolveEntityElement === 'function') {
            return resolveEntityElement(stageEl, uid);
        }
        return stageEl.querySelector(
            `.bui-field-zone .card[${uidAttribute}="${uid}"], ` +
            `.bui-self-hand-bar .card[${uidAttribute}="${uid}"], ` +
            `.bui-emblem-w-grid .bui-crest-slot[${uidAttribute}="${uid}"], ` +
            `.bui-rpg-hud[${uidAttribute}="${uid}"]`
        );
    }

    function hideRealElement(el, uid = null) {
        if (uid) {
            const key = normalizeMotionUid(uid);
            if (key) {
                hiddenEntityUidCounts.set(key, (hiddenEntityUidCounts.get(key) || 0) + 1);
            }
        }
        if (!el) return;
        if (!hiddenRealElements.has(el)) {
            hiddenRealElements.set(el, el.style.visibility || '');
        }
        el.style.visibility = 'hidden';
    }

    function restoreRealElement(el, uid = null) {
        let shouldRemainHidden = false;
        if (uid) {
            const key = normalizeMotionUid(uid);
            if (key) {
                const nextCount = Math.max(0, (hiddenEntityUidCounts.get(key) || 0) - 1);
                if (nextCount > 0) {
                    hiddenEntityUidCounts.set(key, nextCount);
                    shouldRemainHidden = true;
                } else {
                    hiddenEntityUidCounts.delete(key);
                }
            }
        }
        if (shouldRemainHidden) return;

        const latestEl = (uid ? queryBoardEntityElement(uid) : null) || el;
        if (el && hiddenRealElements.has(el)) {
            const prev = hiddenRealElements.get(el);
            hiddenRealElements.delete(el);
            if (el.isConnected) {
                el.style.visibility = prev || '';
            }
        }
        if (latestEl && latestEl !== el) {
            const prev = hiddenRealElements.get(latestEl);
            hiddenRealElements.delete(latestEl);
            if (latestEl.isConnected) {
                latestEl.style.visibility = prev || '';
            }
        }
    }

    // 当实战页通过 replaceWith / replaceChildren 重建某个正在飞行或突进的 UID 节点时，保持其隐藏占位，杜绝双影闪烁
    function syncEntityElementVisibility(uid, element) {
        if (!uid || !element) return;
        const key = normalizeMotionUid(uid);
        if (!key || !hiddenEntityUidCounts.has(key)) return;
        if (!hiddenRealElements.has(element)) {
            hiddenRealElements.set(element, element.style.visibility || '');
        }
        element.style.visibility = 'hidden';
    }

    function isEntityHiddenByCarrier(uid) {
        const key = normalizeMotionUid(uid);
        return Boolean(key && hiddenEntityUidCounts.has(key));
    }

    function cancelAll() {
        generation += 1;
        activeAlternateCasts.forEach(cancel => cancel());
        activeTimers.forEach(id => window.clearTimeout(id));
        activeTimers.clear();
        activeAnimations.forEach(anim => {
            try {
                anim.cancel();
            } catch {
                // ignore
            }
        });
        activeAnimations.clear();
        activeLungesByUid.clear();
        hiddenEntityUidCounts.clear();
        for (const [el, prevVis] of hiddenRealElements.entries()) {
            if (el.isConnected) {
                el.style.visibility = prevVis || '';
            }
        }
        hiddenRealElements.clear();
        const layer = getTransientLayer?.();
        if (layer) {
            layer.innerHTML = '';
        }
    }

    function getGeneration() {
        return generation;
    }

    function isCurrent(token) {
        return token === generation;
    }

    function captureElementSnapshot(el, uid = null, zone = 'unknown') {
        const stageEl = getStageElement?.();
        if (!stageEl || !el) return null;
        const stageRect = stageEl.getBoundingClientRect();
        const vpRect = el.getBoundingClientRect();
        const rect = toStageRect(vpRect, stageRect);
        if (!rect || rect.width <= 0 || rect.height <= 0) return null;

        const resolvedUid = uid || el.getAttribute?.(uidAttribute) || el.dataset?.uid || el.dataset?.selectUid || '';
        const atkText = el.querySelector?.('.attack')?.textContent;
        const hpText = el.querySelector?.('.health, .bui-hp-num, .hp-value')?.textContent;
        const costText = el.querySelector?.('.cost span')?.textContent;
        const cdText = el.querySelector?.('.countdown, .bui-crest-cd, .emblem-countdown')?.textContent;
        const unscaledW = el.offsetWidth || Math.round(rect.width);
        const unscaledH = el.offsetHeight || Math.round(rect.height);
        const isCard = el.classList?.contains('card') === true;

        return {
            uid: String(resolvedUid),
            zone,
            rect,
            unscaledW,
            unscaledH,
            atk: atkText != null ? Number(atkText) : null,
            hp: hpText != null ? Number(hpText) : null,
            cost: costText != null ? costText.trim() : null,
            countdown: cdText != null ? Number(cdText) : null,
            isCard,
            cloneEl: isCard ? createCleanCardClone(el, unscaledW, unscaledH) : null
        };
    }

    function getAnchorRects() {
        const stageEl = getStageElement?.();
        if (!stageEl) return null;
        const stageRect = stageEl.getBoundingClientRect();

        const oppHandEl = resolveOppHandElement?.(stageEl);
        const oppHandVp = oppHandEl?.getBoundingClientRect?.();
        const oppHandRect = (oppHandVp && oppHandVp.width > 0)
            ? toStageRect(oppHandVp, stageRect)
            : {
                left: stageRect.width * 0.5 - 45,
                top: 24,
                width: 90,
                height: 41,
                cx: stageRect.width * 0.5,
                cy: 44
            };

        const selfDeckEl = resolveSelfDeckElement?.(stageEl);
        const selfDeckVp = selfDeckEl?.getBoundingClientRect?.();
        const selfDeckRect = (selfDeckVp && selfDeckVp.width > 0)
            ? toStageRect(selfDeckVp, stageRect)
            : {
                left: 24,
                top: stageRect.height - 44,
                width: 60,
                height: 28,
                cx: 54,
                cy: stageRect.height - 30
            };

        const oppDeckEl = resolveOppDeckElement?.(stageEl);
        const oppDeckVp = oppDeckEl?.getBoundingClientRect?.();
        const oppDeckRect = (oppDeckVp && oppDeckVp.width > 0)
            ? toStageRect(oppDeckVp, stageRect)
            : {
                left: stageRect.width - 120,
                top: 20,
                width: 60,
                height: 28,
                cx: stageRect.width - 90,
                cy: 34
            };

        return { stageRect, oppHandRect, selfDeckRect, oppDeckRect };
    }

    function captureSnapshot() {
        const stageEl = getStageElement?.();
        if (!stageEl) return null;
        const anchors = getAnchorRects();
        if (!anchors) return null;
        const entities = new Map();

        for (const spec of zoneSpecs) {
            stageEl.querySelectorAll(spec.selector).forEach(el => {
                const uid = el.getAttribute(uidAttribute);
                if (!uid) return;
                const snap = captureElementSnapshot(el, uid, spec.zone);
                if (snap) {
                    entities.set(uid, snap);
                }
            });
        }

        return {
            stageRect: anchors.stageRect,
            entities,
            oppHandRect: anchors.oppHandRect,
            selfDeckRect: anchors.selfDeckRect,
            oppDeckRect: anchors.oppDeckRect
        };
    }

    // 1. 区域内与跨区域 FLIP + 属性跳字
    function runPostRenderFlip(prevSnapshot, options = {}) {
        const stageEl = getStageElement?.();
        const layer = getTransientLayer?.();
        if (!stageEl || !prevSnapshot) return;

        const reduced = prefersReducedMotion();
        const handoffs = options.handoffs || new Map();
        const skipFlipUids = options.skipFlipUids || new Set();
        const nextSnapshot = captureSnapshot();
        if (!nextSnapshot) return;

        let enterStaggerIndex = 0;

        for (const [uid, nextInfo] of nextSnapshot.entities.entries()) {
            if (skipFlipUids.has(uid)) continue;
            const el = queryBoardEntityElement(uid);
            if (!el) continue;

            if (handoffs.has(uid)) {
                const handoff = handoffs.get(uid);
                const sourceRect = handoff.sourceRect;
                if (!reduced && sourceRect && layer && nextInfo.isCard) {
                    const staggerDelay = (handoff.staggerIndex ?? 0) * 45;
                    runCrossZoneCarrierFlight({
                        targetUid: uid,
                        targetEl: el,
                        targetRect: nextInfo.rect,
                        targetW: nextInfo.unscaledW,
                        targetH: nextInfo.unscaledH,
                        sourceRect,
                        delayMs: staggerDelay,
                        durationMs: handoff.durationMs ?? 240,
                        pulseTone: handoff.pulseTone ?? 'cyan'
                    });
                }
                continue;
            }

            const prevInfo = prevSnapshot.entities.get(uid);

            if (prevInfo && prevInfo.zone === nextInfo.zone && nextInfo.isCard) {
                const dx = prevInfo.rect.cx - nextInfo.rect.cx;
                const dy = prevInfo.rect.cy - nextInfo.rect.cy;
                if (!reduced && Math.hypot(dx, dy) > 0.75) {
                    const prevAnim = flipAnimationsByElement.get(el);
                    if (prevAnim) {
                        try { prevAnim.cancel(); } catch { /* ignore */ }
                    }
                    const anim = trackAnimation(safeAnimate(el,
                        [
                            { translate: `${dx.toFixed(1)}px ${dy.toFixed(1)}px` },
                            { translate: '0px 0px' }
                        ],
                        {
                            duration: 220,
                            easing: 'cubic-bezier(0.22, 1, 0.36, 1)'
                        }
                    ));
                    if (anim) {
                        flipAnimationsByElement.set(el, anim);
                    }
                }
            } else if (!prevInfo && nextInfo.isCard && !reduced) {
                const delayMs = Math.min(enterStaggerIndex * 45, 220);
                enterStaggerIndex += 1;
                if (nextInfo.zone === 'self:hand') {
                    playHandRightSlideIn({
                        targetUid: uid,
                        targetEl: el,
                        delayMs,
                        durationMs: 240,
                        pulseTone: 'cyan'
                    });
                } else {
                    trackAnimation(safeAnimate(el,
                        [
                            { opacity: 0, translate: '0px -16px', scale: '0.92' },
                            { opacity: 1, translate: '0px 0px', scale: '1' }
                        ],
                        {
                            duration: 220,
                            delay: delayMs,
                            easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
                            fill: 'backwards'
                        }
                    ));
                }
            }

            if (prevInfo && !reduced) {
                if (prevInfo.atk !== null && nextInfo.atk !== null && prevInfo.atk !== nextInfo.atk) {
                    const atkEl = el.querySelector('.attack');
                    animateStatPop(atkEl, nextInfo.atk > prevInfo.atk ? 'buff' : 'debuff');
                }
                if (prevInfo.hp !== null && nextInfo.hp !== null && prevInfo.hp !== nextInfo.hp) {
                    const hpEl = el.querySelector('.health, .bui-hp-num, .hp-value');
                    animateStatPop(hpEl, nextInfo.hp > prevInfo.hp ? 'buff' : 'damage');
                }
                if (prevInfo.countdown !== null && nextInfo.countdown !== null && prevInfo.countdown !== nextInfo.countdown) {
                    const cdEl = el.querySelector('.countdown, .bui-crest-cd, .emblem-countdown');
                    animateStatPop(cdEl, 'neutral');
                }
            }
        }
    }

    function runCrossZoneCarrierFlight({
        targetUid = null,
        targetEl,
        targetRect = null,
        targetW = null,
        targetH = null,
        sourceRect,
        delayMs = 0,
        durationMs = 240,
        pulseTone = 'cyan'
    }) {
        const stageEl = getStageElement?.();
        const layer = getTransientLayer?.();
        if (!stageEl || !layer || !targetEl || !sourceRect || prefersReducedMotion()) return;
        const token = generation;

        const stageRect = stageEl.getBoundingClientRect();
        const resolvedTargetRect = targetRect || toStageRect(targetEl.getBoundingClientRect(), stageRect);
        if (!resolvedTargetRect) return;

        const w = targetW || targetEl.offsetWidth || Math.round(resolvedTargetRect.width) || 96;
        const h = targetH || targetEl.offsetHeight || Math.round(resolvedTargetRect.height) || 132;

        const carrier = document.createElement('div');
        carrier.className = 'bui-motion-carrier';
        carrier.style.width = `${w}px`;
        carrier.style.height = `${h}px`;
        carrier.style.left = `${resolvedTargetRect.cx - w / 2}px`;
        carrier.style.top = `${resolvedTargetRect.cy - h / 2}px`;

        const clone = createCleanCardClone(targetEl, w, h);
        if (!clone) return;
        carrier.appendChild(clone);
        layer.appendChild(carrier);

        hideRealElement(targetEl, targetUid);

        const dx = sourceRect.cx - resolvedTargetRect.cx;
        const dy = sourceRect.cy - resolvedTargetRect.cy;
        const startScale = Math.max(0.32, Math.min(1.18, (sourceRect.width || w) / w));

        const anim = trackAnimation(safeAnimate(carrier,
            [
                {
                    translate: `${dx.toFixed(1)}px ${dy.toFixed(1)}px`,
                    scale: `${startScale.toFixed(3)}`,
                    opacity: startScale < 0.5 ? 0.65 : 0.96
                },
                {
                    translate: '0px -4px',
                    scale: '1.05',
                    opacity: 1,
                    offset: 0.78
                },
                {
                    translate: '0px 0px',
                    scale: '1',
                    opacity: 1
                }
            ],
            {
                duration: durationMs,
                delay: delayMs,
                easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
                fill: 'both'
            }
        ));

        const finalize = () => {
            carrier.remove();
            if (!isCurrent(token)) return;
            restoreRealElement(targetEl, targetUid);
            const liveTarget = (targetUid ? queryBoardEntityElement(targetUid) : null) || targetEl;
            if (liveTarget?.isConnected && pulseTone) {
                playLandingPulse(liveTarget, pulseTone);
            }
        };

        anim?.finished?.then(finalize, finalize);
    }

    function playHandRightSlideIn({
        targetUid = null,
        targetEl,
        delayMs = 0,
        durationMs = 240,
        pulseTone = 'cyan'
    }) {
        if (!targetEl || prefersReducedMotion()) return;
        const token = generation;
        const w = targetEl.offsetWidth || Math.round(targetEl.getBoundingClientRect().width) || 102;
        const slideOffsetX = Math.max(72, Math.round(w * 1.08));

        const fadeAnim = trackAnimation(safeAnimate(targetEl,
            [
                { opacity: 0, scale: '0.93' },
                { opacity: 1, scale: '1.02', offset: 0.78 },
                { opacity: 1, scale: '1' }
            ],
            {
                duration: durationMs,
                delay: delayMs,
                easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
                fill: 'backwards'
            }
        ));

        trackAnimation(safeAnimate(targetEl,
            [
                { translate: `${slideOffsetX}px 5px` },
                { translate: '-3px -1px', offset: 0.78 },
                { translate: '0px 0px' }
            ],
            {
                duration: durationMs,
                delay: delayMs,
                easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
                fill: 'backwards',
                composite: 'add'
            }
        ));

        const finalize = () => {
            if (!isCurrent(token)) return;
            const liveTarget = (targetUid ? queryBoardEntityElement(targetUid) : null) || targetEl;
            if (liveTarget?.isConnected && pulseTone) {
                playLandingPulse(liveTarget, pulseTone);
            }
        };

        fadeAnim?.finished?.then(finalize, finalize);
    }

    function playLandingPulse(cardEl, tone = 'cyan') {
        if (!cardEl || prefersReducedMotion()) return;
        const ring = document.createElement('div');
        ring.className = `bui-landing-pulse is-${tone}`;
        cardEl.appendChild(ring);
        const anim = trackAnimation(safeAnimate(ring,
            [
                { opacity: 0.95, scale: '0.86' },
                { opacity: 0, scale: '1.22' }
            ],
            { duration: 260, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' }
        ));
        anim?.finished?.then(() => ring.remove(), () => ring.remove());
    }

    function animateStatPop(badgeEl, tone = 'buff') {
        if (!badgeEl || prefersReducedMotion()) return;
        const glowColor = tone === 'buff'
            ? 'rgba(74, 222, 128, 0.85)'
            : (tone === 'damage' || tone === 'debuff')
                ? 'rgba(248, 113, 113, 0.88)'
                : 'rgba(250, 204, 21, 0.82)';
        trackAnimation(safeAnimate(badgeEl,
            [
                { scale: '1', filter: 'brightness(1)' },
                { scale: '1.48', filter: `brightness(1.35) drop-shadow(0 0 8px ${glowColor})`, offset: 0.38 },
                { scale: '1', filter: 'brightness(1)' }
            ],
            { duration: 260, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' }
        ));
    }

    // Keep overflow cards readable before the burst; the returned promise spaces consecutive burns.
    function playBurnCard(snapItem, { fastForward = false } = {}) {
        const layer = getTransientLayer?.();
        const stage = getStageElement?.();
        if (!layer || !stage || !snapItem?.cloneEl) return;

        const reduced = prefersReducedMotion();
        const duration = fastForward ? 240 : 700;
        const w = snapItem.unscaledW || 102;
        const h = snapItem.unscaledH || 140;
        const bounds = stage.getBoundingClientRect();
        const scale = Math.max(0.5, Math.min(1.1, (bounds.width - 32) / w, (bounds.height - 32) / h));
        const marginX = w / 2 * scale + 16;
        const marginTop = h / 2 * scale + 16;
        const marginBottom = h / 2 * scale + 16;
        const cx = Math.max(marginX, Math.min(bounds.width - marginX, snapItem.rect.cx));
        const cy = Math.max(marginTop, Math.min(bounds.height - marginBottom, snapItem.rect.cy));
        const shell = document.createElement('div');
        shell.className = 'bui-exit-clip-shell is-burned';
        Object.assign(shell.style, {
            width: `${w}px`, height: `${h}px`,
            left: `${cx - w / 2}px`, top: `${cy - h / 2}px`, scale: `${scale}`
        });
        const clone = snapItem.cloneEl.cloneNode(true);
        shell.appendChild(clone);
        layer.appendChild(shell);

        if (!reduced) {
            const ring = document.createElement('div');
            ring.className = 'bui-burn-ring';
            shell.appendChild(ring);
            trackAnimation(safeAnimate(ring, [
                { opacity: 0, scale: '0.95' },
                { opacity: 0, scale: '0.95', offset: 0.7 },
                { opacity: 1, scale: '1.08', offset: 0.8 },
                { opacity: 0, scale: '1.6' }
            ], { duration, easing: 'linear', fill: 'forwards' }));
            trackAnimation(safeAnimate(clone, [
                { filter: 'brightness(1)' },
                { filter: 'brightness(1)', offset: 0.7 },
                { filter: 'brightness(1.65) saturate(1.3)', offset: 0.8 },
                { filter: 'brightness(0.5) sepia(1)' }
            ], { duration, easing: 'linear', fill: 'forwards' }));
        }
        const frames = reduced ? [
            { opacity: 1 }, { opacity: 1, offset: 0.85 }, { opacity: 0 }
        ] : [
            { opacity: 0, scale: `${scale * 0.85}`, translate: '0px 8px' },
            { opacity: 1, scale: `${scale}`, translate: '0px 0px', offset: 0.12 },
            { opacity: 1, scale: `${scale}`, translate: '0px 0px', offset: 0.7 },
            { opacity: 1, scale: `${scale * 1.04}`, translate: '-3px 0px', offset: 0.79 },
            { opacity: 0.85, scale: `${scale}`, translate: '3px 3px', offset: 0.86 },
            { opacity: 0, scale: `${scale * 0.8}`, translate: '0px 22px' }
        ];
        const anim = trackAnimation(safeAnimate(shell, frames, { duration, easing: 'linear', fill: 'forwards' }));
        return anim.finished.then(() => shell.remove(), () => shell.remove());
    }

    // 2. 语义化退场克隆：破坏 (destroy) vs 消失 (vanish) vs 回手/回牌库 (return / deck)
    function spawnSemanticExitClone(snapItem, reason = 'destroy', overrideStagePos = null, destinationRect = null) {
        const layer = getTransientLayer?.();
        if (!layer || !snapItem || !snapItem.cloneEl) return;
        if (prefersReducedMotion()) return;

        const w = snapItem.unscaledW || 96;
        const h = snapItem.unscaledH || 132;
        const cx = overrideStagePos?.cx ?? snapItem.rect.cx;
        const cy = overrideStagePos?.cy ?? snapItem.rect.cy;

        const shell = document.createElement('div');
        shell.className = `bui-exit-clip-shell is-${reason}`;
        shell.style.width = `${w}px`;
        shell.style.height = `${h}px`;
        shell.style.left = `${cx - w / 2}px`;
        shell.style.top = `${cy - h / 2}px`;

        const clone = snapItem.cloneEl.cloneNode(true);
        shell.appendChild(clone);

        if (reason === 'vanish') {
            const laser = document.createElement('div');
            laser.className = 'bui-vanish-laser';
            shell.appendChild(laser);
            layer.appendChild(shell);

            const artEl = clone.querySelector('.card-art');
            if (artEl) {
                artEl.style.filter = 'hue-rotate(155deg) saturate(1.85) brightness(1.38)';
            }

            trackAnimation(safeAnimate(laser,
                [
                    { bottom: '0%', opacity: 1 },
                    { bottom: '100%', opacity: 0.9, offset: 0.88 },
                    { bottom: '102%', opacity: 0 }
                ],
                { duration: 280, easing: 'cubic-bezier(0.4, 0, 0.2, 1)', fill: 'forwards' }
            ));

            const anim = trackAnimation(safeAnimate(shell,
                [
                    { clipPath: 'inset(0% 0% 0% 0%)', scale: '1 1', opacity: 1 },
                    { clipPath: 'inset(0% 0% 100% 0%)', scale: '1.06 0.96', opacity: 0.25 }
                ],
                { duration: 280, easing: 'cubic-bezier(0.4, 0, 0.2, 1)', fill: 'forwards' }
            ));
            anim?.finished?.then(() => shell.remove(), () => shell.remove());
            return;
        }

        if ((reason === 'return' || reason === 'deck') && destinationRect) {
            layer.appendChild(shell);
            const dx = destinationRect.cx - cx;
            const dy = destinationRect.cy - cy;
            const anim = trackAnimation(safeAnimate(shell,
                [
                    { translate: '0px 0px', scale: '1', opacity: 1 },
                    { translate: `${dx.toFixed(1)}px ${dy.toFixed(1)}px`, scale: '0.32', opacity: 0.12 }
                ],
                { duration: 240, easing: 'cubic-bezier(0.22, 1, 0.36, 1)', fill: 'forwards' }
            ));
            anim?.finished?.then(() => shell.remove(), () => shell.remove());
            return;
        }

        layer.appendChild(shell);

        const artEl = clone.querySelector('.card-art');
        if (artEl) {
            artEl.style.filter = 'grayscale(0.92) contrast(1.35) brightness(0.72)';
        }

        const anim = trackAnimation(safeAnimate(shell,
            [
                { translate: '0px 0px', scale: '1', opacity: 1 },
                { translate: '-5px 2px', scale: '1.03', opacity: 0.96, offset: 0.18 },
                { translate: '5px 5px', scale: '0.96', opacity: 0.82, offset: 0.36 },
                { translate: '0px 18px', scale: '0.80', opacity: 0 }
            ],
            { duration: 300, easing: 'cubic-bezier(0.32, 0, 0.67, 0)', fill: 'forwards' }
        ));
        anim?.finished?.then(() => shell.remove(), () => shell.remove());
    }

    // 3. 同波同步受击反馈：中心爆裂浮字 + .card-art 受击震动闪白 + 护盾碎裂环 + 撞击十字星芒
    function triggerWaveHits(hits = [], prevSnapshot = null) {
        const stageEl = getStageElement?.();
        const layer = getTransientLayer?.();
        if (!stageEl || !layer || !Array.isArray(hits) || hits.length === 0) return;

        const stageRect = stageEl.getBoundingClientRect();
        const reduced = prefersReducedMotion();

        hits.forEach(hit => {
            const targetEl = queryBoardEntityElement(hit.targetUid);
            const fallbackRect = hit.fallbackRect ?? prevSnapshot?.entities?.get(hit.targetUid)?.rect ?? null;
            const rect = targetEl
                ? toStageRect(targetEl.getBoundingClientRect(), stageRect)
                : fallbackRect;
            if (!rect || rect.width <= 0) return;

            const isHeal = Number(hit.heal) > 0;
            const amount = isHeal ? Number(hit.heal) : Number(hit.damage ?? 0);

            if (!isHeal && !reduced) {
                const impactBurst = document.createElement('div');
                impactBurst.className = `bui-impact-burst ${hit.shieldBroken ? 'is-shield-break' : ''}`;
                impactBurst.style.left = `${rect.cx}px`;
                impactBurst.style.top = `${rect.cy}px`;
                layer.appendChild(impactBurst);
                const burstAnim = trackAnimation(safeAnimate(impactBurst,
                    [
                        { opacity: 1, scale: '0.45', rotate: '-12deg' },
                        { opacity: 0.95, scale: '1.15', rotate: '0deg', offset: 0.45 },
                        { opacity: 0, scale: '1.42', rotate: '8deg' }
                    ],
                    { duration: 210, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' }
                ));
                burstAnim?.finished?.then(() => impactBurst.remove(), () => impactBurst.remove());
            }

            if (targetEl && !reduced) {
                const shakeTarget = targetEl.querySelector?.('.card-art')
                    || targetEl.querySelector?.('.bui-rpg-portrait')
                    || targetEl.closest?.('.leader-rpg-portrait')
                    || targetEl;
                if (!isHeal) {
                    trackAnimation(safeAnimate(shakeTarget,
                        [
                            { translate: '0px 0px', filter: 'brightness(1.85) saturate(1.35)' },
                            { translate: '-7px 2px', filter: 'brightness(1.5)' },
                            { translate: '6px -2px', filter: 'brightness(1.25)' },
                            { translate: '-4px 1px', filter: 'brightness(1.1)' },
                            { translate: '3px 0px', filter: 'brightness(1)' },
                            { translate: '0px 0px', filter: 'brightness(1)' }
                        ],
                        { duration: 240, easing: 'ease-out' }
                    ));
                } else {
                    trackAnimation(safeAnimate(shakeTarget,
                        [
                            { filter: 'brightness(1)' },
                            { filter: 'brightness(1.45) drop-shadow(0 0 12px rgba(52, 211, 153, 0.75))', offset: 0.4 },
                            { filter: 'brightness(1)' }
                        ],
                        { duration: 320, easing: 'ease-out' }
                    ));
                }
            }

            const floatEl = document.createElement('div');
            floatEl.className = `bui-float-num ${isHeal ? 'is-heal' : (hit.shieldBroken && amount === 0 ? 'is-shield' : 'is-damage')}`;
            floatEl.textContent = isHeal
                ? `+${amount}`
                : (hit.shieldBroken && amount === 0 ? 'SHIELD' : `-${amount}`);
            floatEl.style.left = `${rect.cx}px`;
            floatEl.style.top = `${rect.top + rect.height * 0.44}px`;
            layer.appendChild(floatEl);

            const floatAnim = trackAnimation(safeAnimate(floatEl,
                reduced
                    ? [
                        { opacity: 1, translate: '-50% -50%' },
                        { opacity: 0, translate: '-50% -75%' }
                    ]
                    : [
                        { opacity: 0, translate: '-50% -35%', scale: '0.42' },
                        { opacity: 1, translate: '-50% -65%', scale: '1.52', offset: 0.18 },
                        { opacity: 1, translate: '-50% -80%', scale: '1.06', offset: 0.36 },
                        { opacity: 1, translate: '-50% -95%', scale: '1.0', offset: 0.72 },
                        { opacity: 0, translate: '-50% -130%', scale: '0.92' }
                    ],
                { duration: 920, easing: 'cubic-bezier(0.22, 1, 0.36, 1)', fill: 'forwards' }
            ));
            floatAnim?.finished?.then(() => floatEl.remove(), () => floatEl.remove());
        });
    }

    // 4. 进化 / 超进化脉冲爆发演出
    function playEvolveBurst(uid, isSuper = false) {
        const stageEl = getStageElement?.();
        const layer = getTransientLayer?.();
        const cardEl = queryBoardEntityElement(uid);
        if (!stageEl || !layer || !cardEl) return Promise.resolve();

        const atkEl = cardEl.querySelector('.attack');
        const hpEl = cardEl.querySelector('.health');
        animateStatPop(atkEl, 'buff');
        animateStatPop(hpEl, 'buff');

        if (prefersReducedMotion()) return Promise.resolve();

        const stageRect = stageEl.getBoundingClientRect();
        const rect = toStageRect(cardEl.getBoundingClientRect(), stageRect);
        if (!rect) return Promise.resolve();

        const burst = document.createElement('div');
        burst.className = `bui-evo-burst ${isSuper ? 'is-super' : 'is-evolve'}`;
        burst.style.left = `${rect.cx}px`;
        burst.style.top = `${rect.cy}px`;
        burst.innerHTML = `
            <span class="bui-evo-ring is-outer"></span>
            <span class="bui-evo-ring is-inner"></span>
            <span class="bui-evo-flare"></span>
        `;
        layer.appendChild(burst);

        const durationMs = isSuper ? 480 : 420;
        const ringAnim = trackAnimation(safeAnimate(burst,
            [
                { opacity: 0.95, scale: '0.42', rotate: '0deg' },
                { opacity: 1, scale: isSuper ? '1.35' : '1.18', rotate: isSuper ? '45deg' : '25deg', offset: 0.52 },
                { opacity: 0, scale: isSuper ? '1.72' : '1.48', rotate: isSuper ? '90deg' : '45deg' }
            ],
            { duration: durationMs, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' }
        ));
        ringAnim?.finished?.then(() => burst.remove(), () => burst.remove());

        trackAnimation(safeAnimate(cardEl,
            [
                { scale: '1', translate: '0px 0px' },
                { scale: isSuper ? '1.24' : '1.18', translate: '0px -10px', offset: 0.38 },
                { scale: '1', translate: '0px 0px' }
            ],
            { duration: durationMs - 20, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' }
        ));

        return new Promise(resolve => {
            scheduleTimer(resolve, 260);
        });
    }

    // 5A. 同步回调模式攻击冲撞（供测试台与幻境回放单步使用）
    function playAttackSequence({
        attackerSnap,
        targetSnap,
        attackerFatal = false,
        onImpactFrame
    }) {
        const layer = getTransientLayer?.();
        const token = generation;

        if (!layer || !attackerSnap || !targetSnap || prefersReducedMotion()) {
            onImpactFrame?.(null);
            return;
        }

        const attackerUid = attackerSnap.uid;
        const w = attackerSnap.unscaledW || 96;
        const h = attackerSnap.unscaledH || 132;
        const vec = computeContactVector(attackerSnap.rect, targetSnap.rect, 14);
        const contactStagePos = {
            cx: attackerSnap.rect.cx + vec.contactX,
            cy: attackerSnap.rect.cy + vec.contactY
        };

        const carrier = document.createElement('div');
        carrier.className = 'bui-motion-carrier is-attacker';
        carrier.style.width = `${w}px`;
        carrier.style.height = `${h}px`;
        carrier.style.left = `${attackerSnap.rect.cx - w / 2}px`;
        carrier.style.top = `${attackerSnap.rect.cy - h / 2}px`;

        if (attackerSnap.cloneEl) {
            carrier.appendChild(attackerSnap.cloneEl.cloneNode(true));
        }
        layer.appendChild(carrier);

        const currentRealAttacker = queryBoardEntityElement(attackerUid);
        hideRealElement(currentRealAttacker, attackerUid);

        const lungeAnim = trackAnimation(safeAnimate(carrier,
            [
                { translate: '0px 0px', scale: '1' },
                { translate: `${(-vec.ux * 12).toFixed(1)}px ${(-vec.uy * 12).toFixed(1)}px`, scale: '0.94', offset: 0.28 },
                { translate: `${vec.contactX.toFixed(1)}px ${vec.contactY.toFixed(1)}px`, scale: '1.10' }
            ],
            {
                duration: 210,
                easing: 'cubic-bezier(0.32, 0, 0.67, 0)',
                fill: 'forwards'
            }
        ));

        lungeAnim?.finished?.then(() => {
            if (!isCurrent(token)) {
                carrier.remove();
                return;
            }

            onImpactFrame?.(contactStagePos);

            if (attackerFatal) {
                carrier.remove();
                restoreRealElement(null, attackerUid);
                spawnSemanticExitClone(attackerSnap, 'destroy', contactStagePos);
                return;
            }

            const stageEl = getStageElement?.();
            const postAttackerEl = queryBoardEntityElement(attackerUid);
            let reboundEndX = 0;
            let reboundEndY = 0;
            if (stageEl && postAttackerEl) {
                syncEntityElementVisibility(attackerUid, postAttackerEl);
                const stageRect = stageEl.getBoundingClientRect();
                const postRect = toStageRect(postAttackerEl.getBoundingClientRect(), stageRect);
                if (postRect) {
                    reboundEndX = postRect.cx - attackerSnap.rect.cx;
                    reboundEndY = postRect.cy - attackerSnap.rect.cy;
                }
            }

            const reboundAnim = trackAnimation(safeAnimate(carrier,
                [
                    { translate: `${vec.contactX.toFixed(1)}px ${vec.contactY.toFixed(1)}px`, scale: '1.10' },
                    { translate: `${reboundEndX.toFixed(1)}px ${reboundEndY.toFixed(1)}px`, scale: '1' }
                ],
                {
                    duration: 165,
                    easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
                    fill: 'forwards'
                }
            ));

            const finishRebound = () => {
                carrier.remove();
                if (!isCurrent(token)) return;
                restoreRealElement(postAttackerEl, attackerUid);
            };
            reboundAnim?.finished?.then(finishRebound, finishRebound);
        }, () => {
            carrier.remove();
            restoreRealElement(currentRealAttacker, attackerUid);
        });
    }

    // 5B. 异步门禁模式攻击冲撞（供实战 SignalR AttackDeclared -> DamageDealt/CardMoved 事件流使用）
    function markActiveLungeExit(uid, exitReason = 'destroy') {
        const key = normalizeMotionUid(uid);
        const active = key ? activeLungesByUid.get(key) : null;
        if (!active) return false;
        active.exitReason = exitReason;
        return true;
    }

    function playAttackLungeAndHold({ attackerUid, targetUid }) {
        const stageEl = getStageElement?.();
        const layer = getTransientLayer?.();
        const token = generation;
        if (!stageEl || !layer || !attackerUid || !targetUid || prefersReducedMotion()) {
            return Promise.resolve();
        }

        const attackerEl = queryBoardEntityElement(attackerUid);
        const targetEl = queryBoardEntityElement(targetUid);
        if (!attackerEl || !targetEl) {
            return Promise.resolve();
        }

        const attackerSnap = captureElementSnapshot(attackerEl, attackerUid, 'field');
        const targetSnap = captureElementSnapshot(targetEl, targetUid, 'target');
        if (!attackerSnap || !targetSnap) {
            return Promise.resolve();
        }

        const normalizedAttackerKey = normalizeMotionUid(attackerUid);
        const w = attackerSnap.unscaledW || 96;
        const h = attackerSnap.unscaledH || 132;
        const vec = computeContactVector(attackerSnap.rect, targetSnap.rect, 14);
        const contactStagePos = {
            cx: attackerSnap.rect.cx + vec.contactX,
            cy: attackerSnap.rect.cy + vec.contactY
        };

        const carrier = document.createElement('div');
        carrier.className = 'bui-motion-carrier is-attacker';
        carrier.style.width = `${w}px`;
        carrier.style.height = `${h}px`;
        carrier.style.left = `${attackerSnap.rect.cx - w / 2}px`;
        carrier.style.top = `${attackerSnap.rect.cy - h / 2}px`;

        if (attackerSnap.cloneEl) {
            carrier.appendChild(attackerSnap.cloneEl.cloneNode(true));
        }
        layer.appendChild(carrier);

        hideRealElement(attackerEl, attackerUid);

        const lungeRecord = {
            uid: attackerUid,
            snap: attackerSnap,
            contactStagePos,
            exitReason: null
        };
        activeLungesByUid.set(normalizedAttackerKey, lungeRecord);

        return new Promise(resolveGate => {
            const lungeAnim = trackAnimation(safeAnimate(carrier,
                [
                    { translate: '0px 0px', scale: '1' },
                    { translate: `${(-vec.ux * 12).toFixed(1)}px ${(-vec.uy * 12).toFixed(1)}px`, scale: '0.94', offset: 0.28 },
                    { translate: `${vec.contactX.toFixed(1)}px ${vec.contactY.toFixed(1)}px`, scale: '1.10' }
                ],
                {
                    duration: 210,
                    easing: 'cubic-bezier(0.32, 0, 0.67, 0)',
                    fill: 'forwards'
                }
            ));

            lungeAnim?.finished?.then(() => {
                if (!isCurrent(token)) {
                    activeLungesByUid.delete(normalizedAttackerKey);
                    carrier.remove();
                    resolveGate();
                    return;
                }

                // 放行门禁，使排队的同波 DamageDealt / HealthChanged / UnitDestroyed / CardMoved 在当前宏任务同步执行完毕
                resolveGate();

                // 紧接同波事件同步结算后，检查攻击者是存活回弹还是在接触点同归于尽
                window.setTimeout(() => {
                    activeLungesByUid.delete(normalizedAttackerKey);
                    if (!isCurrent(token)) {
                        carrier.remove();
                        return;
                    }

                    const postAttackerEl = queryBoardEntityElement(attackerUid);
                    const diedInClash = Boolean(lungeRecord.exitReason) || !postAttackerEl;

                    if (diedInClash) {
                        carrier.remove();
                        restoreRealElement(attackerEl, attackerUid);
                        spawnSemanticExitClone(
                            attackerSnap,
                            lungeRecord.exitReason || 'destroy',
                            contactStagePos
                        );
                        return;
                    }

                    syncEntityElementVisibility(attackerUid, postAttackerEl);
                    const latestStageRect = stageEl.getBoundingClientRect();
                    const postRect = toStageRect(postAttackerEl.getBoundingClientRect(), latestStageRect);
                    const reboundEndX = postRect ? postRect.cx - attackerSnap.rect.cx : 0;
                    const reboundEndY = postRect ? postRect.cy - attackerSnap.rect.cy : 0;

                    const reboundAnim = trackAnimation(safeAnimate(carrier,
                        [
                            { translate: `${vec.contactX.toFixed(1)}px ${vec.contactY.toFixed(1)}px`, scale: '1.10' },
                            { translate: `${reboundEndX.toFixed(1)}px ${reboundEndY.toFixed(1)}px`, scale: '1' }
                        ],
                        {
                            duration: 165,
                            easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
                            fill: 'forwards'
                        }
                    ));

                    const finishRebound = () => {
                        carrier.remove();
                        if (!isCurrent(token)) return;
                        restoreRealElement(postAttackerEl, attackerUid);
                    };
                    reboundAnim?.finished?.then(finishRebound, finishRebound);
                }, 0);
            }, () => {
                activeLungesByUid.delete(normalizedAttackerKey);
                carrier.remove();
                restoreRealElement(attackerEl, attackerUid);
                resolveGate();
            });
        });
    }

    function playAlternateCast({
        layer, carrier, stageEl, stageRect, geometry, mode, targetUid, fastForward, onResolveImpact
    }) {
        const { width, height } = stageRect;
        if (!width || !height) return null;

        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        if (!ctx) return null;

        const timing = alternateCastTimings[mode];
        const speed = fastForward ? 0.55 : 1;
        const pixelRatio = Math.min(window.devicePixelRatio || 1, 1.5, 1440 / width, 900 / height);
        canvas.width = Math.max(1, Math.round(width * pixelRatio));
        canvas.height = Math.max(1, Math.round(height * pixelRatio));
        canvas.className = 'bui-alternate-cast-effect';
        canvas.setAttribute('aria-hidden', 'true');
        ctx.setTransform(canvas.width / width, 0, 0, canvas.height / height, 0, 0);

        const label = document.createElement('div');
        label.className = `bui-alternate-cast-label is-${mode}`;
        label.textContent = timing.label;
        carrier.append(label);
        layer.append(canvas);

        const targetEl = queryBoardEntityElement(targetUid);
        if (targetUid) hideRealElement(targetEl, targetUid);
        const updateTarget = () => {
            const current = queryBoardEntityElement(targetUid);
            const rect = current && toStageRect(current.getBoundingClientRect(), stageEl.getBoundingClientRect());
            geometry.target = rect ? { x: rect.cx, y: rect.cy } : geometry.hover;
        };
        updateTarget();

        let resolveSequence;
        const sequence = new Promise(resolve => { resolveSequence = resolve; });
        const startedAt = performance.now();
        let frameId;
        let impacted = false;
        let flightStarted = false;

        function resolveImpact() {
            if (impacted) return;
            impacted = true;
            if (targetUid) restoreRealElement(targetEl, targetUid);
            carrier.remove();
            resolveSequence();
            onResolveImpact?.();
        }

        function draw(ms) {
            if (!flightStarted && ms >= timing.resolve) {
                flightStarted = true;
                updateTarget();
            }
            if (!impacted) {
                const pose = castCardPose(mode, ms, geometry);
                carrier.style.translate = `${pose.x - geometry.hover.x}px ${pose.y - geometry.hover.y}px`;
                carrier.style.scale = `${pose.scaleX} ${pose.scaleY}`;
                carrier.style.rotate = `${pose.rotate}deg`;
                carrier.style.opacity = pose.opacity;
                label.style.opacity = pose.labelOpacity;
            }
            ctx.clearRect(0, 0, width, height);
            paintAlternateCastEffect(ctx, mode, ms, geometry);
        }

        function tick(now) {
            const ms = Math.max(0, (now - startedAt) / speed);
            draw(ms);
            if (ms < timing.done) frameId = window.requestAnimationFrame(tick);
        }

        function cancel() {
            activeAlternateCasts.delete(cancel);
            window.cancelAnimationFrame(frameId);
            window.clearTimeout(impactTimer);
            window.clearTimeout(cleanupTimer);
            canvas.remove();
            carrier.remove();
            if (!impacted && targetUid) restoreRealElement(targetEl, targetUid);
            resolveSequence();
        }

        // Resolve the card move even when background tabs pause animation frames.
        const impactTimer = window.setTimeout(resolveImpact, timing.impact * speed);
        const cleanupTimer = window.setTimeout(cancel, timing.done * speed);
        activeAlternateCasts.add(cancel);
        draw(0);
        frameId = window.requestAnimationFrame(tick);
        return sequence;
    }

    // 6. 出牌展示（飞入悬停点，再落位或结算法术）
    function playCastHoverSequence({
        cardHtmlElement,
        sourceRect,
        targetUid = null,
        isSpell = false,
        isOpponent = false,
        isFromDrag = false,
        alternateAnimation = null,
        fastForward = false,
        pulseTone = null,
        onResolveImpact
    }) {
        const stageEl = getStageElement?.();
        const layer = getTransientLayer?.();
        const token = generation;

        if (!stageEl || !layer || !cardHtmlElement || prefersReducedMotion()) {
            onResolveImpact?.();
            return Promise.resolve();
        }

        const stageRect = stageEl.getBoundingClientRect();
        const w = 102;
        const h = 140;

        const hoverCx = isFromDrag && sourceRect
            ? (sourceRect.cx * 0.42 + stageRect.width * 0.5 * 0.58)
            : stageRect.width * 0.5;
        const hoverCy = isOpponent
            ? stageRect.height * 0.36
            : (isFromDrag && sourceRect
                ? Math.max(stageRect.height * 0.36, Math.min(stageRect.height * 0.52, sourceRect.cy - 18))
                : stageRect.height * 0.44);

        const startCx = sourceRect?.cx ?? hoverCx;
        const startCy = sourceRect?.cy ?? (isOpponent ? 48 : stageRect.height - 80);
        const startScale = isOpponent ? 0.36 : (isFromDrag ? 1.06 : 0.96);

        const carrier = document.createElement('div');
        carrier.className = `bui-motion-carrier is-cast-hover ${isOpponent ? 'is-opp-cast' : 'is-self-cast'}`;
        carrier.style.width = `${w}px`;
        carrier.style.height = `${h}px`;
        carrier.style.left = `${hoverCx - w / 2}px`;
        carrier.style.top = `${hoverCy - h / 2}px`;

        carrier.appendChild(cardHtmlElement);
        layer.appendChild(carrier);

        if (alternateAnimation === 'accelerate' || alternateAnimation === 'crystalize') {
            const sequence = playAlternateCast({
                layer, carrier, stageEl, stageRect, targetUid, fastForward, onResolveImpact,
                mode: alternateAnimation,
                geometry: {
                    source: { x: startCx, y: startCy, scale: startScale },
                    hover: { x: hoverCx, y: hoverCy },
                    width: w, height: h, drag: isFromDrag
                }
            });
            if (sequence) return sequence;
        }

        const targetEl = targetUid ? queryBoardEntityElement(targetUid) : null;
        if (targetUid) hideRealElement(targetEl, targetUid);
        const halo = document.createElement('div');
        halo.className = `bui-cast-hover-halo ${isOpponent ? 'is-opp' : 'is-self'}`;
        carrier.prepend(halo);

        const startDx = startCx - hoverCx;
        const startDy = startCy - hoverCy;
        const speedScale = fastForward ? 0.55 : 1;
        const enterMs = Math.round((isFromDrag ? 95 : 140) * speedScale);
        const hoverMs = Math.round(260 * speedScale);
        const landMs = Math.round(175 * speedScale);
        const totalHoverPhaseMs = enterMs + hoverMs;

        return new Promise(resolveSequence => {
            const enterAndHoverAnim = trackAnimation(safeAnimate(carrier,
                [
                    { translate: `${startDx.toFixed(1)}px ${startDy.toFixed(1)}px`, scale: `${startScale}`, opacity: isOpponent ? 0.72 : 1 },
                    { translate: '0px 0px', scale: '1.15', opacity: 1, offset: enterMs / totalHoverPhaseMs },
                    { translate: '0px -6px', scale: '1.18', opacity: 1 }
                ],
                {
                    duration: totalHoverPhaseMs,
                    easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
                    fill: 'forwards'
                }
            ));

            enterAndHoverAnim?.finished?.then(() => {
                if (!isCurrent(token)) {
                    carrier.remove();
                    if (targetUid) restoreRealElement(targetEl, targetUid);
                    resolveSequence();
                    return;
                }

                if (isSpell || !targetUid) {
                    const burst = document.createElement('div');
                    burst.className = 'bui-spell-arcane-burst';
                    burst.style.left = `${hoverCx}px`;
                    burst.style.top = `${hoverCy}px`;
                    layer.appendChild(burst);
                    const burstAnim = trackAnimation(safeAnimate(burst,
                        [
                            { opacity: 0.95, scale: '0.55' },
                            { opacity: 0, scale: '1.65' }
                        ],
                        { duration: 300, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' }
                    ));
                    burstAnim?.finished?.then(() => burst.remove(), () => burst.remove());

                    onResolveImpact?.();
                    resolveSequence();

                    const fadeAnim = trackAnimation(safeAnimate(carrier,
                        [
                            { translate: '0px -6px', scale: '1.18', opacity: 1 },
                            { translate: '0px -14px', scale: '1.34', opacity: 0 }
                        ],
                        { duration: 165, easing: 'ease-out', fill: 'forwards' }
                    ));
                    fadeAnim?.finished?.then(() => carrier.remove(), () => carrier.remove());
                    return;
                }

                const currentTargetEl = queryBoardEntityElement(targetUid);
                const latestStageRect = stageEl.getBoundingClientRect();
                const destRect = currentTargetEl
                    ? toStageRect(currentTargetEl.getBoundingClientRect(), latestStageRect)
                    : null;

                if (!destRect || !currentTargetEl) {
                    carrier.remove();
                    restoreRealElement(targetEl, targetUid);
                    onResolveImpact?.();
                    resolveSequence();
                    return;
                }

                syncEntityElementVisibility(targetUid, currentTargetEl);
                const endDx = destRect.cx - hoverCx;
                const endDy = destRect.cy - hoverCy;
                const endScale = (destRect.width || 96) / w;

                const landAnim = trackAnimation(safeAnimate(carrier,
                    [
                        { translate: '0px -6px', scale: '1.18', opacity: 1 },
                        { translate: `${endDx.toFixed(1)}px ${endDy.toFixed(1)}px`, scale: `${endScale.toFixed(3)}`, opacity: 1 }
                    ],
                    { duration: landMs, easing: 'cubic-bezier(0.22, 1, 0.36, 1)', fill: 'forwards' }
                ));

                const finishLand = () => {
                    carrier.remove();
                    if (!isCurrent(token)) {
                        resolveSequence();
                        return;
                    }
                    restoreRealElement(currentTargetEl, targetUid);
                    const liveCard = queryBoardEntityElement(targetUid) || currentTargetEl;
                    if (liveCard?.isConnected) {
                        playLandingPulse(liveCard, pulseTone || (isOpponent ? 'crimson' : 'cyan'));
                    }
                    onResolveImpact?.();
                    resolveSequence();
                };
                landAnim?.finished?.then(finishLand, finishLand);
            }, () => {
                carrier.remove();
                if (targetUid) restoreRealElement(targetEl, targetUid);
                resolveSequence();
            });
        });
    }

    return {
        captureSnapshot,
        captureElementSnapshot,
        getAnchorRects,
        runPostRenderFlip,
        runCrossZoneCarrierFlight,
        playHandRightSlideIn,
        animateStatPop,
        playBurnCard,
        spawnSemanticExitClone,
        triggerWaveHits,
        playEvolveBurst,
        playAttackSequence,
        playAttackLungeAndHold,
        markActiveLungeExit,
        playCastHoverSequence,
        syncEntityElementVisibility,
        isEntityHiddenByCarrier,
        scheduleTimer,
        cancelAll,
        getGeneration,
        isCurrent
    };
}
