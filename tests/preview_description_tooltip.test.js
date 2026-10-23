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

        const options = { cards: [], openCardDetail: showCardDetailModal };
        await showPreviewDescriptionTooltip(anchor, 'keyword', '土之印', options);

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

        expect(resolveDerivedCardMatch).toHaveBeenCalledWith('大地之魔片', options);
        expect(showCardDetailModal).toHaveBeenCalledWith(
            expect.objectContaining({ id: 42, name: '大地之魔片' }),
            expect.objectContaining({
                navigationMode: 'push',
                cards: []
            })
        );
    });

    test('renders deck keyword tag', async () => {
        resolveKeywordInfo.mockResolvedValue({
            kind: 'deck',
            name: '无垠界域牌组',
            description: '由11张不同类型的卡片所组成的牌组。'
        });

        const anchor = document.createElement('button');
        anchor.dataset.previewToken = 'keyword';
        anchor.dataset.previewValue = '无垠界域牌组';
        setRect(anchor);
        document.body.appendChild(anchor);

        await showPreviewDescriptionTooltip(anchor, 'keyword', '无垠界域牌组', { cards: [] });

        const tag = document.querySelector('.preview-description-tooltip .preview-tooltip-tag');
        expect(tag).not.toBeNull();
        expect(tag.textContent).toBe('牌组关键字');
    });

    test('uses the caller-owned navigation action without loading the detail component', async () => {
        const openCardDetail = jest.fn();
        const card = { id: 77, name: '衍生卡' };
        resolveDerivedCardMatch.mockResolvedValue({ match: card });
        const anchor = document.createElement('button');
        setRect(anchor);
        document.body.appendChild(anchor);
        await showPreviewDescriptionTooltip(anchor, 'derived', '衍生卡', { openCardDetail });
        expect(openCardDetail).toHaveBeenCalledWith(card, expect.objectContaining({ navigationMode: 'push' }));
        expect(showCardDetailModal).not.toHaveBeenCalled();
    });
});
