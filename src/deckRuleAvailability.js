const DUAL_CLASS_RULE_UI_ENABLED = false;

function normalizeDeckRuleKey(value, fallback = "standard") {
    if (value === null || value === undefined || value === "") {
        return fallback;
    }
    if (typeof value === "number") {
        return value === 1 ? "dualClass" : "standard";
    }

    const normalized = String(value).trim().toLowerCase();
    return normalized === "dualclass" || normalized === "dual_class" || normalized === "dual"
        ? "dualClass"
        : "standard";
}

export function isDeckRuleUiVisible(rule) {
    const normalizedRule = normalizeDeckRuleKey(rule);
    if (normalizedRule === "dualClass") {
        return DUAL_CLASS_RULE_UI_ENABLED;
    }
    return true;
}

export function filterUiVisibleDecks(decks) {
    return (Array.isArray(decks) ? decks : []).filter((deck) =>
        isDeckRuleUiVisible(deck?.rule ?? deck?.Rule ?? deck?.mode ?? deck?.Mode));
}

export function getUiVisibleDeckRuleOptions(options) {
    return (Array.isArray(options) ? options : []).filter((option) =>
        isDeckRuleUiVisible(option?.value));
}

export function getDefaultVisibleDeckRule() {
    return "standard";
}
