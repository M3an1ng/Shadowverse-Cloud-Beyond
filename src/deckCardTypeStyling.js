function normalizeDeckCardType(value) {
    return String(value ?? "").trim().toLowerCase();
}

export function getDeckCardTypeClass(value) {
    const normalized = normalizeDeckCardType(value);
    if (normalized === "follower" || normalized === "spell" || normalized === "amulet") {
        return `type-${normalized}`;
    }

    return "";
}

export function buildDeckCardRootClassName(baseClassName, { hasArt = false, rarityClass = "", type = "" } = {}) {
    const classes = [String(baseClassName ?? "").trim()].filter(Boolean);

    if (hasArt) {
        classes.push("has-art");
    }

    if (rarityClass) {
        classes.push(String(rarityClass).trim());
    }

    const typeClass = getDeckCardTypeClass(type);
    if (typeClass) {
        classes.push(typeClass);
    }

    return classes.join(" ");
}
