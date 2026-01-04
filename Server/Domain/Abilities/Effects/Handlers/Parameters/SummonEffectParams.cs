// File: Server/Domain/Abilities/Effects/Handlers/Parameters/SummonUnitEffectParams.cs
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums; // For CardZone
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters specific to the SummonUnit effect.
    /// Defines which card to summon and how many.
    /// </summary>
    [EffectMetadata("召唤", "召唤卡牌到战场。定义模式按玩家范围创建新卡；实例模式使用卡牌目标；复制模式复制卡牌目标并按玩家范围归属。")]
    public class SummonEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择召唤卡牌的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("卡牌来源", UiControlType.Dropdown, DefaultValue = CardSourceMode.Definition, Description = "定义模式创建新卡；实例模式使用已选卡牌目标；复制模式复制已选卡牌目标。")]
        public CardSourceMode CardSourceMode { get; set; } = CardSourceMode.Definition;

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
        // [ParameterMetadata("位置索引", UiControlType.NumberStepper, DefaultValue = null, Min = 0, Description = "在战场上的位置索引。留空表示添加到末尾。")]
        // public int? Position { get; set; } = null;

        // Potential future parameters:
        // public bool SummonEvolved { get; set; } = false; // Summon the evolved version directly?
        // public Dictionary<string, object> InitialOverrides { get; set; } // Override stats/keywords on summon?
    }
}
