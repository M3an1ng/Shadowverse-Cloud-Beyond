export function alternatePlayPatch(ability, definitionId) {
    const id = Math.max(0, Number(definitionId) || 0);
    const effects = ability.effects || [];
    const mode = (ability.abilityKeywords || []).find(k => ['Accelerate','Crystalize'].includes(k.keyword))?.keyword;
    const isDeclaration = effect => ['Accelerate','Crystalize',11,12].includes(effect.type ?? effect.Type);
    let updated = effects.map(effect => {
        if (!isDeclaration(effect)) return effect;
        const parameters = Object.fromEntries(Object.entries(effect.parameters ?? effect.Parameters ?? {}).filter(([key]) => key.toLowerCase() !== 'carddefinitionid'));
        return {...effect, parameters:{...parameters,CardDefinitionId:id}};
    });
    if (id && mode && !effects.some(isDeclaration) && effects.every(effect => !(effect.type ?? effect.Type)))
        updated = [{type:mode,parameters:{CardDefinitionId:id},isEnabled:true}];
    const alternate = {...(ability.alternatePlay || {})};
    delete alternate.PlayAsCardDefinitionId;
    return {alternatePlay:id?{...alternate,playAsCardDefinitionId:id}:null,effects:updated};
}

export function alternatePlayFromEffects(ability) {
    const declaration = (ability.effects || []).find(effect => effect.isEnabled !== false && ['Accelerate','Crystalize',11,12].includes(effect.type ?? effect.Type));
    if (!declaration) return ability.alternatePlay || null;
    const parameters = declaration.parameters ?? declaration.Parameters ?? {};
    const raw = parameters.CardDefinitionId ?? parameters.cardDefinitionId;
    const id = Number(raw?.Value ?? raw?.value ?? raw);
    if (!Number.isInteger(id) || id <= 0) return null;
    const alternate = {...(ability.alternatePlay || {})}; delete alternate.PlayAsCardDefinitionId;
    return {...alternate,playAsCardDefinitionId:id};
}
