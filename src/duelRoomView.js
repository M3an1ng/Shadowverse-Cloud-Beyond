import { getLocalAvatarUrl } from './userSession.js';

function setText(element, value) {
    if (element.textContent !== value) element.textContent = value;
}

export function createDuelSeatView(side, role) {
    const element = document.getElementById('duel-room-seat-template').content.firstElementChild.cloneNode(true);
    element.dataset.side = side;
    element.setAttribute('aria-label', role);
    const state = element.querySelector('.duel-room-seat-state');
    const name = element.querySelector('.duel-room-name');
    const selfTag = element.querySelector('.duel-room-self');
    const score = element.querySelector('.duel-room-score');
    const avatar = element.querySelector('img');
    const avatarFallback = element.querySelector('.duel-room-avatar span');
    const decks = element.querySelector('.duel-room-decks');
    const actions = element.querySelector('.duel-room-actions');
    let lastAvatar;
    let lastDecks;
    avatar.addEventListener('error', () => { avatar.hidden = true; });

    return {
        element, actions,
        render(seat) {
            setText(state, seat.status ?? '');
            state.hidden = !seat.status;
            setText(name, seat.name);
            setText(score, seat.score ?? '');
            score.hidden = !seat.score;
            selfTag.hidden = !seat.self;
            const self = String(Boolean(seat.self));
            if (element.dataset.state !== seat.state) element.dataset.state = seat.state;
            if (element.dataset.self !== self) element.dataset.self = self;
            setText(avatarFallback, seat.state === 'empty' ? '' : (Array.from(seat.name)[0] ?? role[0]));
            const nextAvatar = getLocalAvatarUrl(seat.avatarUrl);
            if (nextAvatar !== lastAvatar) {
                lastAvatar = nextAvatar;
                avatar.hidden = !nextAvatar;
                if (nextAvatar) avatar.src = nextAvatar;
                else avatar.removeAttribute('src');
            }
            const key = JSON.stringify(seat.decks);
            if (key === lastDecks) return;
            lastDecks = key;
            decks.replaceChildren(...seat.decks.map(deck => {
                const panel = document.createElement('div');
                panel.className = 'duel-room-deck';
                if (deck.state) panel.dataset.state = deck.state;
                if (deck.class) panel.dataset.deckClass = String(deck.class).toLowerCase();
                const details = document.createElement('div');
                details.className = 'duel-room-deck-details';
                const copy = document.createElement('div');
                copy.className = 'duel-room-deck-copy';
                const title = document.createElement('strong');
                title.className = 'duel-room-deck-name';
                title.textContent = deck.name;
                if (deck.name) title.title = deck.name;
                copy.append(title);
                if (deck.classLabel) {
                    const label = document.createElement('span');
                    label.className = 'duel-room-deck-class';
                    label.textContent = deck.classLabel;
                    copy.append(label);
                }
                details.append(copy);
                panel.append(details);
                if (deck.status) {
                    const status = document.createElement('span');
                    status.className = 'duel-room-deck-status';
                    status.textContent = deck.status;
                    panel.append(status);
                }
                if (deck.action) {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'duel-room-button';
                    button.dataset.tournamentAction = deck.action;
                    button.dataset.deckId = deck.id;
                    button.textContent = deck.action === 'ban' ? '禁用' : '选择出战';
                    button.setAttribute('aria-label', button.textContent + '「' + deck.name + '」');
                    panel.append(button);
                }
                return panel;
            }));
        }
    };
}

export async function copyRoomCode(value) {
    try {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(value);
            return true;
        }
    } catch {
        // Plain HTTP rooms can still use the browser's selection-based copy.
    }
    const input = document.createElement('textarea');
    input.className = 'duel-room-copy-input';
    input.value = value;
    input.setAttribute('readonly', '');
    document.body.append(input);
    const previous = document.activeElement;
    input.select();
    try {
        return document.execCommand('copy');
    } catch {
        return false;
    } finally {
        input.remove();
        previous?.focus();
    }
}
