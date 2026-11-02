import './styles/site-dialog.css';
export const defaultOperationSettings = Object.freeze({ autoConfirmTargets: true, evolutionConfirmation: true, autoConfirmModes: true, endTurnConfirmation: true, fusionConfirmation: false });
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
export function showOperationSettings() {
    if (document.getElementById('game-operation-settings')) return;
    const previous = document.activeElement;
    const overlay = document.createElement('div'); overlay.id = 'game-operation-settings'; overlay.className = 'site-dialog-backdrop';
    const panel = document.createElement('section'); panel.className = 'site-dialog'; panel.setAttribute('role','dialog'); panel.setAttribute('aria-modal','true'); panel.setAttribute('aria-label','游戏操作设置');
    const title = document.createElement('h2'); title.textContent = '游戏操作设置'; panel.append(title);
    let values = readOperationSettings();
    for (const [key, text] of Object.entries({ autoConfirmTargets:'目标选满后自动确认', evolutionConfirmation:'进化 / 超进化确认', autoConfirmModes:'模式选满后自动确认', endTurnConfirmation:'回合结束确认', fusionConfirmation:'融合确认' })) {
        const row = document.createElement('label'); row.className = 'operation-settings-row';
        const input = document.createElement('input'); input.type = 'checkbox'; input.checked = values[key]; input.setAttribute('role','switch');
        input.addEventListener('change', () => { values = saveOperationSettings({ ...values, [key]:input.checked }); });
        const label = document.createElement('span'); label.textContent = text; row.append(input,label); panel.append(row);
    }
    const footer = document.createElement('footer'); const close = document.createElement('button'); close.textContent = '关闭';
    const dismiss = () => { overlay.remove(); document.removeEventListener('keydown', onKey, true); if(previous?.isConnected) previous.focus(); };
    const onKey = event => { if(event.key === 'Escape') {event.stopImmediatePropagation();dismiss();} else if(event.key === 'Tab') { const items=[...panel.querySelectorAll('input,button')]; const index=items.indexOf(document.activeElement); event.preventDefault(); items[(index+(event.shiftKey?-1:1)+items.length)%items.length].focus(); } };
    close.addEventListener('click',dismiss); footer.append(close); panel.append(footer); overlay.append(panel); document.body.append(overlay); document.addEventListener('keydown',onKey,true); panel.querySelector('input').focus();
}
export function installOperationSettingsButton() {
    if (document.getElementById('game-operation-settings-button')) return;
    const host = document.getElementById('system-operation-settings'); if (!host) return;
    const button = document.createElement('button'); button.id='game-operation-settings-button'; button.className='tool-button'; button.textContent='游戏操作设置'; button.addEventListener('click',showOperationSettings); host.append(button);
}
