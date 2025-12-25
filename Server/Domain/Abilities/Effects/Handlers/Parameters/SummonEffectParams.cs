// File: Server/Domain/Abilities/Effects/Handlers/Parameters/SummonUnitEffectParams.cs
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums; // For CardZone

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters specific to the SummonUnit effect.
    /// Defines which card to summon and how many.
    /// </summary>
    [EffectMetadata("召唤", "召唤指定的卡牌到战场上。")]
    public class SummonEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The Definition ID of the card to summon.
        /// This is mandatory for summoning.
        /// </summary>
        [ParameterMetadata("卡牌ID", UiControlType.NumberStepper, DefaultValue = 0, Min = 1, Description = "要召唤的卡牌定义ID。这是召唤效果的必需参数。")]
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// The number of units to summon. Defaults to 1.
        /// </summary>
        [ParameterMetadata("召唤数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "要召唤的单位数量。可以是固定数值，也可以是基于游戏状态的动态公式。")]
        public int SummonCount { get; set; } = 1;


        /// <summary>
        /// Optional: The position index on field (ZoneIndex). Null means append to the end.
        /// </summary>
        [ParameterMetadata("位置索引", UiControlType.NumberStepper, DefaultValue = null, Min = 0, Description = "在战场上的位置索引。留空表示添加到末尾。")]
        public int? Position { get; set; } = null;

        // Potential future parameters:
        // public bool SummonEvolved { get; set; } = false; // Summon the evolved version directly?
        // public Dictionary<string, object> InitialOverrides { get; set; } // Override stats/keywords on summon?
    }
}