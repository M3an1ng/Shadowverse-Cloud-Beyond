export function createDefaultDeckBuilderFilters() {
    return {
        cost: null,
        class: "",
        type: "",
        rarity: "",
        set: "",
        search: ""
    };
}

export function resetDeckBuilderFilters(filters) {
    const defaults = createDefaultDeckBuilderFilters();
    if (!filters || typeof filters !== "object") {
        return defaults;
    }

    Object.assign(filters, defaults);
    return filters;
}
