const KEYWORD_OPEN = '【';
const KEYWORD_CLOSE = '】';
const DERIVED_OPEN = '『';
const DERIVED_CLOSE = '』';
const REFERENCE_OPEN = '{';
const REFERENCE_CLOSE = '}';

function escapeHtmlAttribute(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

function normalizeKeywordToken(value) {
    const text = String(value ?? '');
    const underscoreIndex = text.indexOf('_');
    if (underscoreIndex === -1) {
        return { base: text, renderSuffix: '' };
    }
    const base = text.slice(0, underscoreIndex);
    const suffix = text.slice(underscoreIndex + 1);
    if (!base) {
        return { base: text, renderSuffix: '' };
    }
    return { base, renderSuffix: suffix ? `_${suffix}` : '' };
}

export function parseDescriptionTokens(input) {
    const text = input == null ? '' : String(input);
    const tokens = [];
    let index = 0;
    const dividerPattern = /[-—]{2,}/g;

    const pushPlain = (value) => {
        if (!value) return;
        let lastIndex = 0;
        dividerPattern.lastIndex = 0;
        let match = dividerPattern.exec(value);
        while (match) {
            const start = match.index;
            if (start > lastIndex) {
                let chunk = value.slice(lastIndex, start);
                if (/\s$/.test(chunk)) {
                    chunk = chunk.replace(/\s+$/, '');
                }
                if (chunk) {
                    tokens.push({ type: 'plain', text: chunk });
                }
            }
            tokens.push({ type: 'divider', text: match[0] });

            lastIndex = start + match[0].length;
            while (lastIndex < value.length && /\s/.test(value[lastIndex])) {
                lastIndex++;
            }

            dividerPattern.lastIndex = lastIndex;
            match = dividerPattern.exec(value);
        }
        if (lastIndex < value.length) {
            tokens.push({ type: 'plain', text: value.slice(lastIndex) });
        }
    };

    while (index < text.length) {
        const keywordStart = text.indexOf(KEYWORD_OPEN, index);
        const derivedStart = text.indexOf(DERIVED_OPEN, index);
        const referenceStart = text.indexOf(REFERENCE_OPEN, index);

        if (keywordStart === -1 && derivedStart === -1 && referenceStart === -1) {
            pushPlain(text.slice(index));
            break;
        }

        let nextStart = keywordStart;
        let closeChar = KEYWORD_CLOSE;
        let type = 'keyword';

        if (nextStart === -1 || (derivedStart !== -1 && derivedStart < nextStart)) {
            nextStart = derivedStart;
            closeChar = DERIVED_CLOSE;
            type = 'derived';
        }

        if (nextStart === -1 || (referenceStart !== -1 && referenceStart < nextStart)) {
            nextStart = referenceStart;
            closeChar = REFERENCE_CLOSE;
            type = 'reference';
        }

        if (nextStart > index) {
            pushPlain(text.slice(index, nextStart));
        }

        const endIndex = text.indexOf(closeChar, nextStart + 1);
        if (endIndex === -1) {
            pushPlain(text.slice(nextStart));
            break;
        }

        const content = text.slice(nextStart + 1, endIndex);
        if (content) {
            if (type === 'reference' && tokens.length > 0) {
                const lastToken = tokens[tokens.length - 1];
                if (lastToken.type === 'plain' && /\\n\\s*$/.test(lastToken.text)) {
                    lastToken.text = lastToken.text.replace(/\\s+$/, '');
                }
            }
            tokens.push({ type, text: content });
        } else {
            pushPlain(text.slice(nextStart, endIndex + 1));
        }

        index = endIndex + 1;
        if (type === 'reference') {
            let cursor = index;
            let sawNewline = false;
            while (cursor < text.length && /\\s/.test(text[cursor])) {
                if (text[cursor] === '\\n') {
                    sawNewline = true;
                }
                cursor++;
            }
            if (sawNewline) {
                index = cursor;
            }
        }
    }

    return tokens;
}

export function renderDescriptionHtml(input) {
    const tokens = parseDescriptionTokens(input);
    if (!tokens.length) return '';

    return tokens
        .map(token => {
            if (token.type === 'divider') {
                return '<div class="preview-description-divider" role="presentation"></div>';
            }
            if (token.type === 'keyword') {
                const normalized = normalizeKeywordToken(token.text);
                const value = escapeHtmlAttribute(normalized.base);
                const suffix = normalized.renderSuffix;
                return `<span class="preview-description-token preview-description-keyword">【<span class="preview-description-text" data-preview-token="keyword" data-preview-value="${value}">${normalized.base}</span>${suffix}】</span>`;
            }
            if (token.type === 'derived') {
                const value = escapeHtmlAttribute(token.text);
                return `<span class="preview-description-token preview-description-derived">『<span class="preview-description-text" data-preview-token="derived" data-preview-value="${value}">${token.text}</span>』</span>`;
            }
            if (token.type === 'reference') {
                const value = escapeHtmlAttribute(token.text);
                return `<div class="preview-description-reference" data-preview-reference="${value}">{${token.text}}</div>`;
            }
            return token.text;
        })
        .join('');
}
