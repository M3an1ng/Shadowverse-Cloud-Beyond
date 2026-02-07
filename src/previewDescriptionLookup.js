const cardKeywordGlossary = {
    Storm: {
        name: '疾驰',
        description: '进入战场的当回合即可进行攻击。'
    },
    Ward: {
        name: '守护',
        description: '只要在战场上，便能使敌方无法攻击未拥有守护的从者或主战者。\n若拥有守护的同时也拥有潜行或威慑，则优先发动潜行或威慑，守护将为无效。'
    },
    Bane: {
        name: '必杀',
        description: '透过攻击或被攻击而给予敌方的从者伤害时，将会破坏该从者。\n即便攻击力为0，或因敌方的能力而使所给予的伤害转变为0，同样会发动必杀能力。'
    },
    Rush: {
        name: '突进',
        description: '进入战场的当回合即可攻击从者。'
    },
    Ambush: {
        name: '潜行',
        description: '无法被敌方的能力指定，也不会被敌方的从者攻击。\n当拥有潜行的从者进行攻击或透过能力给予伤害时，即失去潜行能力。'
    },
    Drain: {
        name: '吸血',
        description: '透过攻击给予伤害时，将使自己的主战者回復与该伤害等量的生命值。'
    },
    Countdown: {
        name: '倒数',
        description: '每回合倒数减少，归零时护符被破坏。'
    },
    Aura: {
        name: '灵气',
        description: '无法被敌方的能力指定。\n若为随机指定的能力，则仍有可能成为对象。'
    },
    Pressure: {
        name: '威慑',
        description: '不会被敌方的从者攻击。'
    },
    Shackle: {
        name: '无法攻击',
        description: '该随从不能进行攻击。'
    },
    Shield: {
        name: '屏障',
        description: '拥有障壁的从者或主战者受到伤害时，将使该伤害转变为0。\n障壁发动1次后，便会失去能力。'
    },
    DestroyResist: {
        name: '无法被破坏',
        description: '不受破坏类效果影响。'
    },
    VanishResist: {
        name: '无法被消失',
        description: '不受消失类效果影响。'
    },
    FreeEvolve: {
        name: '免费进化',
        description: '该随从进化不消耗进化点。'
    },
    BanEvolve: {
        name: '无法进化',
        description: '该随从无法进化。'
    },
    EarthSigil: {
        name: '土之印',
        description: '拥有土之印的护符卡进入战场时，土之印数将为1，并标记于卡片右下方。\n若自己战场上有其他的土之印‧护符卡，将使其全部消失并合计土之印数。\n拥有土之印的卡片不会被能力破坏，也无法被敌方的能力指定。\n若土之印数降至0，则会被破坏。\n发动「土之印+2」的能力时，若自己的战场上有土之印‧护符卡，则该土之印数+2；若没有土之印‧护符卡，则会召唤1张『大地之魔片』卡片到自己的战场上，并使其土之印数为2。'
    }
};

const abilityKeywordGlossary = {
    Fanfare: {
        name: '入场曲',
        description: '从手牌中使用并进入战场时，将发动能力。\n未透过「亲自使用卡片」，而是直接从手牌或牌堆中进入战场，或召唤卡片到战场上时，则不会发动。'
    },
    Lastword: {
        name: '谢幕曲',
        description: '被破坏时，将发动能力。\n消失或变身时，则不会发动。'
    },
    AttackEffect: {
        name: '攻击时',
        description: '攻击从者或主战者时，将发动能力。\n能力将于计算伤害前发动。'
    },
    FightEffect: {
        name: '交战时',
        description: '攻击从者或被从者攻击时，将发动能力。\n攻击主战者时，则不会发动能力。\n能力将于计算伤害前发动。'
    },
    EvolveEffect: {
        name: '进化时',
        description: '消耗EP或SEP使卡片进化或超进化时，将发动能力。'
    },
    Combo: {
        name: '连击',
        description: '表示「本回合中自己已使用的卡片张数」。\n部分能力需在连击达指定数值以上时才可发动，使用拥有连击的卡片时，该卡片也将计入已使用的卡片张数中。'
    },
    Rally: {
        name: '协作',
        description: '表示「本次对战中自己已进入战场的从者卡张数」。\n部分能力需在协作达指定数值以上时才可发动。'
    },
    SpellBoost: {
        name: '魔力增幅时',
        description: '自己使用法术卡时，使自己手牌中的全部卡片发动1次魔力增幅。\n虽然发动魔力增幅本身并不会直接影响对战，但拥有「魔力增幅时」的卡片在发动魔力增幅的时候，将触发能力。'
    },
    Awake: {
        name: '觉醒',
        description: '表示「自己的PP最大值为7以上」的状态。'
    },
    Necromance: {
        name: '死灵术',
        description: '自己的墓场数为指定的数值以上时，将消耗等同于该数值的墓场数并发动能力。'
    },
    Venge: {
        name: '复仇',
        description: '主战者生命值较低时触发。'
    },
    Reson: {
        name: '共鸣',
        description: '牌组剩余张数为偶数时触发。'
    },
    Eager: {
        name: '渴望',
        description: '满足渴望条件时触发。'
    },
    EarthRitual: {
        name: '土之秘术',
        description: '自己战场上的土之印数为指定的数值以上时，将消耗等同于该数值的土之印数并发动能力。'
    },
    Enhance: {
        name: '爆能强化',
        description: '使用拥有爆能强化的卡片时，若自己剩馀的PP为指定数值以上，将由原本的消费值转变为消耗等同于爆能强化值的PP并发动能力。'
    },
    Bury: {
        name: '葬送',
        description: '消耗手牌随从以发动效果。'
    },
    Reanimate: {
        name: '亡者召还',
        description: '由本次对战中自己已被破坏且消费不高于指定数值的从者卡中，召唤1张与之同名的从者卡到战场上，并使其获得死者‧类型。\n若消费最高的从者卡有複数张，则会在其中随机选择。\n同名的卡片已被破坏複数次时，该卡片被选择的机率便会提高。'
    },
    Accelerate: {
        name: '激奏',
        description: '以较低费用施放并触发替代效果。'
    },
    Crystalize: {
        name: '结晶',
        description: '以护符形式打出并触发结晶效果。'
    },
    Travel: {
        name: '漫游',
        description: '满足漫游条件时触发。'
    },
    Intromezzo: {
        name: '间奏',
        description: '满足间奏条件时触发。'
    },
    Wrath: {
        name: '狂乱',
        description: '本局自残次数达到条件时触发。'
    },
    Choose: {
        name: '模式',
        description: '由複数能力中选择指定数量的能力并发动。\n虽可选择未达能力发动条件的模式，但该能力并不会发动。'
    },
    Act: {
        name: '启动',
        description: '拥有启动的护符卡，1回合仅限1次，可按下卡片详细内的「策动」按钮发动能力。\n启动可能会标示指定的消费值，若自己剩馀的PP为指定数值以上，将消耗等同于该数值的PP并发动能力。'
    },
    Sacrifice: {
        name: '血祭',
        description: '消耗随从以发动效果。'
    },
    SuperEvolveEffect: {
        name: '超进化时',
        description: '消耗SEP使卡片超进化时，将发动能力。'
    }
};

const keywordNormalizationMap = new Map([
    ['馳', '驰'],
    ['護', '护'],
    ['殺', '杀'],
    ['進', '进'],
    ['潛', '潜'],
    ['數', '数'],
    ['靈', '灵'],
    ['懾', '慑'],
    ['無', '无'],
    ['擊', '击'],
    ['場', '场'],
    ['謝', '谢'],
    ['戰', '战'],
    ['時', '时'],
    ['覺', '觉'],
    ['術', '术']
]);

let keywordLabelLookup = null;
let keywordLabelNormalizedLookup = null;
let keywordLabelPromise = null;

let cardIndex = null;
let cardIndexPromise = null;
let localCardSource = null;
let localCardIndex = null;

function normalizeKeywordLabel(value) {
    const text = String(value ?? '').trim();
    if (!text) return '';
    return Array.from(text)
        .map(char => keywordNormalizationMap.get(char) ?? char)
        .join('');
}

function normalizeCardName(value) {
    return String(value ?? '').trim().toLowerCase();
}

async function fetchEnumLocalization(enumName) {
    const response = await fetch(`/api/localization/enums/${enumName}`);
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    return await response.json();
}

async function loadKeywordLabelLookup() {
    if (keywordLabelLookup && keywordLabelNormalizedLookup) {
        return {
            labelLookup: keywordLabelLookup,
            normalizedLookup: keywordLabelNormalizedLookup
        };
    }

    if (!keywordLabelPromise) {
        keywordLabelPromise = (async () => {
            try {
                const [cardKeywords, abilityKeywords] = await Promise.all([
                    fetchEnumLocalization('CardKeyword'),
                    fetchEnumLocalization('AbilityKeyword')
                ]);

                const labelLookup = new Map();
                const normalizedLookup = new Map();
                const addEntries = (kind, entries) => {
                    Object.entries(entries || {}).forEach(([key, label]) => {
                        if (!label || key === 'None') return;
                        const trimmed = String(label).trim();
                        if (!trimmed) return;
                        const entry = { kind, key, label: trimmed };
                        labelLookup.set(trimmed, entry);
                        const normalized = normalizeKeywordLabel(trimmed);
                        if (!normalizedLookup.has(normalized)) {
                            normalizedLookup.set(normalized, entry);
                        }
                    });
                };

                addEntries('card', cardKeywords);
                addEntries('ability', abilityKeywords);

                keywordLabelLookup = labelLookup;
                keywordLabelNormalizedLookup = normalizedLookup;
            } catch (error) {
                console.warn('PreviewDescriptionLookup: Failed to load keyword localization.', error);
                keywordLabelLookup = new Map();
                keywordLabelNormalizedLookup = new Map();
            }

            return {
                labelLookup: keywordLabelLookup,
                normalizedLookup: keywordLabelNormalizedLookup
            };
        })();
    }

    return await keywordLabelPromise;
}

function createEmptyIndex() {
    return {
        cardsById: new Map(),
        idsByName: new Map()
    };
}

function buildCardIndex(cards) {
    const index = createEmptyIndex();
    (cards || []).forEach(card => {
        const cardId = card?.id ?? card?.CardId ?? card?.Id;
        if (cardId == null) return;
        if (!index.cardsById.has(cardId)) {
            index.cardsById.set(cardId, card);
        }

        const name = card?.name ?? card?.Name;
        if (!name) return;
        const normalized = normalizeCardName(name);
        if (!normalized) return;
        const list = index.idsByName.get(normalized);
        if (list) {
            if (!list.includes(cardId)) {
                list.push(cardId);
            }
        } else {
            index.idsByName.set(normalized, [cardId]);
        }
    });
    return index;
}

function getLocalCardIndex(cards) {
    if (cards === localCardSource && localCardIndex) {
        return localCardIndex;
    }
    localCardSource = cards;
    localCardIndex = buildCardIndex(cards);
    return localCardIndex;
}

function getMatchesByName(index, normalizedName) {
    if (!index || !normalizedName) return [];
    const ids = index.idsByName.get(normalizedName);
    if (!Array.isArray(ids) || ids.length === 0) return [];
    return ids.map(id => index.cardsById.get(id)).filter(Boolean);
}

async function loadCardIndex() {
    if (cardIndex) return cardIndex;
    if (!cardIndexPromise) {
        cardIndexPromise = (async () => {
            try {
                const response = await fetch('/api/cards');
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                const cards = await response.json();
                cardIndex = buildCardIndex(cards);
            } catch (error) {
                console.warn('PreviewDescriptionLookup: Failed to load card definitions.', error);
                cardIndex = createEmptyIndex();
            }
            return cardIndex;
        })();
    }
    return await cardIndexPromise;
}

export async function resolveKeywordInfo(rawText) {
    const label = String(rawText ?? '').trim();
    if (!label) return null;

    const { labelLookup, normalizedLookup } = await loadKeywordLabelLookup();
    const normalized = normalizeKeywordLabel(label);
    const entry = labelLookup.get(label) || normalizedLookup.get(normalized);

    if (!entry) {
        return null;
    }

    const glossary = entry.kind === 'card' ? cardKeywordGlossary : abilityKeywordGlossary;
    const info = glossary[entry.key];
    return {
        kind: entry.kind,
        key: entry.key,
        label: entry.label,
        name: info?.name ?? entry.label ?? label,
        description: info?.description ?? ''
    };
}

export async function resolveDerivedCardInfo(rawText, options = {}) {
    const name = String(rawText ?? '').trim();
    if (!name) return null;

    const normalizedName = normalizeCardName(name);
    if (!normalizedName) return null;

    if (Array.isArray(options.cards)) {
        const index = getLocalCardIndex(options.cards);
        const match = getMatchesByName(index, normalizedName);
        if (match.length) return match;
    }

    const index = await loadCardIndex();
    const matches = getMatchesByName(index, normalizedName);
    return matches?.length ? matches : null;
}

function parseDerivedId(rawText) {
    const text = String(rawText ?? '').trim();
    if (!text) return { name: '', id: null };
    const match = text.match(/^(.*?)(?:\\s*)[（(](\\d+)[）)]$/);
    if (!match) {
        return { name: text, id: null };
    }
    return {
        name: (match[1] ?? '').trim(),
        id: match[2] ?? null
    };
}

export async function resolveDerivedCardMatch(rawText, options = {}) {
    const parsed = parseDerivedId(rawText);
    if (!parsed.name) {
        return { match: null, matches: [], reason: 'not_found', requestedId: parsed.id };
    }

    const normalizedName = normalizeCardName(parsed.name);
    if (!normalizedName) {
        return { match: null, matches: [], reason: 'not_found', requestedId: parsed.id };
    }

    let matches = [];
    if (Array.isArray(options.cards)) {
        const index = getLocalCardIndex(options.cards);
        const localMatches = getMatchesByName(index, normalizedName);
        if (localMatches.length) {
            matches = localMatches;
        }
    }

    if (matches.length === 0) {
        const index = await loadCardIndex();
        matches = getMatchesByName(index, normalizedName);
    }

    if (matches.length === 0) {
        return { match: null, matches: [], reason: 'not_found', requestedId: parsed.id };
    }

    if (parsed.id) {
        const idValue = Number(parsed.id);
        const byId = matches.find(card => {
            const cardId = card?.id ?? card?.CardId ?? card?.Id;
            return Number(cardId) === idValue;
        });
        if (!byId) {
            return { match: null, matches, reason: 'id_not_found', requestedId: parsed.id };
        }
        return { match: byId, matches, reason: null, requestedId: parsed.id };
    }

    if (matches.length > 1) {
        return { match: null, matches, reason: 'multiple', requestedId: null };
    }

    return { match: matches[0], matches, reason: null, requestedId: null };
}
