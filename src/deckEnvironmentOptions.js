import { ensureEnumLocalization, getCachedEnumLabel } from "./enumLocalizationCache.js";

const FALLBACK_ENVIRONMENT = "rotation";
const FALLBACK_LABEL = "指定";

let cachedOptions = null;
let inFlight = null;

function normalizeKey(value) {
    return String(value ?? "").trim().toLowerCase();
}

function dedupeOptions(options) {
    const seen = new Set();
    const deduped = [];
    for (const option of options) {
        const key = normalizeKey(option?.value);
        if (!key || seen.has(key)) continue;
        seen.add(key);
        deduped.push({
            value: key,
            label: String(option?.label ?? option?.value ?? key).trim() || key
        });
    }
    return deduped;
}

function sortPreferredFirst(options) {
    const source = Array.isArray(options) ? options : [];
    return [
        ...source.filter((item) => item.value === FALLBACK_ENVIRONMENT),
        ...source.filter((item) => item.value !== FALLBACK_ENVIRONMENT)
    ];
}

function buildOptionsFromLocalization(raw) {
    const entries = Object.entries(raw || {}).map(([enumName, label]) => ({
        value: normalizeKey(enumName),
        label: String(label ?? enumName).trim() || String(enumName)
    }));

    const deduped = sortPreferredFirst(dedupeOptions(entries));
    if (deduped.length > 0) {
        return deduped;
    }

    return [{
        value: FALLBACK_ENVIRONMENT,
        label: getCachedEnumLabel("DeckEnvironment", FALLBACK_ENVIRONMENT, FALLBACK_LABEL) || FALLBACK_LABEL
    }];
}

export async function ensureDeckEnvironmentOptions() {
    if (Array.isArray(cachedOptions) && cachedOptions.length > 0) {
        return cachedOptions;
    }
    if (inFlight) {
        return await inFlight;
    }

    inFlight = (async () => {
        try {
            const localization = await ensureEnumLocalization("DeckEnvironment");
            cachedOptions = buildOptionsFromLocalization(localization?.raw || {});
        } catch (error) {
            console.warn("DeckEnvironmentOptions: failed to load DeckEnvironment localization.", error);
            cachedOptions = buildOptionsFromLocalization({});
        } finally {
            inFlight = null;
        }
        return cachedOptions;
    })();

    return await inFlight;
}

export function getDeckEnvironmentOptions() {
    if (Array.isArray(cachedOptions) && cachedOptions.length > 0) {
        return [...cachedOptions];
    }

    return buildOptionsFromLocalization({});
}

export function getDefaultDeckEnvironment() {
    return getDeckEnvironmentOptions()[0]?.value || FALLBACK_ENVIRONMENT;
}

export function normalizeDeckEnvironment(value, fallback = null) {
    const fallbackValue = fallback === ""
        ? ""
        : (normalizeKey(fallback) || getDefaultDeckEnvironment());
    const hasLoadedEnumOptions = Array.isArray(cachedOptions) && cachedOptions.length > 0;
    if (value === null || value === undefined || value === "") {
        return fallbackValue;
    }

    if (typeof value === "number") {
        const index = Math.trunc(value);
        const options = getDeckEnvironmentOptions();
        if (index >= 0 && index < options.length) {
            return options[index].value;
        }
        if (!hasLoadedEnumOptions) {
            return String(index);
        }
        return index === 0 ? FALLBACK_ENVIRONMENT : fallbackValue;
    }

    const normalized = normalizeKey(value);
    if (!normalized) return fallbackValue;

    const options = getDeckEnvironmentOptions();
    if (options.some((item) => item.value === normalized)) {
        return normalized;
    }

    if (!hasLoadedEnumOptions) {
        return normalized;
    }

    return fallbackValue;
}

export function getDeckEnvironmentLabel(value) {
    const normalized = normalizeDeckEnvironment(value);
    const fromCache = getCachedEnumLabel("DeckEnvironment", normalized, "");
    if (fromCache) return fromCache;

    const fromOptions = getDeckEnvironmentOptions().find((item) => item.value === normalized)?.label;
    if (fromOptions) return fromOptions;

    return String(value ?? "").trim() || FALLBACK_LABEL;
}

export function fillDeckEnvironmentSelect(selectEl, selectedValue = null) {
    if (!selectEl) return;
    const options = getDeckEnvironmentOptions();
    const selected = normalizeDeckEnvironment(selectedValue, options[0]?.value);
    selectEl.innerHTML = options
        .map((option) => {
            const isSelected = option.value === selected ? " selected" : "";
            return `<option value="${option.value}"${isSelected}>${option.label}</option>`;
        })
        .join("");
}
