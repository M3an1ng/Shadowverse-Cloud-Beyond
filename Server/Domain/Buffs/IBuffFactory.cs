using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability class
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Buff工厂接口，负责创建不同类型的Buff实例
    /// </summary>
    public interface IBuffFactory
    {
        /// <summary>
        /// 创建属性修改Buff
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="statType">属性类型</param>
        /// <param name="modificationValue">修改值</param>
        /// <param name="modifierLayer">修改层级</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateStatBuff(
            CardInstance target,
            CardInstance? sourceCard,
            IAbility sourceAbility,
            List<StatModification> statModifications,
            DurationType durationType,
            int? durationParameter,
            int currentTurn);

        /// <summary>
        /// 创建系统属性修改Buff（无来源能力）。
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="statModifications">属性修改列表</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateSystemStatBuff(
            CardInstance target,
            List<StatModification> statModifications,
            DurationType durationType,
            int? durationParameter,
            int currentTurn);

        /// <summary>
        /// 创建关键字Buff
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="keywords">要添加的关键字列表</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateKeywordBuff(
            CardInstance target,
            CardInstance? sourceCard,
            IAbility sourceAbility,
            List<CardKeyword> keywords,
            DurationType durationType,
            int? durationParameter,
            int currentTurn);

        /// <summary>
        /// 创建系统关键字Buff（无来源能力）。
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="keywords">要添加的关键字列表</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateSystemKeywordBuff(
            CardInstance target,
            List<CardKeyword> keywords,
            DurationType durationType,
            int? durationParameter,
            int currentTurn);

        /// <summary>
        /// 创建复合Buff（包含多种效果）
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="effects">效果列表</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateCompositeBuff(
            CardInstance target,
            CardInstance? sourceCard,
            IAbility sourceAbility,
            List<IBuffEffect> effects,
            DurationType durationType,
            int? durationParameter,
            int currentTurn,
            string? definitionId = null);

        /// <summary>
        /// 根据BuffDefinition创建Buff实例（使用缓存）
        /// </summary>
        /// <param name="buffDefinitionId">BuffDefinition ID</param>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="currentTurn">当前回合</param>
        /// <param name="customParameters">自定义参数（可选）</param>
        /// <returns>创建的Buff实例，如果失败则返回null</returns>
        Buff? CreateBuffFromDefinition(
            string buffDefinitionId,
            CardInstance target,
            CardInstance? sourceCard,
            IAbility sourceAbility,
            int currentTurn,
            Dictionary<string, object?>? customParameters = null);

        /// <summary>
        /// 创建一个现有Buff的深拷贝副本
        /// </summary>
        /// <param name="originalBuff">要复制的原始Buff</param>
        /// <returns>一个新的、独立的Buff实例</returns>
        Buff CreateBuffCopy(Buff originalBuff, CardInstance? newTarget = null);

        /// <summary>
        /// Creates a buff that removes (suppresses) keywords from a target.
        /// </summary>
        Buff CreateRemoveKeywordBuff(
            CardInstance target,
            CardInstance? sourceCard,
            IAbility sourceAbility,
            List<CardKeyword> keywordsToRemove,
            DurationType durationType,
            int? durationParameter,
            int currentTurn);

        /// <summary>
        /// 创建系统关键字移除Buff（无来源能力）。
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="keywordsToRemove">要移除的关键字列表</param>
        /// <param name="currentTurn">当前回合</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateSystemRemoveKeywordBuff(
            CardInstance target,
            List<CardKeyword> keywordsToRemove,
            DurationType durationType,
            int? durationParameter,
            int currentTurn);

    }
}
