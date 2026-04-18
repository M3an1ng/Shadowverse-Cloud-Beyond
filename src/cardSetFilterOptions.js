import { compareCardSetDisplayOrder } from './cardSetDisplayOrder.js';

export const CARD_SET_UNSPECIFIED_ENVIRONMENT = '__unspecified__';

function normalizeText(value) {
    return String(value ?? '').trim();
}

function normalizeKey(value) {
    return normalizeText(value).toLowerCase();
}

function normalizeEnvironmentValue(value, normalizeEnvironment) {
    if (value === CARD_SET_UNSPECIFIED_ENVIRONMENT) {
        return CARD_SET_UNSPECIFIED_ENVIRONMENT;
    }

    const normalized = typeof normalizeEnvironment === 'function'
        ? normalizeEnvironment(value)
        : normalizeKey(value);
    return normalizeText(normalized);
}

function normalizeEnvironmentList(value, normalizeEnvironment) {
    const source = Array.isArray(value) ? value : (value == null ? [] : [value]);
    const normalized = source
        .map(item => normalizeEnvironmentValue(item, normalizeEnvironment))
        .filter(Boolean);
    return Array.from(new Set(normalized));
}

function aggregateCardSets(cards, getCardSetInfo, normalizeEnvironment) {
    const entries = new Map();

    (Array.isArray(cards) ? cards : []).forEach(card => {
        const info = typeof getCardSetInfo === 'function' ? getCardSetInfo(card) : null;
        const name = normalizeText(info?.name);
        if (!name) {
            return;
        }

        const key = normalizeKey(name);
        if (!entries.has(key)) {
            entries.set(key, {
                value: name,
                label: name,
                code: normalizeText(info?.code),
                environments: new Set()
            });
        }

        const entry = entries.get(key);
        if (!entry.code) {
            entry.code = normalizeText(info?.code);
        }

        const environments = normalizeEnvironmentList(info?.environments, normalizeEnvironment);
        if (environments.length === 0) {
            entry.environments.add(CARD_SET_UNSPECIFIED_ENVIRONMENT);
            return;
        }

        environments.forEach(environment => entry.environments.add(environment));
    });

    return Array.from(entries.values()).map(entry => ({
        value: entry.value,
        label: entry.label,
        code: entry.code,
        environments: Array.from(entry.environments)
    }));
}

export function matchesCardSetEnvironment(info, selectedEnvironment, normalizeEnvironment) {
    const normalizedSelected = normalizeEnvironmentValue(selectedEnvironment, normalizeEnvironment);
    if (!normalizedSelected) {
        return true;
    }

    const environments = normalizeEnvironmentList(info?.environments, normalizeEnvironment);
    if (environments.length === 0) {
        return normalizedSelected === CARD_SET_UNSPECIFIED_ENVIRONMENT;
    }

    return environments.includes(normalizedSelected);
}

export function buildCardSetEnvironmentOptions(cards, {
    getCardSetInfo,
    normalizeEnvironment,
    getEnvironmentLabel,
    knownEnvironmentOptions = []
} = {}) {
    const cardSets = aggregateCardSets(cards, getCardSetInfo, normalizeEnvironment);
    const availableEnvironmentSet = new Set(
        cardSets.flatMap(cardSet => cardSet.environments).filter(Boolean)
    );

    const normalizedKnown = (Array.isArray(knownEnvironmentOptions) ? knownEnvironmentOptions : [])
        .map(option => ({
            value: normalizeEnvironmentValue(option?.value, normalizeEnvironment),
            label: normalizeText(option?.label ?? option?.value)
        }))
        .filter(option => option.value && option.label);

    const ordered = [];
    const seen = new Set();

    normalizedKnown.forEach(option => {
        if (!availableEnvironmentSet.has(option.value) || seen.has(option.value)) {
            return;
        }

        seen.add(option.value);
        ordered.push(option);
    });

    Array.from(availableEnvironmentSet)
        .filter(value => value && !seen.has(value))
        .sort((left, right) => {
            const leftLabel = normalizeText(
                typeof getEnvironmentLabel === 'function' ? getEnvironmentLabel(left) : left
            );
            const rightLabel = normalizeText(
                typeof getEnvironmentLabel === 'function' ? getEnvironmentLabel(right) : right
            );
            return leftLabel.localeCompare(rightLabel, undefined, { numeric: true, sensitivity: 'base' });
        })
        .forEach(value => {
            ordered.push({
                value,
                label: normalizeText(typeof getEnvironmentLabel === 'function' ? getEnvironmentLabel(value) : value) || value
            });
        });

    return ordered;
}

export function buildGroupedCardSetOptions(cards, {
    getCardSetInfo,
    normalizeEnvironment,
    getEnvironmentLabel,
    knownEnvironmentOptions = []
} = {}) {
    const environments = buildCardSetEnvironmentOptions(cards, {
        getCardSetInfo,
        normalizeEnvironment,
        getEnvironmentLabel,
        knownEnvironmentOptions
    });
    const cardSets = aggregateCardSets(cards, getCardSetInfo, normalizeEnvironment);
    const seenCardSets = new Set();

    return environments
        .map(environment => ({
            value: environment.value,
            label: environment.label,
            options: cardSets
                .filter(cardSet => {
                    const key = normalizeKey(cardSet.value);
                    if (seenCardSets.has(key)) {
                        return false;
                    }
                    if (!cardSet.environments.includes(environment.value)) {
                        return false;
                    }

                    seenCardSets.add(key);
                    return true;
                })
                .sort(compareCardSetDisplayOrder)
                .map(cardSet => ({
                    value: cardSet.value,
                    label: cardSet.label,
                    code: cardSet.code
                }))
        }))
        .filter(group => group.options.length > 0);
}

export function buildScopedCardSetOptions(cards, {
    selectedEnvironment = '',
    getCardSetInfo,
    normalizeEnvironment
} = {}) {
    const normalizedSelected = normalizeEnvironmentValue(selectedEnvironment, normalizeEnvironment);
    if (!normalizedSelected) {
        return [];
    }

    return aggregateCardSets(cards, getCardSetInfo, normalizeEnvironment)
        .filter(cardSet => cardSet.environments.includes(normalizedSelected))
        .sort(compareCardSetDisplayOrder)
        .map(cardSet => ({
            value: cardSet.value,
            label: cardSet.label,
            code: cardSet.code
        }));
}
