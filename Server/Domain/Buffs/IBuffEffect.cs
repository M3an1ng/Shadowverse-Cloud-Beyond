using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For StatType, ModifierLayer etc. if defined there
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities; // For CardInstance etc.
using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Represents a specific type of modification or effect contained within a Buff.
    /// This interface allows Buffs to compose various effects (stats, keywords, abilities)
    /// without becoming a "fat class".
    /// </summary>
    public interface IBuffEffect
    {
        // Marker interface for now.
        // Specific implementations will hold the actual data.
    }

    // --- Example Concrete Implementations (can be in separate files later) ---

    /// <summary>
    /// Represents modifications to a card's stats within a Buff.
    /// </summary>
    public class StatBuffEffect : IBuffEffect
    {
        public List<StatModification> Modifications { get; init; } = new();

        public StatBuffEffect(List<StatModification> modifications)
        {
            Modifications = modifications ?? throw new ArgumentNullException(nameof(modifications));
        }
    }

    /// <summary>
    /// Represents keywords granted or removed by a Buff.
    /// </summary>
    public class KeywordBuffEffect : IBuffEffect
    {
        public List<CardKeyword> GrantedKeywords { get; init; } = new();
        // public List<string> RemovedKeywords { get; init; } = new(); // Optional: If buffs can remove keywords

        public KeywordBuffEffect(List<CardKeyword> grantedKeywords)
        {
            GrantedKeywords = grantedKeywords ?? throw new ArgumentNullException(nameof(grantedKeywords));
        }
    }

    /// <summary>
    /// Represents abilities granted by a Buff.
    /// </summary>
    public class AbilityBuffEffect : IBuffEffect
    {
        /// <summary>
        /// List of Ability configurations granted by this buff effect.
        /// The AbilityManager will need to instantiate these at runtime when the buff is active.
        /// </summary>
        public List<AbilityConfig> GrantedAbilityConfigs { get; init; } = new();

        public AbilityBuffEffect(List<AbilityConfig> grantedAbilityConfigs)
        {
            GrantedAbilityConfigs = grantedAbilityConfigs ?? throw new ArgumentNullException(nameof(grantedAbilityConfigs));
        }
    }

    /// <summary>
    /// Represents tribes granted by a Buff.
    /// </summary>
    public class TribeBuffEffect : IBuffEffect
    {
        public List<string> AddedTribes { get; init; } = new();

        public TribeBuffEffect(IEnumerable<string> addedTribes)
        {
            AddedTribes = addedTribes?
                .Where(static tribe => !string.IsNullOrWhiteSpace(tribe))
                .Select(static tribe => tribe.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }
    }

    /// <summary>
    /// Represents ability keyword value modifiers (e.g., Act cost adjustments) granted by a Buff.
    /// </summary>
    public class AbilityKeywordBuffEffect : IBuffEffect
    {
        public List<AbilityKeywordModification> Modifications { get; init; } = new();

        public AbilityKeywordBuffEffect(List<AbilityKeywordModification> modifications)
        {
            Modifications = modifications ?? throw new ArgumentNullException(nameof(modifications));
        }
    }

    /// <summary>
    /// Represents per-turn action usage limit modifiers (e.g., Attack/Act/Fusion limits) granted by a Buff.
    /// </summary>
    public class ActionUsageLimitBuffEffect : IBuffEffect
    {
        public List<ActionUsageLimitModification> Modifications { get; init; } = new();

        public ActionUsageLimitBuffEffect(List<ActionUsageLimitModification> modifications)
        {
            Modifications = modifications ?? throw new ArgumentNullException(nameof(modifications));
        }
    }

    /// <summary>
    /// Represents ability suppression rules applied by a Buff.
    /// </summary>
    public class AbilitySuppressionBuffEffect : IBuffEffect
    {
        public RemoveAbilityMode RemoveMode { get; }
        public IReadOnlyList<long> AbilityDefinitionIds { get; }
        public AbilityKeyword AbilityKeyword { get; }
        public Guid? SourceAbilityRuntimeId { get; }

        public AbilitySuppressionBuffEffect(
            RemoveAbilityMode removeMode,
            IEnumerable<long>? abilityDefinitionIds = null,
            AbilityKeyword abilityKeyword = AbilityKeyword.None,
            Guid? sourceAbilityRuntimeId = null)
        {
            RemoveMode = removeMode;
            AbilityKeyword = abilityKeyword;
            SourceAbilityRuntimeId = sourceAbilityRuntimeId;

            var ids = abilityDefinitionIds ?? Array.Empty<long>();
            AbilityDefinitionIds = ids
                .Where(id => id > 0)
                .Distinct()
                .ToList()
                .AsReadOnly();
        }
    }


    /// <summary>
    /// A buff effect that removes (suppresses) specific keywords from a card.
    /// This effect does not grant stats or keywords; it only negates them.
    /// </summary>
    public class RemoveKeywordBuffEffect : IBuffEffect
    {
        /// <summary>
        /// A list of keywords that this effect will remove or suppress.
        /// </summary>
        public IReadOnlyList<CardKeyword> KeywordsToRemove { get; }

        public RemoveKeywordBuffEffect(IEnumerable<CardKeyword> keywordsToRemove)
        {
            KeywordsToRemove = keywordsToRemove?.ToList().AsReadOnly() ?? new List<CardKeyword>().AsReadOnly();
        }
    }

    // --- Supporting Records/Enums (can be moved to a common file) ---

    /// <summary>
    /// Defines a single modification to a specific stat.
    /// </summary>
    /// <param name="Stat">The type of stat being modified (e.g., Attack, Defense, Cost).</param>
    /// <param name="Value">The value of the modification (can be positive or negative; multiplicative uses direct multiplier).</param>
    /// <param name="Layer">The layer in which this modification applies (determines calculation order).</param>
    public record StatModification(StatType Stat, double Value, ModifierLayer Layer);

    /// <summary>
    /// Defines a single modification to a specific ability keyword value.
    /// </summary>
    /// <param name="Keyword">The ability keyword being modified (e.g., Act).</param>
    /// <param name="Value">The value of the modification (can be positive or negative; multiplicative uses direct multiplier).</param>
    /// <param name="Layer">The layer in which this modification applies (determines calculation order).</param>
    public record AbilityKeywordModification(AbilityKeyword Keyword, double Value, ModifierLayer Layer);

    /// <summary>
    /// Defines the layers for applying stat modifications to ensure correct calculation order.
    /// Example: Base -> Additive -> Multiplicative -> Set
    /// </summary>
    public enum ModifierLayer
    {
        /// <summary> Modifies the base value (rarely used by buffs). </summary>
        [Description("基础")]
        Base,
        /// <summary> Additive bonuses (+X). </summary>
        [Description("修正")]
        Additive,
        /// <summary> Multiplicative bonuses (*X). </summary>
        [Description("倍乘")]
        Multiplicative,
        /// <summary> Sets the value directly (=X), usually applied last. </summary>
        [Description("设置")]
        Set
    }

    /// <summary>
    /// Represents the type of stat being targeted by an effect or condition.
    /// Needs to be defined or imported. Assuming it exists in Common/Enums.cs
    /// </summary>
    public enum StatType
    {
        [Description("攻击力")]
        Attack,
        [Description("生命值")]
        Health,
        [Description("费用")]
        Cost
        // Add other relevant stats (MaxEP, etc.)
    }

    public sealed record ActionUsageLimitModification(
        CardActionUsageType ActionType,
        double Value,
        ModifierLayer Layer
    );
}
