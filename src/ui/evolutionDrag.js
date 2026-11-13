const SOURCE_SELECTOR = '#player1-info .ep-box, #player1-info .sep-box';
const TARGET_SELECTOR = '#player1-field .field-card[data-uid]';
const MIN_DRAG_DISTANCE = 10;

function centerOf(element) {
    const bounds = element.getBoundingClientRect();
    return { x: bounds.left + bounds.width / 2, y: bounds.top + bounds.height / 2 };
}

export function createEvolutionDrag({ getAvailability, onDrop, suppressClick }) {
    let container;
    let drag;

    function cancel() {
        if (!drag) return;
        const { listeners, origin, target, arrow } = drag;
        drag = null;
        listeners.abort();
        origin.classList.remove('evolution-drag-origin');
        target?.classList.remove('evolution-drag-target');
        arrow?.remove();
        container.removeAttribute('data-evolution-drag');
    }

    function getDropTarget(event, available) {
        if (!available || available.turnKey !== drag.turnKey || !drag.origin.isConnected) return null;
        const target = document.elementFromPoint(event.clientX, event.clientY)?.closest(TARGET_SELECTOR);
        return target && container.contains(target) && available.targets.has(target.dataset.uid) ? target : null;
    }

    function move(event) {
        if (!drag || event.pointerId !== drag.pointerId) return;
        const available = getAvailability(drag.mode);
        if (!available?.targets.size || available.turnKey !== drag.turnKey || !drag.origin.isConnected || event.buttons === 0) {
            cancel();
            return;
        }

        if (!drag.arrow) {
            if (Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) < MIN_DRAG_DISTANCE) return;
            drag.arrow = document.createElement('div');
            drag.arrow.className = 'evolution-drag-arrow';
            drag.arrow.dataset.mode = drag.mode;
            drag.arrow.setAttribute('aria-hidden', 'true');
            document.body.append(drag.arrow);
            container.dataset.evolutionDrag = drag.mode;
            drag.origin.classList.add('evolution-drag-origin');
        }

        const target = getDropTarget(event, available);
        if (target !== drag.target) {
            drag.target?.classList.remove('evolution-drag-target');
            target?.classList.add('evolution-drag-target');
            drag.target = target;
        }
        const start = centerOf(drag.origin);
        const end = target ? centerOf(target) : { x: event.clientX, y: event.clientY };
        const dx = end.x - start.x;
        const dy = end.y - start.y;
        drag.arrow.style.width = `${Math.hypot(dx, dy)}px`;
        drag.arrow.style.transform = `translate(${start.x}px, ${start.y}px) rotate(${Math.atan2(dy, dx)}rad) translateY(-50%)`;
        drag.arrow.classList.toggle('is-locked', Boolean(target));
        event.preventDefault();
    }

    function drop(event) {
        if (!drag || event.pointerId !== drag.pointerId) return;
        const { mode, arrow } = drag;
        const target = arrow ? getDropTarget(event, getAvailability(mode)) : null;
        cancel();
        if (!arrow) return;
        suppressClick();
        event.preventDefault();
        if (target) onDrop(target.dataset.uid, mode);
    }

    function start(event) {
        if (drag || !event.isPrimary || event.button !== 0) return;
        const origin = event.target?.closest?.(SOURCE_SELECTOR);
        if (!origin) return;
        const mode = origin.classList.contains('sep-box') ? 'super' : 'evolve';
        const available = getAvailability(mode);
        if (!available?.targets.size) return;

        const listeners = new AbortController();
        drag = {
            pointerId: event.pointerId,
            startX: event.clientX,
            startY: event.clientY,
            turnKey: available.turnKey,
            mode,
            origin,
            listeners,
            target: null,
            arrow: null
        };
        const options = { signal: listeners.signal };
        window.addEventListener('pointermove', move, options);
        window.addEventListener('pointerup', drop, options);
        window.addEventListener('pointercancel', e => { if (e.pointerId === drag?.pointerId) cancel(); }, options);
        window.addEventListener('blur', cancel, options);
        window.addEventListener('keydown', e => { if (e.key === 'Escape') cancel(); }, options);
        event.preventDefault();
    }

    return {
        attach(element) {
            if (element === container) return;
            cancel();
            container?.removeEventListener('pointerdown', start);
            container = element;
            container.addEventListener('pointerdown', start);
        },
        refresh() {
            if (drag) {
                const available = getAvailability(drag.mode);
                if (!available?.targets.size || available.turnKey !== drag.turnKey) cancel();
            }
            for (const origin of container?.querySelectorAll(SOURCE_SELECTOR) ?? []) {
                const mode = origin.classList.contains('sep-box') ? 'super' : 'evolve';
                origin.classList.toggle('evolution-drag-available', Boolean(getAvailability(mode)?.targets.size));
            }
        },
        cancel,
        get active() { return Boolean(drag); }
    };
}
