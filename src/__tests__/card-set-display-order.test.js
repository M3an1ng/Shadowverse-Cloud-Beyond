import { compareCardSetDisplayOrder } from '../cardSetDisplayOrder.js';

describe('card set display order', () => {
    test('orders filter options by numeric set code before label', () => {
        const options = [
            { value: 'bravo', label: 'Bravo', code: '102' },
            { value: 'alpha', label: 'Alpha', code: '001' },
            { value: 'legacy', label: 'Legacy' }
        ];

        const ordered = options.slice().sort(compareCardSetDisplayOrder);

        expect(ordered.map(option => option.label)).toEqual(['Alpha', 'Bravo', 'Legacy']);
    });

    test('falls back to region and sequence when set code text is unavailable', () => {
        const options = [
            { name: 'Set B', region: 2, sequence: 1 },
            { name: 'Set A', region: 1, sequence: 12 }
        ];

        const ordered = options.slice().sort(compareCardSetDisplayOrder);

        expect(ordered.map(option => option.name)).toEqual(['Set A', 'Set B']);
    });
});
