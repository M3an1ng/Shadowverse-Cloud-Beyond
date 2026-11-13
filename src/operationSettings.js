import './styles/site-dialog.css';
import { createDialogFrame, createDialogButton } from './ui/dialogFrame.js';
export const defaultOperationSettings = Object.freeze({
    autoConfirmTargets: true,
    evolutionConfirmation: true,
    autoConfirmModes: true,
    endTurnConfirmation: true,
    fusionConfirmation: false,
    hideCardButtons: false
});
export function readOperationSettings(storage = null) {
    try { const saved = JSON.parse((storage || globalThis.localStorage).getItem('sv-operation-settings') || '{}');
        return Object.fromEntries(Object.entries(defaultOperationSettings).map(([key, value]) => [key, typeof saved[key] === 'boolean' ? saved[key] : value]));
    } catch { return { ...defaultOperationSettings }; }
}
export function saveOperationSettings(value, storage = null) {
    const normalized = Object.fromEntries(Object.entries(defaultOperationSettings).map(([key, fallback]) => [key, typeof value[key] === 'boolean' ? value[key] : fallback]));
    try { (storage || globalThis.localStorage).setItem('sv-operation-settings', JSON.stringify(normalized)); } catch { /* Browser storage may be disabled. */ }
    return normalized;
}

function applyCardButtonVisibility(settings) {
    document.documentElement.classList.toggle('hide-card-buttons', settings.hideCardButtons);
    document.dispatchEvent(new CustomEvent('sv-card-buttons-visibility-change'));
}

export function showOperationSettings() {
    if (document.getElementById('game-operation-settings')) return;
    const previous = document.activeElement;
    const overlay = document.createElement('div'); overlay.id = 'game-operation-settings'; overlay.className = 'battle-dialog-backdrop';
    const { panel, body, actions, closeButton } = createDialogFrame('游戏操作设置', 'settings', { closable: true });
    let values = readOperationSettings();
    const labels = {
        autoConfirmTargets: '目标选满后自动确认',
        autoConfirmModes: '模式选满后自动确认',
        evolutionConfirmation: '进化 / 超进化确认',
        endTurnConfirmation: '回合结束确认',
        fusionConfirmation: '融合确认',
        hideCardButtons: '隐藏卡片上按钮'
    };
    for (const [key, text] of Object.entries(labels)) {
        if (key === 'autoConfirmTargets' || key === 'hideCardButtons') {
            const group = document.createElement('h3'); group.className = 'operation-settings-group';
            group.textContent = key === 'hideCardButtons' ? '卡面显示' : '操作确认'; body.append(group);
        }
        const row = document.createElement('label'); row.className = 'operation-settings-row';
        const input = document.createElement('input'); input.type = 'checkbox'; input.name = key; input.checked = values[key]; input.setAttribute('role','switch');
        input.addEventListener('change', () => {
            values = saveOperationSettings({ ...values, [key]: input.checked });
            if (key === 'hideCardButtons') applyCardButtonVisibility(values);
        });
        const label = document.createElement('span'); label.textContent = text; row.append(label,input); body.append(row);
    }
    const close = createDialogButton('完成', true);
    const dismiss = () => { overlay.remove(); document.removeEventListener('keydown', onKey, true); if(previous?.isConnected) previous.focus(); };
    const onKey = event => { if(event.key === 'Escape') {event.preventDefault();event.stopImmediatePropagation();dismiss();} else if(event.key === 'Tab') { const items=[...panel.querySelectorAll('input,button')]; const index=items.indexOf(document.activeElement); event.preventDefault(); items[(index+(event.shiftKey?-1:1)+items.length)%items.length].focus(); } };
    close.addEventListener('click',dismiss); closeButton.addEventListener('click',dismiss); actions.append(close); overlay.append(panel); document.body.append(overlay); document.addEventListener('keydown',onKey,true); panel.querySelector('input').focus();
}
export function installOperationSettingsButton() {
    applyCardButtonVisibility(readOperationSettings());
    if (document.getElementById('game-operation-settings-button')) return;
    const host = document.getElementById('system-operation-settings'); if (!host) return;
    const button = document.createElement('button'); button.id='game-operation-settings-button'; button.className='tool-button'; button.textContent='游戏操作设置'; button.addEventListener('click',showOperationSettings); host.append(button);
}
