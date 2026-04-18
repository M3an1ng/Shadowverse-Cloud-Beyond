import {
    getDefaultDeckEnvironment,
    getDeckEnvironmentLabel as getDeckEnvironmentLabelFromOptions,
    normalizeDeckEnvironment as normalizeDeckEnvironmentFromOptions
} from './deckEnvironmentOptions.js';

const CLASS_LABELS = {
    neutral: '中立',
    forestcraft: '妖精',
    swordcraft: '皇家护卫',
    runecraft: '巫师',
    dragoncraft: '龙族',
    abysscraft: '梦魇',
    havencraft: '主教',
    portalcraft: '复仇者'
};

const RULE_LABELS = {
    standard: '传统对战',
    dualclass: '混合职业'
};

function normalizeEnumValue(value) {
    if (value === null || value === undefined) return '';
    return String(value).trim().toLowerCase();
}

export function normalizeEnvironment(value, fallback = getDefaultDeckEnvironment()) {
    return normalizeDeckEnvironmentFromOptions(value, fallback);
}

export function normalizeDeckRule(value, fallback = 'standard') {
    if (value === null || value === undefined || value === '') return fallback;
    if (typeof value === 'number') return value === 1 ? 'dualClass' : 'standard';
    const normalized = normalizeEnumValue(value);
    return normalized === 'dualclass' || normalized === 'dual_class' || normalized === 'dual'
        ? 'dualClass'
        : 'standard';
}

export function resolveSecondaryClass(rule, primaryClass, secondaryClass) {
    if (normalizeDeckRule(rule) !== 'dualClass') {
        return null;
    }

    const normalizedPrimary = normalizeEnumValue(primaryClass);
    const normalizedSecondary = normalizeEnumValue(secondaryClass);
    if (!normalizedSecondary || normalizedSecondary === 'neutral' || normalizedSecondary === normalizedPrimary) {
        return null;
    }

    return normalizedSecondary;
}

export function getDeckClassLabel(value) {
    const normalized = normalizeEnumValue(value);
    return CLASS_LABELS[normalized] || value || '未知职业';
}

export function getDeckClassDisplay(primaryClass, rule, secondaryClass) {
    const primaryLabel = getDeckClassLabel(primaryClass);
    if (normalizeDeckRule(rule) !== 'dualClass') {
        return primaryLabel;
    }

    const secondary = resolveSecondaryClass(rule, primaryClass, secondaryClass);
    if (!secondary) {
        return `${primaryLabel} + ?`;
    }

    return `${primaryLabel} + ${getDeckClassLabel(secondary)}`;
}

export function getDeckEnvironmentLabel(value) {
    return getDeckEnvironmentLabelFromOptions(value);
}

export function getDeckRuleLabel(value) {
    const normalized = normalizeDeckRule(value).toLowerCase();
    if (!normalized) return '传统对战';
    if (normalized === 'dual_class' || normalized === 'dual') {
        return RULE_LABELS.dualclass;
    }
    return RULE_LABELS[normalized] || '传统对战';
}

export function formatDeckSummaryLabel(deck) {
    const classLabel = getDeckClassDisplay(
        deck?.class ?? deck?.Class,
        deck?.rule ?? deck?.Rule ?? deck?.mode ?? deck?.Mode,
        deck?.secondaryClass ?? deck?.SecondaryClass
    );
    const name = deck?.name || deck?.Name || '未命名卡组';
    const count = Number(deck?.cardCount ?? deck?.CardCount ?? 0);
    return `${name} · ${classLabel} · ${count}/40`;
}

export function buildDeckPresentation(deck) {
    const rule = normalizeDeckRule(deck?.rule ?? deck?.Rule ?? deck?.mode ?? deck?.Mode);
    return {
        classLabel: getDeckClassDisplay(deck?.class ?? deck?.Class, rule, deck?.secondaryClass ?? deck?.SecondaryClass),
        environmentLabel: getDeckEnvironmentLabel(deck?.environment ?? deck?.Environment),
        ruleLabel: getDeckRuleLabel(rule)
    };
}
