using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Metadata;
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("替换事件", "在事件发生前截取并替换触发事件。", AbilityEditorCategories.SpecialId)]
    public class ReplaceEventEffectParams : BaseEffectParams
    {
        [ParameterMetadata("替换事件类型", UiControlType.Dropdown, Description = "要替换成的事件类型。")]
        public EventType ReplacementEventType { get; set; }

        public Dictionary<string, object?> ReplacementParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
