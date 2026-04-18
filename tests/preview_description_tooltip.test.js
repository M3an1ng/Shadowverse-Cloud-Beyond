jest.mock('../src/previewDescriptionLookup.js', () => ({
    resolveKeywordInfo: jest.fn(),
    resolveDerivedCardMatch: jest.fn()
}));

jest.mock('../src/components/CardDetailMockup.js', () => ({
    showCardDetailModal: jest.fn()
}));

const { resolveKeywordInfo, resolveDerivedCardMatch } = require('../src/previewDescriptionLookup.js');
const { showCardDetailModal } = require('../src/components/CardDetailMockup.js');
const {
    showPreviewDescriptionTooltip,
    hidePreviewDescriptionTooltip
} = require('../src/previewDescriptionTooltip.js');

function setRect(element, rect = {}) {
    element.getBoundingClientRect = () => ({
        left: 24,
        top: 24,
        right: 88,
        bottom: 44,
        width: 64,
        height: 20,
        ...rect
    });
}

describe('preview description tooltip', () => {
    beforeEach(() => {
        document.body.innerHTML = '';
        jest.clearAllMocks();
    });

    afterEach(() => {
        hidePreviewDescriptionTooltip();
        document.body.innerHTML = '';
    });

    test('renders derived references inside keyword tooltip and keeps them clickable', async () => {
        resolveKeywordInfo.mockResolvedValue({
            kind: 'card',
            name: '土之印',
            description: '召唤1张『大地之魔片』到战场上。'
        });
        resolveDerivedCardMatch.mockResolvedValue({
            match: {
                id: 42,
                name: '大地之魔片',
                artPreview: 'earthsigil-preview.webp',
                artDetail: 'earthsigil-detail.webp'
            }
        });

        const anchor = document.createElement('button');
        anchor.dataset.previewToken = 'keyword';
        anchor.dataset.previewValue = '土之印';
        setRect(anchor);
        document.body.appendChild(anchor);

        await showPreviewDescriptionTooltip(anchor, 'keyword', '土之印', { cards: [] });

        const nestedDerivedToken = document.querySelector('.preview-description-tooltip [data-preview-token="derived"]');
        expect(nestedDerivedToken).not.toBeNull();
        expect(nestedDerivedToken.textContent).toBe('大地之魔片');

        setRect(nestedDerivedToken, {
            left: 48,
            top: 52,
            right: 132,
            bottom: 72,
            width: 84
        });
        nestedDerivedToken.dispatchEvent(new MouseEvent('click', { bubbles: true }));
        await Promise.resolve();
        await Promise.resolve();

        expect(resolveDerivedCardMatch).toHaveBeenCalledWith('大地之魔片', { cards: [] });
        expect(showCardDetailModal).toHaveBeenCalledWith(
            expect.objectContaining({ id: 42, name: '大地之魔片' }),
            expect.objectContaining({
                navigationMode: 'push',
                cards: []
            })
        );
    });
});
