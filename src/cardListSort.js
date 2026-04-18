const CARD_CLASS_ORDER = Object.freeze({
    neutral: 0,
    forestcraft: 1,
    swordcraft: 2,
    runecraft: 3,
    dragoncraft: 4,
    abysscraft: 5,
    havencraft: 6,
    portalcraft: 7,
    中立: 0,
    精灵: 1,
    皇家护卫: 2,
    巫师: 3,
    龙族: 4,
    梦魇: 5,
    主教: 6,
    复仇者: 7
});

const CARD_TYPE_ORDER = Object.freeze({
    leader: 0,
    follower: 1,
    spell: 2,
    amulet: 3,
    crest: 4,
    faith: 5,
    主战者: 0,
    从者: 1,
    法术: 2,
    护符: 3,
    纹章: 4,
    信仰: 5
});

const CARD_RARITY_ORDER = Object.freeze({
    special: 0,
    特殊: 0,
    bronze: 1,
    common: 1,
    青铜: 1,
    silver: 2,
    白银: 2,
    gold: 3,
    黄金: 3,
    legend: 4,
    legendary: 4,
    传说: 4
});

const MAX_SORT_KEY = Number.MAX_SAFE_INTEGER;

function toSortNumber(value, fallback = MAX_SORT_KEY) {
    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }
    if (typeof value === 'string') {
        const trimmed = value.trim();
        if (!trimmed) return fallback;
        const parsed = Number(trimmed);
        if (Number.isFinite(parsed)) {
            return parsed;
        }
    }
    return fallback;
}

function normalizeKey(value) {
    if (value === null || value === undefined) return '';
    return String(value).trim().toLowerCase().replace(/[\s_-]+/g, '');
}

function resolveEnumOrder(value, orderMap) {
    const numeric = toSortNumber(value, Number.NaN);
    if (Number.isFinite(numeric)) {
        return numeric;
    }
    const key = normalizeKey(value);
    if (!key) return MAX_SORT_KEY;
    return Object.prototype.hasOwnProperty.call(orderMap, key)
        ? orderMap[key]
        : MAX_SORT_KEY;
}

function resolveCost(card) {
    return toSortNumber(card?.cost ?? card?.Cost, 0);
}

function resolveClassOrder(card) {
    const rawClass = card?.class ?? card?.Class ?? card?.cardClass ?? card?.card_class;
    return resolveEnumOrder(rawClass, CARD_CLASS_ORDER);
}

function resolveTypeOrder(card) {
    const rawType = card?.type ?? card?.Type ?? card?.cardType ?? card?.card_type;
    return resolveEnumOrder(rawType, CARD_TYPE_ORDER);
}

function resolveSetDefinitionOrder(card) {
    const rawSetId = card?.card_set_definition_id ?? card?.cardSetDefinitionId ?? card?.CardSetDefinitionId;
    return toSortNumber(rawSetId, MAX_SORT_KEY);
}

function resolveRarityOrder(card) {
    const rawRarity = card?.rarity ?? card?.Rarity;
    return resolveEnumOrder(rawRarity, CARD_RARITY_ORDER);
}

function resolveCardId(card) {
    return toSortNumber(card?.id ?? card?.CardId, MAX_SORT_KEY);
}

export function compareCardsForEditorAndDeck(a, b) {
    const costDiff = resolveCost(a) - resolveCost(b);
    if (costDiff !== 0) return costDiff;

    const classDiff = resolveClassOrder(a) - resolveClassOrder(b);
    if (classDiff !== 0) return classDiff;

    const typeDiff = resolveTypeOrder(a) - resolveTypeOrder(b);
    if (typeDiff !== 0) return typeDiff;

    const setDiff = resolveSetDefinitionOrder(a) - resolveSetDefinitionOrder(b);
    if (setDiff !== 0) return setDiff;

    const rarityDiff = resolveRarityOrder(a) - resolveRarityOrder(b);
    if (rarityDiff !== 0) return rarityDiff;

    return resolveCardId(a) - resolveCardId(b);
}
