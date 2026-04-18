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
});
