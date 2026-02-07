using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Represents the player's intent to attack with a follower.
    /// This event is the entry point for an attack action. Its Commit method
    /// orchestrates the generation of subsequent events like Clash and Damage.
    /// </summary>
    [GameEventMetadata("攻击", "当一个单位攻击另一个单位时触发（在伤害计算前）。", EventType.Attack)]
    public class AttackEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("攻击者", "发起攻击的单位。", DslDisplayTypeName = "事件源")]
        public CardInstance? Attacker => Source?.Card;

        [GameEventParameter("目标", "被攻击的目标单位。", DslDisplayTypeName = "事件目标")]
        public CardInstance? TargetCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("消耗攻击次数", "是否在提交时消耗攻击次数。", Scope = GameEventParameterScope.Input)]
        public bool ConsumeAttackCount { get; }

        public AttackEvent(CardInstance attacker, CardInstance target, bool consumeAttackCount = false, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Attack, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(attacker);
            Target.Add(TargetInfo.ForCard(target));
            ConsumeAttackCount = consumeAttackCount;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (Attacker == null || TargetCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Attacker or target is null in AttackEvent." };
            }

            var result = new DomainOperationResult { Success = true };
            if (ConsumeAttackCount)
            {
                var oldCanAttack = Attacker.CanAttack;
                Attacker.ApplyAttackPerformed();
                if (oldCanAttack != Attacker.CanAttack)
                {
                    result.GeneratedEvents.Add(new AttackStateUpdatedEvent(Attacker, Attacker.CanAttack));
                }
            }

            if (Attacker.HasKeyword(CardKeyword.Ambush))
            {
                result.GeneratedEvents.Add(new KeywordRemovedEvent(CardKeyword.Ambush, Attacker));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var attacker = Attacker;
            var target = TargetCard;
            if (attacker == null || target == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(attacker)}攻击{AnnouncementTokens.Card(target)}。";
        }
    }
}
