import {
    ensureAbilityData,
    ensureCardDefinitions,
    getAbilityDataSnapshot,
    getActiveAbilityDataScope
} from '../abilityDataCache.js';
import { normalizeCardPoolScope } from '../cardDefinitionsLoader.js';

function normalizeCardChoices(cards) {
    const entries = (cards || [])
        .map((card, index) => {
            const idRaw = card?.id ?? card?.Id ?? index;
            const id = Number(idRaw);
            if (!Number.isFinite(id) || id <= 0) {
                return null;
            }
            const name = (card?.name ?? card?.Name ?? `Card ${id}`).toString().trim() || `Card ${id}`;
            const description = (card?.description ?? card?.Description ?? '').toString().trim();
            const cost = Number(card?.cost ?? card?.Cost ?? 0);
            const cardClass = (card?.class ?? card?.Class ?? '').toString().trim();
            const cardType = (card?.type ?? card?.Type ?? '').toString().trim();
            const rarity = (card?.rarity ?? card?.Rarity ?? '').toString().trim();
            const searchText = [name, id, cost, description, cardClass, cardType, rarity].join(' ').toLowerCase();
            return { id, name, description, cost, cardClass, cardType, rarity, searchText };
        })
        .filter(Boolean)
        .sort((a, b) => a.id - b.id);
    return entries;
}

function normalizeAbilityChoices(abilities, cardIndex) {
    const entries = (abilities || [])
        .map((ability, index) => {
            const idRaw = ability?.id ?? ability?.Id ?? index;
            const id = Number(idRaw);
            if (!Number.isFinite(id) || id <= 0) {
                return null;
            }
            const name = (ability?.name ?? ability?.Name ?? `Ability ${id}`).toString().trim() || `Ability ${id}`;
            const description = (ability?.description ?? ability?.Description ?? '').toString().trim();
            const cardId = Number(ability?.cardId ?? ability?.CardId ?? 0);
            const cardEntry = cardIndex?.get?.(cardId);
            const cardName = cardEntry?.name ?? '';
            const keywords = Array.isArray(ability?.abilityKeywords)
                ? ability.abilityKeywords.map(item => item?.keyword ?? item?.Keyword ?? '').filter(Boolean)
                : [];
            const keywordText = keywords.join(' ');
            const searchText = [name, id, description, cardName, cardId, keywordText].join(' ').toLowerCase();
            return { id, name, description, cardId, cardName, keywords, searchText };
        })
        .filter(Boolean)
        .sort((a, b) => a.id - b.id);
    return entries;
}

export class DefinitionPickerCatalog {
    #scope = null;
    #cardDefinitionCache = null;
    #cardDefinitionIndex = new Map();
    #cardDefinitionPromise = null;
    #abilityDefinitionCache = null;
    #abilityDefinitionIndex = new Map();
    #abilityDefinitionPromise = null;

    constructor(options = {}) {
        if (options && typeof options === 'object' && options.scope) {
            this.#scope = normalizeCardPoolScope(options.scope);
        } else if (typeof options === 'string' && options) {
            this.#scope = normalizeCardPoolScope(options);
        }
    }

    #resolveScope() {
        return this.#scope || getActiveAbilityDataScope() || 'standard';
    }

    async loadCards() {
        const scope = this.#resolveScope();
        const snapshot = getAbilityDataSnapshot(scope);
        if (snapshot.cards?.length) {
            this.#cardDefinitionCache = normalizeCardChoices(snapshot.cards);
            this.#cardDefinitionIndex = new Map(this.#cardDefinitionCache.map(entry => [entry.id, entry]));
            return this.#cardDefinitionCache;
        }

        if (!this.#cardDefinitionPromise) {
            this.#cardDefinitionPromise = ensureCardDefinitions(scope)
                .then(cards => {
                    this.#cardDefinitionCache = normalizeCardChoices(cards);
                    this.#cardDefinitionIndex = new Map(this.#cardDefinitionCache.map(entry => [entry.id, entry]));
                    return this.#cardDefinitionCache;
                })
                .finally(() => {
                    this.#cardDefinitionPromise = null;
                });
        }

        return await this.#cardDefinitionPromise;
    }

    async loadAbilities() {
        const scope = this.#resolveScope();
        const snapshot = getAbilityDataSnapshot(scope);
        if (snapshot.abilities?.length) {
            await this.loadCards();
            this.#abilityDefinitionCache = normalizeAbilityChoices(snapshot.abilities, this.#cardDefinitionIndex);
            this.#abilityDefinitionIndex = new Map(this.#abilityDefinitionCache.map(entry => [entry.id, entry]));
            return this.#abilityDefinitionCache;
        }

        if (!this.#abilityDefinitionPromise) {
            this.#abilityDefinitionPromise = ensureAbilityData(scope)
                .then(async data => {
                    await this.loadCards();
                    this.#abilityDefinitionCache = normalizeAbilityChoices(data.abilities, this.#cardDefinitionIndex);
                    this.#abilityDefinitionIndex = new Map(this.#abilityDefinitionCache.map(entry => [entry.id, entry]));
                    return this.#abilityDefinitionCache;
                })
                .finally(() => {
                    this.#abilityDefinitionPromise = null;
                });
        }

        return await this.#abilityDefinitionPromise;
    }

    getCards() { return this.#cardDefinitionCache; }

    getCard(id) { return this.#cardDefinitionIndex.get(id); }

    getAbility(id) { return this.#abilityDefinitionIndex.get(id); }
}
