// File: Server/Services/BuffService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Implements the logic for calculating card stats and keywords based on active buffs.
    /// </summary>
    public class BuffService : IBuffService
    {
        private readonly IBuffManager _buffManager;
        private readonly IConditionEvaluator _conditionEvaluator;

        public BuffService(IBuffManager buffManager, IConditionEvaluator conditionEvaluator)
        {
            _buffManager = buffManager;
            _conditionEvaluator = conditionEvaluator;
        }

        public async Task<int> GetEffectiveAttackAsync(CardInstance card, GameContext gameContext)
        {
            return await CalculateStatWithBuffsAsync(card, StatType.Attack, card.BaseAttack, gameContext);
        }

        public async Task<int> GetEffectiveMaxHealthAsync(CardInstance card, GameContext gameContext)
        {
            return await CalculateStatWithBuffsAsync(card, StatType.Defense, card.BaseHealth, gameContext);
        }

        public async Task<int> GetEffectiveCostAsync(CardInstance card, GameContext gameContext)
        {
            return await CalculateStatWithBuffsAsync(card, StatType.Cost, card.BaseCost, gameContext);
        }

        public async Task<List<CardKeyword>> GetEffectiveKeywordsAsync(CardInstance card, GameContext gameContext)
        {
            if (card.IsSilenced)
            {
                return [];
            }

            var effectiveKeywords = new List<CardKeyword>(card.BaseKeywords);
            effectiveKeywords.AddRange(card.TemporaryKeywords);

            var buffs = await _buffManager.GetBuffsForCardAsync(card.InstanceId);
            foreach (var buff in buffs)
            {
                if (await buff.IsCurrentlyApplicableAsync(card, gameContext, _conditionEvaluator))
                {
                    var buffKeywords = buff.GetGrantedKeywords();
                    foreach (var keywordStr in buffKeywords)
                    {
                        if (Enum.TryParse<CardKeyword>(keywordStr, true, out var keywordEnum) && keywordEnum != CardKeyword.None)
                        {
                            effectiveKeywords.Add(keywordEnum);
                        }
                    }
                }
            }

            return [.. effectiveKeywords.Distinct()];
        }

        private async Task<int> CalculateStatWithBuffsAsync(CardInstance card, StatType statType, int baseValue, GameContext gameContext)
        {
            var currentValue = baseValue;
            var buffs = await _buffManager.GetBuffsForCardAsync(card.InstanceId);

            foreach (var buff in buffs)
            {
                if (await buff.IsCurrentlyApplicableAsync(card, gameContext, _conditionEvaluator))
                {
                    var modifications = buff.GetStatModifications().Where(m => m.Stat == statType);
                    foreach (var mod in modifications.OrderBy(m => m.Layer))
                    {
                        currentValue = ApplyStatModification(currentValue, mod);
                    }
                }
            }

            return Math.Max(0, currentValue);
        }

        private static int ApplyStatModification(int currentValue, StatModification modification)
        {
            return modification.Layer switch
            {
                ModifierLayer.Additive => currentValue + modification.Value,
                ModifierLayer.Multiplicative => (int)(currentValue * (modification.Value / 100.0)),
                ModifierLayer.Set => modification.Value,
                ModifierLayer.Base => modification.Value,
                _ => currentValue,
            };
        }
    }
}