using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 能力触发事件，用于播报能力标签
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class AbilityTriggeredEvent : GameEventBase
    {
        public AbilityTriggeredEvent(Ability ability, IGameEvent triggerEvent) : base("ability_triggered")
        {
            SourceCard = ability.SourceCard;
            SourcePlayer = ability.SourceCard?.OwnerPlayerId != null ?
                new PlayerStateDto { PlayerId = ability.SourceCard.OwnerPlayerId } : null;
            Parameters["ability_tag"] = ability.AbilityTag;
            Parameters["trigger_event"] = triggerEvent;
            Parameters["trigger_event_type"] = triggerEvent.EventType;
            Parameters["ability_id"] = ability.AbilityId;
        }

        public AbilityTriggeredEvent(AbilityConfig abilityConfig, IGameEvent triggerEvent) : base("ability_triggered")
        {
            SourceCard = abilityConfig.SourceCard;
            SourcePlayer = abilityConfig.SourceCard?.OwnerPlayerId != null ?
                new PlayerStateDto { PlayerId = abilityConfig.SourceCard.OwnerPlayerId } : null;
            Parameters["ability_tag"] = abilityConfig.AbilityTag;
            Parameters["trigger_event"] = triggerEvent;
            Parameters["trigger_event_type"] = triggerEvent.EventType;
            Parameters["ability_id"] = abilityConfig.Id;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // 这个Event本身不执行逻辑，只是用于播报
            // 其他能力可以监听这个Event来响应特定标签的能力触发
            await Task.CompletedTask;
            return new List<IGameEvent>();
        }
    }
}
