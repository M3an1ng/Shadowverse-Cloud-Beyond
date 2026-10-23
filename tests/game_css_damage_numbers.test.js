const fs = require('fs');
const path = require('path');

function readGameCss() {
    const cssPath = path.join(__dirname, '..', 'src', 'styles', 'game.css');
    return fs.readFileSync(cssPath, 'utf8');
}

function extractRuleBlock(source, selector) {
    const escapedSelector = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const match = source.match(new RegExp(`${escapedSelector}\\s*\\{([\\s\\S]*?)\\}`, 'm'));
    return match ? match[1] : '';
}

function extractKeyframesBlock(source, name) {
    const startPattern = new RegExp(`@keyframes\\s+${name}\\s*\\{`, 'm');
    const startMatch = startPattern.exec(source);
    if (!startMatch) return '';

    let depth = 1;
    let index = startMatch.index + startMatch[0].length;
    while (index < source.length && depth > 0) {
        if (source[index] === '{') depth += 1;
        if (source[index] === '}') depth -= 1;
        index += 1;
    }
    return source.slice(startMatch.index, index);
}

describe('damage number combat feedback', () => {
    test('keeps damage numbers readable long enough to notice the hit', () => {
        const css = readGameCss();
        const damageRule = extractRuleBlock(css, '.damage-number');
        const keyframes = extractKeyframesBlock(css, 'damage-number-animation');

        expect(damageRule).toContain('animation: damage-number-animation 1.25s');
        expect(damageRule).toContain('-webkit-text-stroke');
        expect(damageRule).toContain('filter: drop-shadow');
        expect(damageRule).toContain('z-index: 240');

        expect(keyframes).toContain('65%');
        expect(keyframes).toMatch(/65%\s*\{[\s\S]*opacity:\s*1;/);
    });
});
