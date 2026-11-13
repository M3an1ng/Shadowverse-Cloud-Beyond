import '../styles/battle-dialog.css';

let nextDialogId = 0;

// Only the approved battle confirmations, notices, settings and history opt in.
export function createDialogFrame(title, variant = 'notice', { closable = false } = {}) {
    const panel = document.createElement('section');
    panel.className = `battle-dialog battle-dialog--${variant}`;
    panel.dataset.variant = variant;
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-modal', 'true');
    panel.innerHTML = `
        <div class="battle-dialog-frame"><div class="battle-dialog-shell">
            <header class="battle-dialog-heading"><h2></h2></header>
            <div class="battle-dialog-body"></div>
            <footer class="battle-dialog-actions"></footer>
        </div></div>`;
    const heading = panel.querySelector('h2');
    heading.id = `battle-dialog-title-${++nextDialogId}`;
    heading.textContent = title;
    panel.setAttribute('aria-labelledby', heading.id);
    const header = panel.querySelector('header');
    let closeButton = null;
    if (closable) {
        header.classList.add('has-close');
        closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'battle-dialog-close';
        closeButton.setAttribute('aria-label', '关闭');
        closeButton.textContent = '×';
        header.append(closeButton);
    }
    return { panel, heading, header, closeButton,
        body: panel.querySelector('.battle-dialog-body'),
        actions: panel.querySelector('.battle-dialog-actions') };
}

export function createDialogButton(text, primary = false) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `battle-dialog-button${primary ? ' is-primary' : ''}`;
    button.textContent = text;
    return button;
}
