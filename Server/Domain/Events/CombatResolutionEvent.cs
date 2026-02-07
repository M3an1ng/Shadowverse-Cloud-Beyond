using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("战斗结算", "当攻击与交战效果全部处理完毕后结算战斗伤害。", EventType.CombatResolution, visibility: EventMetadataVisibility.System)]
    public class CombatResolutionEvent : DelayedEventBase, ICommittableEvent
    {
        [GameEventParameter("攻击者", "发起攻击的单位。", DslDisplayTypeName = "事件源")]
        public CardInstance? Attacker => Source?.Card;

        [GameEventParameter("防御者", "被攻击的单位或主战者。", DslDisplayTypeName = "事件目标")]
        public CardInstance? Defender => Target.FirstOrDefault()?.Card;

        public CombatResolutionEvent(CardInstance attacker, CardInstance defender, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CombatResolution, eventId, timestamp)
        {
            ConfigureDelay(DelayConfig.QueueDrained());
            Source = TargetInfo.ForCard(attacker);
            Target.Add(TargetInfo.ForCard(defender));
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (Attacker == null || Defender == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "CombatResolutionEvent.Commit: Attacker or defender is null." };
            }

            if (!IsCombatParticipantActive(Attacker, isAttacker: true) || !IsCombatParticipantActive(Defender, isAttacker: false))
            {
                return new DomainOperationResult { Success = true };
            }

            return Attacker.PerformAttack(Defender);
        }

        private static bool IsCombatParticipantActive(CardInstance participant, bool isAttacker)
        {
            if (participant.IsLeader)
            {
                return true;
            }

            return participant.CurrentZone == CardZone.Field;
        }
    }
}
