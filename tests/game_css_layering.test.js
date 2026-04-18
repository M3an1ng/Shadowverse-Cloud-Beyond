const fs = require('fs');
const path = require('path');

function extractRuleBlocks(source, selector) {
    const escapedSelector = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return Array.from(source.matchAll(new RegExp(`${escapedSelector}\\s*\\{([\\s\\S]*?)\\}`, 'gm')))
        .map(match => match[1]);
}

function extractNumericDeclaration(block, propertyName) {
    const escapedProperty = propertyName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const match = block.match(new RegExp(`${escapedProperty}\\s*:\\s*(-?\\d+)`, 'm'));
    return match ? Number.parseInt(match[1], 10) : Number.NaN;
}

describe('game css layering', () => {
    test('game log container stays above the hand play drop lane overlay', () => {
        const cssPath = path.join(__dirname, '..', 'src', 'styles', 'game.css');
        const cssSource = fs.readFileSync(cssPath, 'utf8');

        const logBlocks = extractRuleBlocks(cssSource, '#game-log-container');
        const dropLaneBlocks = extractRuleBlocks(cssSource, '.hand-play-drop-lane');

        const logZIndex = logBlocks
            .map(block => extractNumericDeclaration(block, 'z-index'))
            .find(value => Number.isFinite(value));
        const dropLaneZIndex = dropLaneBlocks
            .map(block => extractNumericDeclaration(block, 'z-index'))
            .find(value => Number.isFinite(value));

        expect(Number.isFinite(logZIndex)).toBe(true);
        expect(Number.isFinite(dropLaneZIndex)).toBe(true);
        expect(logZIndex).toBeGreaterThan(dropLaneZIndex);
    });

    test('attack drag valid target feedback uses red across followers and leader zone', () => {
        const cssPath = path.join(__dirname, '..', 'src', 'styles', 'game.css');
        const cssSource = fs.readFileSync(cssPath, 'utf8');

        const hoverBlock = extractRuleBlocks(cssSource, '.attack-drag-hover').join('\n');
        const leaderZoneBlock = extractRuleBlocks(cssSource, '.attack-drag-leader-zone.is-active.is-valid').join('\n');

        expect(hoverBlock).toContain('239, 68, 68');
        expect(hoverBlock).not.toContain('34, 197, 94');
        expect(leaderZoneBlock).toContain('239, 68, 68');
        expect(leaderZoneBlock).not.toContain('34, 197, 94');
    });
});
