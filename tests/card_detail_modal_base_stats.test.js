jest.mock('../src/descriptionParser.js', () => ({
    renderDescriptionHtml: jest.fn(() => '')
}));

jest.mock('../src/previewDescriptionReferences.js', () => ({
    hydrateDescriptionReferences: jest.fn(() => Promise.resolve())
}));

jest.mock('../src/enumLocalizationCache.js', () => ({
    ensureEnumLocalizations: jest.fn(() => Promise.resolve()),
    getCachedEnumLabel: jest.fn((_, __, fallback) => fallback ?? '')
}));

jest.mock('../src/previewDescriptionTooltip.js', () => ({
    showPreviewDescriptionTooltip: jest.fn()
}));

jest.mock('../src/userSession.js', () => ({
    getUserSession: jest.fn(() => null)
}));

jest.mock('../src/icon/utx_ic_card_attack.png', () => 'attack-icon');
jest.mock('../src/icon/utx_ic_card_health.png', () => 'health-icon');
jest.mock('../src/icon/utx_ic_card_cost.png', () => 'cost-icon');

const { showCardDetailModal } = require('../src/components/CardDetailMockup.js');

describe('showCardDetailModal', () => {
    beforeEach(() => {
        document.body.innerHTML = '';
    });

    afterEach(() => {
        document.querySelector('#card-detail-modal .close-btn')?.click();
        document.body.innerHTML = '';
    });

    test('renders base stats instead of current runtime stats', () => {
        showCardDetailModal({
            name: '测试随从',
            type: 'Follower',
            cost: 2,
            currentCost: 7,
            attack: 1,
            currentAttack: 9,
            health: 3,
            currentHealth: 8,
            description: ''
        });

        const costValue = document.querySelector('.stat-cost .card-stat-value');
        const statValues = Array.from(document.querySelectorAll('.stat-atk .card-stat-value, .stat-def .card-stat-value'))
            .map(node => node.textContent.trim());
        const miniStats = Array.from(document.querySelectorAll('.mini-stat'))
            .map(node => node.textContent.replace(/\s+/g, ' ').trim());

        expect(costValue?.textContent.trim()).toBe('2');
        expect(statValues).toEqual(['1', '3']);
        expect(miniStats).toEqual(expect.arrayContaining(['1', '3']));
        expect(document.body.textContent).not.toContain('7');
        expect(document.body.textContent).not.toContain('9');
        expect(document.body.textContent).not.toContain('8');
    });

    test('description navigation receives the detail action and preserves the back stack', () => {
        const { showPreviewDescriptionTooltip } = require('../src/previewDescriptionTooltip.js');
        const original = { id: 1, name: '原卡', description: '' };
        const derived = { id: 2, name: '衍生卡', description: '' };
        const cards = [original, derived];
        showCardDetailModal(original, { cards });
        const token = document.createElement('button');
        token.dataset.previewToken = 'derived';
        token.dataset.previewValue = '衍生卡';
        document.getElementById('card-detail-modal').appendChild(token);
        token.click();
        const options = showPreviewDescriptionTooltip.mock.calls.at(-1)[3];
        expect(options.openCardDetail).toBe(showCardDetailModal);
        expect(options.cards).toBe(cards);
        options.openCardDetail(derived, { cards, navigationMode: 'push' });
        expect(document.getElementById('card-detail-modal').textContent).toContain('衍生卡');
        document.querySelector('#card-detail-modal .back-btn').click();
        expect(document.getElementById('card-detail-modal').textContent).toContain('原卡');
    });
});
