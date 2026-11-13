// Mode options carry their own numbering in either Name or Description.
export function splitModeNumber(rawText, index = 0) {
    const text = String(rawText ?? '').trim();
    const match = text.match(/^[（(]\s*([0-9０-９]+)\s*[）)]\s*[、.．:：]?\s*/u);
    const digits = match
        ? match[1].replace(/[０-９]/g, ch => String(ch.charCodeAt(0) - 0xff10)).replace(/^0+(?=\d)/, '')
        : String(index + 1);
    return { number: `（${digits}）`, text: match ? text.slice(match[0].length) : text, explicit: Boolean(match) };
}

export function getModeOptionContent(card, index) {
    const name = splitModeNumber(card?.Name ?? card?.name, index);
    const description = splitModeNumber(card?.Description ?? card?.description, index);
    const title = name.text === 'None' && description.text ? '' : name.text;
    return {
        number: name.explicit ? name.number : description.number,
        title,
        description: description.text === title ? '' : description.text
    };
}

// Single-choice switches directly; multi-choice never replaces a player's existing picks.
export function toggleModeSelection(selectedIds, key, maximum) {
    const selected = new Set(selectedIds);
    if (selected.has(key)) {
        selected.delete(key);
    } else if (maximum === 1 || selected.size < maximum) {
        if (maximum === 1) selected.clear();
        selected.add(key);
    }
    return selected;
}

function escapeHtml(text) {
    return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

export function createModeChoiceDialog({ root, request, min, max, renderDescription, onSelect, onConfirm, onCancel }) {
    const previousFocus = document.activeElement;
    root.classList.add('choice-centered-modal', 'choice-mode-overlay');
    // Keep the mode dialog above hand-drag carriers and outside transformed battlefield ancestors.
    document.body.appendChild(root);
    root.innerHTML = `
        <section class="mode-surface" role="dialog" aria-modal="true" aria-labelledby="choice-mode-title" aria-describedby="choice-mode-hint">
            <div class="mode-frame"><div class="mode-shell">
                <header class="mode-header">
                    <div class="mode-heading"><h2 id="choice-mode-title" class="mode-title">选择模式</h2></div>
                    <p id="choice-mode-hint" class="mode-subtitle" aria-live="polite"></p>
                </header>
                <div class="mode-list" role="group" aria-label="可选择的模式"></div>
                <footer class="mode-footer">
                    <div class="mode-selection">
                        <span class="mode-lamps" aria-hidden="true"></span>
                        <div class="mode-count" role="status" aria-live="polite"><span class="mode-status">已选</span><b id="choice-selected-count"></b></div>
                    </div>
                    <div class="mode-actions">
                        <button id="choice-cancel-btn" class="mode-action mode-cancel" type="button">取消</button>
                        <button id="choice-confirm-btn" class="mode-action mode-action-primary mode-confirm" type="button" disabled>
                            <span>确认选择</span><span class="mode-action-arrow" aria-hidden="true">→</span>
                        </button>
                    </div>
                </footer>
            </div></div>
        </section>`;
    const dialog = root.querySelector('.mode-surface');
    const list = root.querySelector('.mode-list');
    const hint = root.querySelector('.mode-subtitle');
    const confirm = root.querySelector('.mode-confirm');
    const cancel = root.querySelector('.mode-cancel');
    const status = root.querySelector('.mode-status');
    const count = root.querySelector('#choice-selected-count');
    const lamps = root.querySelector('.mode-lamps');
    const ids = (request.SelectableTargetIds || []).map(String);
    const cards = new Map((request.SelectableCardInstances || []).map(dto => [String(dto.InstanceId), dto]));
    const buttons = new Map();
    let busy = false;
    const defaultHint = ids.length === 0 ? '当前没有可选模式，请确认继续'
        : min === max ? `从以下效果中选择 ${max} 项`
            : min === 0 ? `至多选择 ${max} 项效果，也可直接确认` : `从以下效果中选择 ${min}–${max} 项`;
    hint.textContent = defaultHint;
    for (const [index, id] of ids.entries()) {
        const content = getModeOptionContent(cards.get(id), index);
        const button = document.createElement('button');
        button.className = 'mode-option';
        button.type = 'button';
        button.dataset.uid = id;
        button.setAttribute('aria-pressed', 'false');
        const number = document.createElement('span');
        number.className = 'mode-index';
        number.textContent = content.number;
        const effect = document.createElement('span');
        effect.className = 'mode-effect';
        if (content.title) {
            const title = document.createElement('span');
            title.className = 'mode-option-title';
            title.textContent = content.title;
            effect.appendChild(title);
        }
        if (content.description) {
            const description = document.createElement('span');
            description.className = 'mode-description';
            // Preserve the existing keyword/reference renderer while treating supplied text as text.
            description.innerHTML = renderDescription(escapeHtml(content.description));
            effect.appendChild(description);
        }
        if (!effect.childNodes.length) effect.textContent = '模式';
        button.append(number, effect);
        button.addEventListener('click', () => { if (!busy) onSelect(id); });
        list.appendChild(button);
        buttons.set(id, button);
    }
    // Large mode lists use the numeric count; avoid an unbounded row of decorative lamps.
    for (let i = 0; i < Math.min(max, 8); i++) lamps.appendChild(document.createElement('i'));
    cancel.hidden = request.IsCancellable !== true;
    confirm.addEventListener('click', () => { if (!confirm.disabled) onConfirm(); });
    cancel.addEventListener('click', () => { if (!busy && !cancel.hidden) onCancel(); });
    root.addEventListener('click', event => event.stopPropagation());
    root.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            if (!busy && !cancel.hidden) onCancel();
        } else if (event.key === 'Tab') {
            const items = [...dialog.querySelectorAll('button:not(:disabled):not([hidden])')];
            if (!items.length) { event.preventDefault(); return; }
            const index = items.indexOf(document.activeElement);
            if (index < 0 || (event.shiftKey ? index === 0 : index === items.length - 1)) {
                event.preventDefault();
                items[event.shiftKey ? items.length - 1 : 0].focus();
            }
            event.stopPropagation();
        }
    });
    const focusFrame = requestAnimationFrame(() => {
        if (root.isConnected) dialog.querySelector('button:not(:disabled):not([hidden])')?.focus({ preventScroll: true });
    });
    return {
        showLimitHint() {
            hint.textContent = `已选满 ${max} 项，请先取消一项再选择`;
        },
        update({ selectedIds, pending = false }) {
            busy = pending;
            hint.textContent = defaultHint;
            dialog.setAttribute('aria-busy', String(pending));
            for (const [id, button] of buttons) {
                button.setAttribute('aria-pressed', String(selectedIds.has(id)));
                button.disabled = pending;
            }
            const selected = selectedIds.size;
            count.textContent = min === max ? `${selected} / ${max}` : `${selected} / ${min}–${max}`;
            status.textContent = pending ? '确认中' : '已选';
            [...lamps.children].forEach((lamp, i) => lamp.classList.toggle('is-filled', i < selected));
            confirm.disabled = pending || selected < min || selected > max;
            confirm.querySelector('span').textContent = pending ? '正在确认…' : '确认选择';
            cancel.disabled = pending;
        },
        destroy() {
            cancelAnimationFrame(focusFrame);
            root.remove();
            if (previousFocus?.isConnected) previousFocus.focus({ preventScroll: true });
        }
    };
}
