// 授予能力的输入、复制与嵌套编辑输出共享转换规则，不依赖效果编辑器实例。

export function normalizeGrantedAbilityConfig(ability) {
    if (!ability || typeof ability !== 'object') return null;

    const normalized = { ...ability };
    const name = ability.name ?? ability.Name ?? '';
    const description = ability.description ?? ability.Description ?? '';
    const keywords = ability.abilityKeywords ?? ability.AbilityKeywords ?? [];
    const trigger = ability.trigger ?? ability.Trigger ?? {
        eventTypes: [],
        relationType: '',
        timing: 'On',
        validZones: []
    };

    normalized.name = String(name || '').trim() || '未命名能力';
    normalized.description = description ?? '';
    normalized.abilityKeywords = Array.isArray(keywords) ? keywords : [];
    const legacyExecutionKind = ability.executionKind ?? ability.ExecutionKind ?? null;
    const rawType = ability.type ?? ability.Type ?? null;
    normalized.type = rawType
        || (String(legacyExecutionKind).toLowerCase() === 'aura' ? 'Continuous' : 'Triggered');
    normalized.trigger = trigger;
    normalized.conditions = ability.conditions ?? ability.Conditions ?? [];
    normalized.targets = ability.targets ?? ability.Targets ?? [];
    normalized.effects = ability.effects ?? ability.Effects ?? [];
    normalized.auras = ability.auras ?? ability.Auras ?? [];
    normalized.counterRegistrations = ability.counterRegistrations ?? ability.CounterRegistrations ?? [];
    normalized.playerCounterRegistrations = ability.playerCounterRegistrations ?? ability.PlayerCounterRegistrations ?? [];
    normalized.isActive = ability.isActive ?? ability.IsActive ?? ability.isEnabled ?? ability.IsEnabled ?? true;
    normalized.broadcastOnTrigger = ability.broadcastOnTrigger ?? ability.BroadcastOnTrigger ?? true;
    normalized.buffDisplay = ability.buffDisplay ?? ability.BuffDisplay ?? normalized.broadcastOnTrigger;
    normalized.version = ability.version ?? ability.Version ?? '1.0';

    delete normalized.id;
    delete normalized.Id;
    delete normalized.cardId;
    delete normalized.CardId;
    delete normalized.abilityType;
    delete normalized.priority;
    delete normalized.jsonConfig;
    delete normalized.executionKind;
    delete normalized.ExecutionKind;

    return normalized;
}

function parseAbilityDefinitionConfig(ability) {
    const raw = ability?.jsonConfig ?? ability?.JsonConfig ?? null;
    if (!raw) return null;
    const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
        throw new TypeError('能力 JSON 配置必须是对象');
    }
    return parsed;
}

function normalizeGrantedAbilityDefinition(ability, parsedConfig) {
    if (!ability && !parsedConfig) return null;
    const base = parsedConfig || {};
    const broadcastOnTrigger = ability?.broadcastOnTrigger ?? ability?.BroadcastOnTrigger ?? base.broadcastOnTrigger ?? true;
    const config = {
        ...base,
        name: ability?.name ?? ability?.Name ?? base.name ?? '未命名能力',
        description: ability?.description ?? ability?.Description ?? base.description ?? '',
        abilityKeywords: ability?.abilityKeywords ?? ability?.AbilityKeywords ?? base.abilityKeywords ?? [],
        isActive: ability?.isActive ?? ability?.IsActive ?? base.isActive ?? true,
        broadcastOnTrigger,
        buffDisplay: ability?.buffDisplay ?? ability?.BuffDisplay ?? base.buffDisplay ?? broadcastOnTrigger,
        version: base.version ?? base.Version ?? '1.0'
    };
    return normalizeGrantedAbilityConfig(config);
}

export function createGrantedAbilityEditorInput(ability) {
    if (!ability) {
        return {
            name: '',
            description: '',
            abilityKeywords: [],
            trigger: {
                eventTypes: [],
                relationType: '',
                timing: 'On',
                validZones: []
            },
        conditions: [],
        targets: [],
        effects: [],
        auras: [],
        type: 'Triggered',
        counterRegistrations: [],
        playerCounterRegistrations: [],
        isActive: true,
        broadcastOnTrigger: true,
        buffDisplay: true
    };
    }

    const broadcastOnTrigger = ability.broadcastOnTrigger ?? ability.BroadcastOnTrigger ?? true;
    return {
        name: ability.name ?? ability.Name ?? '',
        description: ability.description ?? ability.Description ?? '',
        abilityKeywords: ability.abilityKeywords ?? ability.AbilityKeywords ?? [],
        type: ability.type ?? ability.Type ?? 'Triggered',
        trigger: ability.trigger ?? ability.Trigger ?? {
            eventTypes: [],
            relationType: '',
            timing: 'On',
            validZones: []
        },
        conditions: ability.conditions ?? ability.Conditions ?? [],
        targets: ability.targets ?? ability.Targets ?? [],
        effects: ability.effects ?? ability.Effects ?? [],
        auras: ability.auras ?? ability.Auras ?? [],
        counterRegistrations: ability.counterRegistrations ?? ability.CounterRegistrations ?? [],
        playerCounterRegistrations: ability.playerCounterRegistrations ?? ability.PlayerCounterRegistrations ?? [],
        isActive: ability.isActive ?? ability.IsActive ?? ability.isEnabled ?? ability.IsEnabled ?? true,
        broadcastOnTrigger,
        buffDisplay: ability.buffDisplay ?? ability.BuffDisplay ?? broadcastOnTrigger
    };
}

export function extractGrantedAbilityEditorConfig(finalConfig) {
    if (!finalConfig || typeof finalConfig !== 'object') return null;

    const parsed = parseAbilityDefinitionConfig(finalConfig);
    if (parsed) return normalizeGrantedAbilityConfig(parsed);

    const fallback = {
        name: finalConfig.name ?? '',
        description: finalConfig.description ?? '',
        abilityKeywords: finalConfig.abilityKeywords ?? [],
        type: finalConfig.type ?? 'Triggered',
        trigger: finalConfig.trigger ?? {
            eventTypes: [],
            relationType: '',
            timing: 'On',
            validZones: []
        },
        conditions: finalConfig.conditions ?? [],
        targets: finalConfig.targets ?? [],
        effects: finalConfig.effects ?? [],
        auras: finalConfig.auras ?? [],
        counterRegistrations: finalConfig.counterRegistrations ?? [],
        playerCounterRegistrations: finalConfig.playerCounterRegistrations ?? [],
        isActive: finalConfig.isActive ?? true,
        broadcastOnTrigger: finalConfig.broadcastOnTrigger ?? true,
        buffDisplay: finalConfig.buffDisplay ?? true,
        version: finalConfig.version ?? '1.0'
    };

    return normalizeGrantedAbilityConfig(fallback);
}

export function copyGrantedAbilityConfig(ability) {
    return normalizeGrantedAbilityDefinition(ability, parseAbilityDefinitionConfig(ability));
}

export function normalizeInheritedTriggerFields(trigger) {
    const source = trigger && typeof trigger === 'object' && !Array.isArray(trigger)
        ? trigger
        : {};
    const rawEventTypes = source.eventTypes ?? source.EventTypes ?? [];
    const rawValidZones = source.validZones ?? source.ValidZones ?? [];
    return {
        eventTypes: Array.isArray(rawEventTypes) ? rawEventTypes.filter(Boolean) : [],
        relationType: source.relationType ?? source.RelationType ?? '',
        timing: source.timing ?? source.Timing ?? 'On',
        validZones: Array.isArray(rawValidZones) ? rawValidZones.filter(Boolean) : [],
        revalidateZoneBeforeExecution: !!(source.revalidateZoneBeforeExecution ?? source.RevalidateZoneBeforeExecution ?? false)
    };
}

export function mergeInheritedTriggerFields(trigger, inheritedTrigger) {
    const existing = trigger && typeof trigger === 'object' && !Array.isArray(trigger)
        ? { ...trigger }
        : {};
    const inherited = normalizeInheritedTriggerFields(inheritedTrigger);
    const merged = {
        ...existing,
        eventTypes: [...inherited.eventTypes],
        relationType: inherited.relationType,
        timing: inherited.timing,
        validZones: [...inherited.validZones],
        revalidateZoneBeforeExecution: inherited.revalidateZoneBeforeExecution
    };

    delete merged.EventTypes;
    delete merged.RelationType;
    delete merged.Timing;
    delete merged.ValidZones;
    delete merged.RevalidateZoneBeforeExecution;

    return merged;
}

export function isDecisionAbilityEffect(effectTypeName) {
    const normalized = String(effectTypeName || '').trim().toLowerCase();
    return normalized === 'choose' || normalized === 'random' || normalized === 'sequence';
}
