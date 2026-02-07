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
using ShadowverseBeyond.Server.Domain.Constants;

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
    /// 在简化系统中，所有Buff都允许添加，不进行替换处理
    /// </summary>
    public class BuffStackingResult
    {
        /// <summary>是否允许添加</summary>
        public bool AllowAdd { get; set; } = true;

        /// <summary>需要移除的Buff列表（用于非叠加Buff的替换）</summary>
        public List<Buff> BuffsToRemove { get; set; } = new();

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
        /// 在简化系统中，所有Buff都被允许添加，不执行替换
        /// </summary>
        /// <param name="newBuff">要添加的新Buff</param>
        /// <param name="existingBuffs">已存在的Buff列表</param>
        /// <returns>叠加结果</returns>
        public BuffStackingResult EvaluateStacking(Buff newBuff, List<Buff> existingBuffs)
        {
            try
            {
                _logger.LogDebug("Evaluating buff stacking for {BuffId}. Existing buffs: {ExistingCount}. Simplified stacking system.",
                    newBuff.Id, existingBuffs.Count);

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
        /// 验证Buff是否有效（在简化系统中主要用于日志记录）
        /// </summary>
        /// <param name="buff">要验证的Buff</param>
        /// <returns>验证结果</returns>
        public BuffValidationResult ValidateBuff(Buff buff)
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
                List<CardKeyword> keywords = buff.GetGrantedKeywords().ToList();
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
