using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Reflection;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;

namespace ShadowverseBeyond.Server.Controllers
{
    /// <summary>
    /// 本地化API控制器 - 提供枚举的本地化文本
    /// </summary>
    [ApiController]
    [Route("api/localization")]
    public class LocalizationController : ControllerBase
    {
        private readonly ILogger<LocalizationController> _logger;

        public LocalizationController(ILogger<LocalizationController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 获取指定枚举的本地化文本
        /// </summary>
        /// <param name="enumName">枚举名称</param>
        /// <returns>枚举值到本地化文本的映射</returns>
        [HttpGet("enums/{enumName}")]
        public ActionResult<Dictionary<string, string>> GetEnumLocalization(string enumName)
        {
            try
            {
                _logger.LogInformation("Getting localization for enum: {EnumName}", enumName);

                var enumType = GetEnumType(enumName);
                if (enumType == null)
                {
                    _logger.LogWarning("Enum type not found: {EnumName}", enumName);
                    return NotFound($"Enum '{enumName}' not found");
                }

                var localization = GetEnumLocalization(enumType);
                _logger.LogInformation("Successfully retrieved localization for {EnumName} with {Count} entries",
                    enumName, localization.Count);

                return Ok(localization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get localization for enum: {EnumName}", enumName);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// 根据枚举名称获取枚举类型
        /// </summary>
        private Type? GetEnumType(string enumName)
        {
            // 定义支持的枚举类型映射
            var enumTypes = new Dictionary<string, Type>
            {
                { "CardType", typeof(CardType) },
                { "CardClass", typeof(CardClass) },
                { "CardRarity", typeof(CardRarity) },
                { "TriggerTiming", typeof(TriggerTiming) },
                { "TriggerRelationType", typeof(TriggerRelationType) },
                { "EventType", typeof(EventType) },
                { "CardZone", typeof(CardZone) },
                { "CardMoveReason", typeof(CardMoveReason) },
                { "EffectType", typeof(EffectType) },
                { "TargetType", typeof(TargetType) },
                { "CounterOperation", typeof(CounterOperation) },
                { "FailureHandling", typeof(FailureHandling) },
                { "CardKeyword", typeof(CardKeyword) },
                { "AbilityKeyword", typeof(AbilityKeyword) },
                { "PlayerScope", typeof(PlayerScope) },
                { "CardSourceMode", typeof(CardSourceMode) },
                { "DurationType", typeof(DurationType) },
                { "DamageDistributionMode", typeof(DamageDistributionMode) },
                { "KeywordOperation", typeof(KeywordOperation) },
                { "DamageKind", typeof(DamageKind) },
                { "EventReasonSource", typeof(EventReasonSource) },
                { "ModifierLayer", typeof(ModifierLayer) },
                { "StatType", typeof(StatType) },
                { "PlayMode", typeof(PlayMode) },
                { "RemoveAbilityMode", typeof(RemoveAbilityMode) },
                { "SequenceOrder", typeof(SequenceOrder) },
                { "DeckInsertMode", typeof(DeckInsertMode) },
            };

            return enumTypes.TryGetValue(enumName, out var type) ? type : null;
        }

        /// <summary>
        /// 获取枚举的本地化文本
        /// </summary>
        private Dictionary<string, string> GetEnumLocalization(Type enumType)
        {
            var result = new Dictionary<string, string>();

            foreach (var value in Enum.GetValues(enumType))
            {
                var memberInfo = enumType.GetMember(value.ToString()!).FirstOrDefault();
                var descriptionAttribute = memberInfo?.GetCustomAttribute<DescriptionAttribute>();

                var enumName = value.ToString()!;
                var localizedText = descriptionAttribute?.Description ?? GetDefaultLocalization(enumName);

                result[enumName] = localizedText;
            }

            return result;
        }

        /// <summary>
        /// 获取默认的本地化文本（如果没有Description特性）
        /// </summary>
        private string GetDefaultLocalization(string enumValue)
        {
            // 为常见的枚举值提供默认的中文翻译
            var defaultLocalizations = new Dictionary<string, string>
            {
                // CardType
                { "Follower", "随从" },
                { "Spell", "法术" },
                { "Amulet", "护符" },
                { "Crest", "纹章" },
                { "Leader", "主战者" },

                // CardClass
                { "Neutral", "中立" },
                { "Forestcraft", "精灵" },
                { "Swordcraft", "皇家护卫" },
                { "Runecraft", "巫师" },
                { "Dragoncraft", "龙族" },
                { "Abysscraft", "梦魇" },
                { "Havencraft", "主教" },
                { "Portalcraft", "复仇者" },

                // CardRarity
                { "Bronze", "青铜" },
                { "Silver", "白银" },
                { "Gold", "黄金" },
                { "Legendary", "传说" },
                { "Special", "特殊" },

                // TriggerTiming
                { "Before", "之前" },
                { "On", "时" },
                { "After", "之后" },

                // TriggerRelationType
                { "Any", "任意" },
                { "Self", "自身" },
                { "Ally", "友方" },
                { "Enemy", "敌方" },

                // EventReasonSource
                { "System", "系统" },
                { "Ability", "能力" },

                // EventType
                { "CardPlayed", "卡牌打出" },
                { "CardDestroyed", "卡牌死亡" },
                { "TurnStart", "回合开始" },
                { "TurnEnd", "回合结束" },
                { "DamageDealt", "造成伤害" },
                { "DamageTaken", "受到伤害" },

                // CardZone
                { "Hand", "手牌" },
                { "Battlefield", "战场" },
                { "Graveyard", "墓地" },
                { "Deck", "卡组" },
                { "Exile", "放逐区" },

                // FailureHandling
                { "AbortAbility", "中止能力" },
                { "SkipEffect", "跳过效果" },
                { "CompleteWithFailure", "失败仍完成" },
                { "UseDefault", "使用默认值" },

                // PlayerScope
                { "Opponent", "对手" },
                { "Both", "双方" },

                // CardSourceMode
                { "Definition", "定义" },
                { "Instance", "实例" },
                { "Copy", "复制" },

                // ModifierLayer
                { "Base", "基础" },
                { "Additive", "修正" },
                { "Set", "设置" },
                { "Multiplicative", "倍乘" },

                // StatType
                { "Attack", "攻击力" },
                { "Health", "生命值" },
                { "Cost", "费用" }
            };

            return defaultLocalizations.TryGetValue(enumValue, out var localized) ? localized : enumValue;
        }
    }
}
