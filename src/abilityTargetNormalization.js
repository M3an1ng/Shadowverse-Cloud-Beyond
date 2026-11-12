// src/abilityTargetNormalization.js
// 统一归一化能力配置中的 targets（兼容对象字典形态与旧版伪 DSL 表达式）及全链路目标池键引用

const LEGACY_POOL_EXPR_MAP = {
    '[SourcePlayer.Leader]': 'context.owner.leader',
    'SourcePlayer.Leader': 'context.owner.leader',
    '[OpponentPlayer.Leader]': 'context.opponent.leader',
    'OpponentPlayer.Leader': 'context.opponent.leader',
    'SourcePlayer.Field': 'context.owner.field.query(self.type == "Follower")',
    'OpponentPlayer.Field': 'context.opponent.field.query(self.type == "Follower")',
    '[SourceCard]': 'context.sourceCard',
    'SourceCard': 'context.sourceCard',
    'SourcePlayer.Hand': 'context.owner.hand',
    'OpponentPlayer.Hand': 'context.opponent.hand'
};

function escapeRegExp(str) {
    return String(str).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function remapTargetKeyRefsInDsl(expr, keyRemap) {
    const raw = String(expr ?? '');
    if (!raw || !keyRemap || keyRemap.size === 0) return raw;

    const activeEntries = [...keyRemap.entries()]
        .filter(([oldKey]) => Boolean(oldKey))
        .sort((a, b) => b[0].length - a[0].length);

    if (activeEntries.length === 0) return raw;

    const lowerFallbackMap = new Map(
        activeEntries.map(([oldKey, newKey]) => [oldKey.toLowerCase(), newKey])
    );
    const keyAlternation = activeEntries.map(([oldKey]) => escapeRegExp(oldKey)).join('|');
    const pattern = new RegExp(
        `(?<![A-Za-z0-9_.])(ResolvedTargets|AbilityTargets|context\\.resolvedTargets)\\.(${keyAlternation})(?![A-Za-z0-9_])`,
        'gi'
    );

    return raw.replace(pattern, (full, prefix, matchedKey) => {
        const mapped = keyRemap.get(matchedKey) ?? lowerFallbackMap.get(matchedKey.toLowerCase());
        return mapped ? `${prefix}.${mapped}` : full;
    });
}

function migrateLegacyDslText(expr, keyRemap = null) {
    const raw = String(expr ?? '').trim();
    if (!raw) return '';
    if (LEGACY_POOL_EXPR_MAP[raw]) {
        return LEGACY_POOL_EXPR_MAP[raw];
    }
    const migrated = raw
        .replace(/\bSourcePlayer\.Combo\b/g, 'context.owner.combo')
        .replace(/\bSourcePlayer\.Rally\b/g, 'context.owner.rally')
        .replace(/\bSourcePlayer\.MaxPlayPoints\b/g, 'context.owner.maxPlayPoints');
    return remapTargetKeyRefsInDsl(migrated, keyRemap);
}

function remapSelectorToken(token, keyRemap) {
    const raw = String(token ?? '').trim();
    if (!raw || !keyRemap || keyRemap.size === 0) return raw;

    if (keyRemap.has(raw)) {
        return keyRemap.get(raw);
    }

    const prefixMatch = /^(ResolvedTargets\.)(.+)$/i.exec(raw);
    if (prefixMatch) {
        const innerKey = prefixMatch[2].trim();
        if (keyRemap.has(innerKey)) {
            return `${prefixMatch[1]}${keyRemap.get(innerKey)}`;
        }
    }

    return raw;
}

function normalizeSingleTargetEntry(rawTarget, keyRemap) {
    if (!rawTarget || typeof rawTarget !== 'object') {
        return rawTarget;
    }

    const next = structuredClone(rawTarget);
    if (next.query && typeof next.query === 'object') {
        let expr = migrateLegacyDslText(next.query.sourcePoolExpression, keyRemap);
        const rawOps = Array.isArray(next.query.operations) ? next.query.operations : [];
        const validOps = [];

        for (const op of rawOps) {
            if (op && !op.$type && String(op.type).toLowerCase() === 'take' && String(op.strategy).toLowerCase() === 'random') {
                const cnt = Number(op.count?.value ?? op.count ?? 1) || 1;
                if (expr && !/\.take\s*\(/i.test(expr)) {
                    expr = `${expr}.take(random ${cnt})`;
                }
                continue;
            }
            validOps.push(op);
        }

        next.query = {
            ...next.query,
            sourcePoolExpression: expr,
            operations: validOps
        };
    }

    if (next.playerChoiceOptions && typeof next.playerChoiceOptions === 'object') {
        const pco = { ...next.playerChoiceOptions };
        if (pco.minCount === undefined && pco.minSelections !== undefined) {
            pco.minCount = pco.minSelections;
        }
        if (pco.maxCount === undefined && pco.maxSelections !== undefined) {
            pco.maxCount = pco.maxSelections;
        }
        delete pco.minSelections;
        delete pco.maxSelections;
        delete pco.selectionTimeoutSeconds;
        next.playerChoiceOptions = pco;
    }

    return next;
}

function remapEffectDslFormulas(effect, keyRemap) {
    if (!effect || typeof effect !== 'object' || !keyRemap || keyRemap.size === 0) {
        return effect;
    }

    const next = structuredClone(effect);
    const params = next.parameters ?? next.Parameters;
    if (params && typeof params === 'object') {
        for (const valCfg of Object.values(params)) {
            if (valCfg && typeof valCfg === 'object') {
                if (typeof valCfg.Formula === 'string') {
                    valCfg.Formula = remapTargetKeyRefsInDsl(valCfg.Formula, keyRemap);
                }
                if (typeof valCfg.formula === 'string') {
                    valCfg.formula = remapTargetKeyRefsInDsl(valCfg.formula, keyRemap);
                }
            }
        }
    }

    const outputs = next.outputs ?? next.Outputs;
    if (outputs && typeof outputs === 'object') {
        for (const [outKey, outFormula] of Object.entries(outputs)) {
            if (typeof outFormula === 'string') {
                outputs[outKey] = remapTargetKeyRefsInDsl(outFormula, keyRemap);
            }
        }
    }

    return next;
}

/**
 * 将对象字典或数组形态的 targets 统一归一化为标准数组，并同步修正 conditions 与 effects 中的目标池引用
 */
export function normalizeAbilityTargetsAndRefs(config) {
    if (!config || typeof config !== 'object') {
        return { targets: [], conditions: [], effects: [] };
    }

    const rawTargets = config.targets ?? config.Targets;
    const keyRemap = new Map();
    const rawTargetEntries = [];

    if (Array.isArray(rawTargets)) {
        rawTargets.forEach((item, idx) => {
            if (item && typeof item.key === 'string' && item.key.trim()) {
                keyRemap.set(item.key.trim(), `target${idx + 1}`);
            }
            rawTargetEntries.push(item);
        });
    } else if (rawTargets && typeof rawTargets === 'object') {
        Object.entries(rawTargets).forEach(([legacyKey, item], idx) => {
            keyRemap.set(legacyKey, `target${idx + 1}`);
            rawTargetEntries.push(item);
        });
    }

    const targets = rawTargetEntries.map(item => {
        const normalized = normalizeSingleTargetEntry(item, keyRemap);
        if (normalized && typeof normalized === 'object') {
            delete normalized.key;
        }
        return normalized;
    });

    const rawConditions = Array.isArray(config.conditions ?? config.Conditions)
        ? (config.conditions ?? config.Conditions)
        : [];
    const conditions = rawConditions.map(cond => {
        if (!cond || typeof cond !== 'object') return cond;
        return {
            ...cond,
            expression: migrateLegacyDslText(cond.expression ?? cond.Expression ?? '', keyRemap)
        };
    });

    const rawEffects = Array.isArray(config.effects ?? config.Effects)
        ? (config.effects ?? config.Effects)
        : [];
    const effects = rawEffects.map(eff => {
        if (!eff || typeof eff !== 'object') return eff;
        const cloned = remapEffectDslFormulas(eff, keyRemap);
        const rawSel = cloned.targetSelector ?? cloned.TargetSelector;
        if (Array.isArray(rawSel) && keyRemap.size > 0) {
            cloned.targetSelector = rawSel.map(k => remapSelectorToken(k, keyRemap));
        } else if (typeof rawSel === 'string' && keyRemap.size > 0) {
            cloned.targetSelector = rawSel
                .split(/[|,;]+/)
                .map(s => remapSelectorToken(s, keyRemap))
                .filter(Boolean);
        }
        return cloned;
    });

    return { targets, conditions, effects };
}
