// File: Server/Domain/Buffs/BuffStackingRules.cs
//
// 相关文档:
// - Buff系统: 文档/05-游戏系统/10-动态效果与Buff系统.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// 简化的Buff叠加验证结果
    /// 在简化的叠加系统中，主要用于日志记录和调试
    /// </summary>
    public class BuffValidationResult
    {
        /// <summary>是否允许添加（在简化系统中始终为true）</summary>
        public bool AllowAdd { get; set; } = true;

        /// <summary>验证信息</summary>
        public string Message { get; set; } = "Buff added successfully - additive stacking system";
    }

    /// <summary>
    /// 简化的Buff叠加结果
    /// 在简化系统中，大部分Buff都被允许添加，只有非叠加Buff需要替换处理
    /// </summary>
    public class BuffStackingResult
    {
        /// <summary>是否允许添加</summary>
        public bool AllowAdd { get; set; } = true;

        /// <summary>需要移除的Buff列表（用于非叠加Buff的替换）</summary>
        public List<BuffConfig> BuffsToRemove { get; set; } = new();

        /// <summary>操作原因（主要用于日志记录）</summary>
        public string Reason { get; set; } = "Additive stacking - all buffs allowed";
    }

    /// <summary>
    /// 简化的Buff叠加引擎
    /// 实现简单的加法叠加系统，所有Buff都被允许添加和叠加
    /// </summary>
    public class BuffStackingEngine
    {
        private readonly ILogger<BuffStackingEngine> _logger;

        public BuffStackingEngine(ILogger<BuffStackingEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 简化的Buff叠加评估
        /// 在简化系统中，大部分Buff都被允许添加，只有特殊的同名Buff规则
        /// </summary>
        /// <param name="newBuff">要添加的新Buff</param>
        /// <param name="existingBuffs">已存在的Buff列表</param>
        /// <returns>叠加结果</returns>
        public BuffStackingResult EvaluateStacking(BuffConfig newBuff, List<BuffConfig> existingBuffs)
        {
            try
            {
                _logger.LogDebug("Evaluating buff stacking for {BuffId}. Existing buffs: {ExistingCount}. Simplified stacking system.",
                    newBuff.Id, existingBuffs.Count);

                // 特殊规则：检查非叠加Buff
                // 只有明确标记为IsNonStackable=true的能力创建的Buff才需要替换处理
                if (CheckNonStackableBuff(newBuff, existingBuffs, out var buffsToReplace))
                {
                    _logger.LogDebug("Non-stackable buff detected for {BuffId}. Will replace {ReplaceCount} existing buffs",
                        newBuff.Id, buffsToReplace.Count);

                    return new BuffStackingResult
                    {
                        AllowAdd = true,
                        BuffsToRemove = buffsToReplace,
                        Reason = $"Non-stackable buff replacement: removing {buffsToReplace.Count} existing buffs from same ability"
                    };
                }

                return new BuffStackingResult
                {
                    AllowAdd = true,
                    Reason = $"Additive stacking system - Buff {newBuff.Id} added successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during buff stacking evaluation for buff {BuffId}", newBuff.Id);
                // Even on error, allow the buff to be added in the simplified system
                return new BuffStackingResult
                {
                    AllowAdd = true,
                    Reason = $"Error during evaluation but allowing buff due to additive stacking: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 检查非叠加Buff规则
        /// 只有明确标记为IsNonStackable=true的Buff才需要替换处理
        /// </summary>
        /// <param name="newBuff">新Buff</param>
        /// <param name="existingBuffs">现有Buff列表</param>
        /// <param name="buffsToReplace">需要替换的Buff列表</param>
        /// <returns>是否需要进行替换</returns>
        private bool CheckNonStackableBuff(BuffConfig newBuff, List<BuffConfig> existingBuffs, out List<BuffConfig> buffsToReplace)
        {
            buffsToReplace = new List<BuffConfig>();

            // 检查新Buff是否标记为非叠加
            if (!newBuff.IsNonStackable)
            {
                return false; // 可以叠加，无需替换
            }

            // 查找来自相同能力的现有Buff（不管它们是否也是非叠加的）
            // 当新Buff是非叠加时，应该替换来自相同能力的所有现有Buff
            var sameAbilityBuffs = existingBuffs
                .Where(b => b.IsActive &&
                           b.SourceAbility.DefinitionId == newBuff.SourceAbility.DefinitionId)
                .ToList();

            if (sameAbilityBuffs.Any())
            {
                buffsToReplace = sameAbilityBuffs;
                return true; // 需要替换
            }

            return false; // 没有冲突的Buff
        }



        /// <summary>
        /// 验证Buff是否有效（在简化系统中主要用于日志记录）
        /// </summary>
        /// <param name="buff">要验证的Buff</param>
        /// <returns>验证结果</returns>
        public BuffValidationResult ValidateBuff(BuffConfig buff)
        {
            try
            {
                if (buff == null)
                {
                    return new BuffValidationResult
                    {
                        AllowAdd = false,
                        Message = "Buff is null"
                    };
                }

                var effectCount = buff.Effects?.Count ?? 0;
                var keywords = buff.GetGrantedKeywords().ToList();
                var statMods = buff.GetStatModifications().ToList();
                var abilities = buff.GetGrantedAbilityConfigs().ToList();

                _logger.LogDebug("Validating Buff {BuffId}: {EffectCount} effects, {KeywordCount} keywords, {StatModCount} stat modifications, {AbilityCount} abilities",
                    buff.Id, effectCount, keywords.Count, statMods.Count, abilities.Count);

                return new BuffValidationResult
                {
                    AllowAdd = true,
                    Message = $"Buff {buff.Id} validated successfully - additive stacking system"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating buff {BuffId}", buff?.Id.ToString() ?? "null");
                return new BuffValidationResult
                {
                    AllowAdd = true, // Still allow in simplified system
                    Message = $"Validation error but allowing buff: {ex.Message}"
                };
            }
        }
    }
}
