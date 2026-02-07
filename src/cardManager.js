// src/cardManager.js - 卡牌实例创建
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - API集成: 文档/03-前端架构/03-API集成.md
//
// 架构说明:
// 在2025年7月的重构后，此文件不再维护客户端卡牌数据库。
// 服务器现在被视为单一数据源（Single Source of Truth），
// 并发送包含所有必要信息的“完整”CardInstanceDto。
// 此文件的唯一职责是将这些DTO转换为前端可以使用的运行时对象。

/**
 * 根据从服务器接收到的完整的CardInstanceDto，创建一个前端卡牌实例。
 * 这个函数现在是幂等的，并且不依赖任何外部状态。
 * @param {object} cardDto - 从服务器接收的 CardInstanceDto 对象。
 * @returns {object|null} 一个包含所有卡牌数据的前端实例对象，如果DTO无效则返回null。
 */
export function createCardInstance(cardDto) {
    // [FIX] 修正了验证逻辑，使用 PascalCase (InstanceId)
    const instanceId = cardDto?.InstanceId ?? cardDto?.instanceId;
    if (!cardDto || typeof cardDto !== 'object' || instanceId === undefined) {
        console.error("Cannot create card instance: Invalid or incomplete DTO provided.", cardDto);
        return null;
    }

    // [FIX] 修正了所有属性映射，从 DTO 读取 PascalCase 属性
    const instance = {
        // 静态数据 (由服务器在DTO中提供)
        // NOTE: CardId is serialized as `id` in CardInstanceDto.
        id: cardDto.CardId ?? cardDto.id,
        name: cardDto.Name,
        cost: cardDto.Cost,
        attack: cardDto.Attack,
        health: cardDto.Health,
        maxAttack: cardDto.MaxAttack,
        maxHealth: cardDto.MaxHealth,
        type: cardDto.Type,
        rarity: cardDto.Rarity,
        class: cardDto.Class,
        description: cardDto.Description,
        artPreview: cardDto.ArtPreview ?? cardDto.artPreview ?? null,
        artDetail: cardDto.ArtDetail ?? cardDto.artDetail ?? null,
        keywords: cardDto.Keywords || [],
        tribe: cardDto.Tribe || [],
        earthsigil: cardDto.Earthsigil ?? cardDto.earthsigil ?? cardDto.EarthSigil ?? 0,
        canEvolve: cardDto.CanEvolve ?? cardDto.canEvolve,
        effectDescriptions: cardDto.EffectDescriptions ?? cardDto.effectDescriptions ?? [],

        // 运行时状态 (由服务器在DTO中提供)
        uid: instanceId,
        currentCost: cardDto.CurrentCost,
        currentAttack: cardDto.CurrentAttack,
        currentHealth: cardDto.CurrentHealth,
        isEvolved: cardDto.IsEvolved,
        canAttack: cardDto.CanAttack,
        fieldTurn: cardDto.FieldTurn ?? cardDto.fieldTurn ?? 0,
        hasActAbility: cardDto.HasActAbility ?? false,
        actAbilities: cardDto.ActAbilities ?? cardDto.actAbilities ?? [],
        actLastUsedTurnNumber: cardDto.ActLastUsedTurnNumber ?? null,
        fusionAbilities: cardDto.FusionAbilities ?? cardDto.fusionAbilities ?? [],
        ownerId: cardDto.OwnerPlayerId,
        zone: cardDto.Zone,
        countdown: cardDto.CurrentCountdown,
        buffSummaries: cardDto.BuffSummaries ?? cardDto.buffSummaries ?? [],
        buffStatSummary: cardDto.BuffStatSummary ?? cardDto.buffStatSummary ?? null,
        counters: cardDto.Counters ?? cardDto.counters ?? cardDto.CounterMap ?? cardDto.counterMap ?? {},

        // 纯前端状态 (在本地初始化和管理)
        turnPlayed: null,
        attackCount: 0,
    };

    return instance;
}
